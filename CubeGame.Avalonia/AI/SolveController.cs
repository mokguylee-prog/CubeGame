using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// AI SubAgent 파이프라인의 현재 단계를 나타냅니다.
/// None → Analysis → MovesGen → Verifying → Executing → Done / Failed
/// </summary>
public enum PipelineStep
{
    None,        // 대기 중
    Analysis,    // Agent 1: 큐브 상태 분석
    MovesGen,    // Agent 2: 풀이 수식 생성
    Verifying,   // C# 시뮬레이션 검증
    Executing,   // 수식 실행(큐 재생) 중
    Done,        // 완료
    Failed       // 실패
}

/// <summary>
/// AI 풀기의 이동 기록, 솔루션 큐, 상태를 관리합니다.
///
/// UseHistoryMode = true  → 이동 기록을 역순으로 재생해 큐브를 물리적으로 복원 (확실)
/// UseHistoryMode = false → SubAgent 파이프라인으로 AI가 생성한 수식을 검증 후 실행
///                          (Agent1: 상태분석 → Agent2: 수식생성 → C# 검증 → 큐 실행)
/// </summary>
public class SolveController
{
    // ── 이동 기록 ─────────────────────────────────────────────────────
    private readonly Stack<(LayerAxis Axis, int Layer, bool Clockwise)> _moveHistory = new();

    // ── 솔루션 재생 큐 ───────────────────────────────────────────────
    private Queue<(LayerAxis Axis, int Layer, bool Clockwise)>? _solutionQueue;

    // ── 비동기 취소 토큰 ─────────────────────────────────────────────
    private CancellationTokenSource? _cts;

    // ── 공개 설정 ─────────────────────────────────────────────────────
    /// true = 기록 역재생 (확실), false = AI SubAgent 파이프라인
    public bool UseHistoryMode { get; set; } = true;

    // ── 공개 상태 ─────────────────────────────────────────────────────
    public bool         IsRunning     { get; private set; }
    public string       StatusMessage { get; private set; } = "";
    public bool         HasHistory    => _moveHistory.Count > 0;
    public bool         HasPending    => _solutionQueue is { Count: > 0 };

    /// AI SubAgent 파이프라인 현재 단계 (UI 단계 표시바에 사용)
    public PipelineStep Step          { get; private set; } = PipelineStep.None;
    /// Agent2 재시도 번호 (1~3), 표시용
    public int          Attempt       { get; private set; } = 0;
    /// Agent2 최대 재시도 횟수
    public const int    MaxAttempts   = 3;

    // AI 패널 로그
    public string LastRequest  { get; private set; } = "";
    public string LastResponse { get; private set; } = "";

    /// 솔루션 직접 입력 패널 상태 메시지
    public string SolutionStatus { get; set; } = "";

    // ── 복사 피드백 토스트 ────────────────────────────────────────────
    public enum CopiedKind { None, Request, Response }
    public CopiedKind LastCopied    { get; private set; } = CopiedKind.None;
    public DateTime   LastCopiedAt  { get; private set; } = DateTime.MinValue;
    private const double ToastSec   = 1.5;

    public void NotifyCopied(CopiedKind kind)
    {
        LastCopied   = kind;
        LastCopiedAt = DateTime.UtcNow;
    }

    /// 토스트가 아직 표시 중이면 [0.0, 1.0] 불투명도, 아니면 0
    public double CopyToastAlpha(CopiedKind kind)
    {
        if (LastCopied != kind) return 0;
        var elapsed = (DateTime.UtcNow - LastCopiedAt).TotalSeconds;
        if (elapsed >= ToastSec) return 0;
        // 마지막 0.4초 동안 fade-out
        var fade = ToastSec - 0.4;
        return elapsed < fade ? 1.0 : 1.0 - (elapsed - fade) / 0.4;
    }

    // ────────────────────────────────────────────────────────────────
    // 수동 이동 기록
    // ────────────────────────────────────────────────────────────────
    public void RecordMove(LayerAxis axis, int layer, bool clockwise)
        => _moveHistory.Push((axis, layer, clockwise));

    // ────────────────────────────────────────────────────────────────
    // AI 풀기 시작
    //   cubeStateDesc : UseHistoryMode=false 일 때 FormatCubeState() 결과
    //   cube          : UseHistoryMode=false 일 때 검증용 현재 큐브
    // ────────────────────────────────────────────────────────────────
    public void RequestSolve(string cubeStateDesc, Cube3x3? cube = null)
    {
        if (IsRunning) return;

        if (UseHistoryMode)
            StartHistoryMode();
        else
            StartAiPipelineMode(cubeStateDesc, cube);
    }

    // ── 기록 역재생 모드 ─────────────────────────────────────────────
    private void StartHistoryMode()
    {
        if (_moveHistory.Count == 0)
        {
            LastRequest   = "이동 기록 없음";
            LastResponse  = "✅ 큐브가 이미 완성 상태이거나\n기록이 없습니다.";
            StatusMessage = LastResponse;
            return;
        }

        // Stack(LIFO) 순서 = 최근 이동부터 역방향
        var queue = new Queue<(LayerAxis, int, bool)>();
        foreach (var (axis, layer, cw) in _moveHistory)
            queue.Enqueue((axis, layer, !cw));

        _solutionQueue = queue;
        _moveHistory.Clear();
        IsRunning = true;

        LastRequest   = $"이동 기록 {queue.Count}번 — 역방향 복원 요청";
        LastResponse  = "🤖 AI 분석 중...";
        StatusMessage = LastResponse;

        var count = queue.Count;
        BeginAiTask(ct => AiSolverService.GetHistoryCommentAsync(count, ct));
    }

    // ── AI SubAgent 파이프라인 모드 ──────────────────────────────────
    private void StartAiPipelineMode(string cubeStateDesc, Cube3x3? cube)
    {
        IsRunning     = true;
        Step          = PipelineStep.Analysis;
        Attempt       = 0;
        LastRequest   = cubeStateDesc;
        LastResponse  = "🔍 [1단계] 큐브 상태 분석 요청 중...";
        StatusMessage = LastResponse;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(() => RunAiPipelineAsync(cubeStateDesc, cube, ct), ct);
    }

    // ── Agent 파이프라인 본체 (비동기 루프) ─────────────────────────
    private async Task RunAiPipelineAsync(
        string cubeStateDesc, Cube3x3? cube, CancellationToken ct)
    {
        try
        {
            // ══════════════════════════════════════════════════════════
            // Agent 1: 큐브 상태 분석
            // ══════════════════════════════════════════════════════════
            await SetStepAsync(PipelineStep.Analysis, 0,
                "🔍 [1단계] Agent1 큐브 상태 분석 중...\n(AI에 6면 정보 전송)", ct);

            var analysisResult = await AiSolverService.GetCubeAnalysisAsync(cubeStateDesc, ct);
            var analysis       = analysisResult.Response;
            bool analysisOk    = !analysis.StartsWith("Analysis failed") && !analysis.StartsWith("No API");

            await SetStepAsync(PipelineStep.MovesGen, 0,
                analysisOk
                    ? $"✅ [1단계 완료] 분석: {TruncateLine(analysis, 36)}\n🔄 [2단계] Agent2 풀이 수식 생성 요청..."
                    : $"⚠️ [1단계 부분실패] {TruncateLine(analysis, 36)}\n🔄 [2단계] Agent2 풀이 수식 생성 요청...",
                ct);

            // ══════════════════════════════════════════════════════════
            // Agent 2 + 검증 루프 (최대 MaxAttempts 회 재시도)
            // ══════════════════════════════════════════════════════════
            string? lastAiMoves = null;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                await SetStepAsync(PipelineStep.MovesGen, attempt,
                    $"🤖 [2단계 시도 {attempt}/{MaxAttempts}] Agent2 수식 생성 중...\n" +
                    $"    (OpenRouter 무료 모델 호출)", ct);

                var movesResult = await AiSolverService.GetSolvingMovesAsync(
                    cubeStateDesc, analysis, ct);

                var parsed  = RubikNotation.Parse(movesResult.Response);
                lastAiMoves = movesResult.Response;

                if (parsed.Count == 0)
                {
                    await SetStepAsync(PipelineStep.MovesGen, attempt,
                        $"⚠️ [2단계 {attempt}/{MaxAttempts}] 수식 파싱 실패\n" +
                        $"    AI 응답: {TruncateLine(movesResult.Response, 36)}\n" +
                        $"    → 힌트 강화 후 재시도...", ct);

                    analysis += " Focus on providing ONLY move notation tokens.";
                    continue;
                }

                // ── C# 시뮬레이션으로 검증 ──────────────────────────────
                await SetStepAsync(PipelineStep.Verifying, attempt,
                    $"🔬 [3단계] 수식 검증 중 ({parsed.Count}수)...\n" +
                    $"    {TruncateLine(RubikNotation.ToNotationString(parsed), 36)}", ct);

                bool verified = cube is not null && CubeVerifier.VerifySolution(cube, parsed);

                if (verified)
                {
                    // ✅ 검증 성공 → 큐에 등록하고 실행
                    var notation = RubikNotation.ToNotationString(parsed);
                    var queue    = new Queue<(LayerAxis, int, bool)>();
                    foreach (var m in parsed) queue.Enqueue((m.Axis, m.Layer, m.Clockwise));

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (ct.IsCancellationRequested) return;
                        Step           = PipelineStep.Executing;
                        _solutionQueue = queue;
                        LastResponse   =
                            $"✅ [3단계 검증 완료] {parsed.Count}수 풀이 확인!\n" +
                            $"▶ [4단계] 수식 실행 중...\n" +
                            $"    {TruncateLine(notation, 36)}";
                        StatusMessage  = LastResponse;
                    });
                    return;  // 파이프라인 완료
                }

                // 검증 실패
                await SetStepAsync(PipelineStep.MovesGen, attempt,
                    $"❌ [3단계] 검증 실패 ({attempt}/{MaxAttempts})\n" +
                    $"    수식이 큐브를 완성하지 못함\n" +
                    $"    → 힌트 강화 후 Agent2 재시도...", ct);

                analysis +=
                    $" Previous attempt '{TruncateLine(lastAiMoves, 28)}'" +
                    " did not solve the cube. Recalculate carefully.";
            }

            // ══════════════════════════════════════════════════════════
            // 모든 재시도 실패 → fallback 제안
            // ══════════════════════════════════════════════════════════
            string finalMsg = cube is null
                ? $"❌ [실패] AI 수식 생성 실패 ({MaxAttempts}회)\n" +
                  $"    AI 응답: {TruncateLine(lastAiMoves ?? "", 36)}"
                : $"❌ [실패] {MaxAttempts}번 시도 모두 검증 실패\n" +
                  $"    → '기록 역재생' 체크박스 ON 권장\n" +
                  $"    (섞기 후 AI 풀기 = 기록 역재생이 확실)";

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Step           = PipelineStep.Failed;
                IsRunning      = false;
                LastResponse   = finalMsg;
                StatusMessage  = finalMsg;
            });
        }
        catch (OperationCanceledException)
        {
            // 취소는 StopSolving()이 처리함
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Step           = PipelineStep.Failed;
                IsRunning      = false;
                LastResponse   = $"❌ 파이프라인 오류\n    {ex.Message}";
                StatusMessage  = LastResponse;
            });
        }
    }

    // ── 단계+응답 동시 업데이트 헬퍼 ───────────────────────────────
    private async Task SetStepAsync(
        PipelineStep step, int attempt, string message, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (ct.IsCancellationRequested) return;
            Step           = step;
            Attempt        = attempt;
            LastResponse   = message;
            StatusMessage  = message;
        });
    }

    private void BeginAiTask(
        Func<CancellationToken, Task<AiResult>> call,
        Action<AiResult>? onComplete = null)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            var result = await call(ct);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested) return;
                LastResponse   = result.Response;
                StatusMessage  = result.Response;
                onComplete?.Invoke(result);
            });
        }, ct);
    }

    // ────────────────────────────────────────────────────────────────
    // 솔루션 큐에서 다음 이동 꺼내기 — Tick()에서 호출
    // ────────────────────────────────────────────────────────────────
    public bool TryDequeueNext(out LayerAxis axis, out int layer, out bool clockwise)
    {
        if (_solutionQueue is { Count: > 0 })
        {
            var (a, l, cw) = _solutionQueue.Dequeue();
            axis = a; layer = l; clockwise = cw;
            return true;
        }
        axis = default; layer = 0; clockwise = false;
        return false;
    }

    // ────────────────────────────────────────────────────────────────
    // 풀이 완료 알림 — 큐가 빌 때 Tick()에서 호출
    // ────────────────────────────────────────────────────────────────
    public void NotifyComplete()
    {
        _solutionQueue = null;
        Step           = PipelineStep.Done;
        IsRunning      = false;
        StatusMessage  = "✅ [완료] AI 풀이 실행 완료!";
        LastResponse   = StatusMessage;
    }

    // ────────────────────────────────────────────────────────────────
    // 풀이 중지 — 중지 버튼 클릭
    // ────────────────────────────────────────────────────────────────
    public void StopSolving()
    {
        _cts?.Cancel();
        _cts           = null;
        _solutionQueue = null;
        Step           = PipelineStep.None;
        IsRunning      = false;
        StatusMessage  = "⛔ 풀이 중지됨";
        LastResponse   = StatusMessage;
    }

    // ────────────────────────────────────────────────────────────────
    // 솔루션 직접 입력 — 외부 AI에서 받은 표기법 문자열을 바로 실행
    // ────────────────────────────────────────────────────────────────
    public void QueueFromNotation(string notation, Cube3x3? cube)
    {
        if (IsRunning)
        {
            SolutionStatus = "⚠️ 현재 AI 풀이 실행 중 — 완료 후 다시 시도하세요";
            return;
        }
        if (string.IsNullOrWhiteSpace(notation))
        {
            SolutionStatus = "⚠️ 빈 입력 — 큐브 표기법을 입력하세요";
            return;
        }

        var parsed = RubikNotation.Parse(notation);
        if (parsed.Count == 0)
        {
            SolutionStatus =
                "❌ 파싱 실패\n" +
                "    유효한 큐브 표기법이 없습니다\n" +
                "    예) R U R' U' F B2 L D'";
            return;
        }

        // 검증: 현재 큐브에서 이 수식이 완성 상태를 만드는지 확인
        bool verified = cube is not null && CubeVerifier.VerifySolution(cube, parsed);

        var queue = new Queue<(LayerAxis, int, bool)>();
        foreach (var m in parsed) queue.Enqueue((m.Axis, m.Layer, m.Clockwise));

        _solutionQueue = queue;
        IsRunning      = true;
        Step           = PipelineStep.Executing;

        var notation2 = RubikNotation.ToNotationString(parsed);
        SolutionStatus = verified
            ? $"✅ 검증 완료! {parsed.Count}수 실행 중...\n" +
              $"    {TruncateLine(notation2, 38)}"
            : $"▶ {parsed.Count}수 실행 중... (검증: 이 수식으론 완성 안 됨)\n" +
              $"    {TruncateLine(notation2, 38)}";

        LastResponse  = SolutionStatus;
        StatusMessage = SolutionStatus;
    }

    // ────────────────────────────────────────────────────────────────
    // 큐브 리셋 시 전체 초기화
    // ────────────────────────────────────────────────────────────────
    public void Reset()
    {
        _cts?.Cancel();
        _cts           = null;
        _solutionQueue = null;
        _moveHistory.Clear();
        Step           = PipelineStep.None;
        Attempt        = 0;
        IsRunning      = false;
        StatusMessage  = "";
        LastRequest    = "";
        LastResponse   = "";
        SolutionStatus = "";
    }

    // ── 내부 유틸 ────────────────────────────────────────────────────
    private static string TruncateLine(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var firstLine = s.Split('\n')[0].Trim();
        return firstLine.Length <= max ? firstLine : firstLine[..(max - 1)] + "…";
    }
}
