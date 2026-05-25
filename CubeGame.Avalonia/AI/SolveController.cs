using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// AI 풀기의 이동 기록, 솔루션 큐, 상태를 관리합니다.
///
/// UseHistoryMode = true  → 이동 기록을 역순으로 재생해 큐브를 물리적으로 복원
/// UseHistoryMode = false → 큐브 상태를 AI에 전달해 풀이 조언을 받음 (물리 이동 없음)
/// </summary>
public class SolveController
{
    // ── 이동 기록 ─────────────────────────────────────────────────────
    private readonly Stack<(LayerAxis Axis, int Layer, bool Clockwise)> _moveHistory = new();

    // ── 솔루션 재생 큐 (기록 역재생 모드) ────────────────────────────
    private Queue<(LayerAxis Axis, int Layer, bool Clockwise)>? _solutionQueue;

    // ── 비동기 AI 요청 취소 토큰 ─────────────────────────────────────
    private CancellationTokenSource? _cts;

    // ── 공개 설정 ─────────────────────────────────────────────────────
    /// true = 기록 역재생, false = AI에게 큐브 상태 전달
    public bool UseHistoryMode { get; set; } = true;

    // ── 공개 상태 ─────────────────────────────────────────────────────
    public bool   IsRunning     { get; private set; }
    public string StatusMessage { get; private set; } = "";
    public bool   HasHistory    => _moveHistory.Count > 0;
    public bool   HasPending    => _solutionQueue is { Count: > 0 };

    // ── AI 패널 로그 ─────────────────────────────────────────────────
    /// AI에게 던진 요청 내용 (프롬프트 요약)
    public string LastRequest  { get; private set; } = "";
    /// AI로부터 받은 응답 내용
    public string LastResponse { get; private set; } = "";

    // ────────────────────────────────────────────────────────────────
    // 수동 이동 기록 — RotateLayer(recordMove=true) 에서 호출
    // ────────────────────────────────────────────────────────────────
    public void RecordMove(LayerAxis axis, int layer, bool clockwise)
        => _moveHistory.Push((axis, layer, clockwise));

    // ────────────────────────────────────────────────────────────────
    // AI 풀기 시작
    //   cubeStateDesc : UseHistoryMode=false 일 때 큐브 상태 문자열
    // ────────────────────────────────────────────────────────────────
    public void RequestSolve(string cubeStateDesc)
    {
        if (IsRunning) return;

        if (UseHistoryMode)
            StartHistoryMode();
        else
            StartAiOnlyMode(cubeStateDesc);
    }

    // ── 기록 역재생 ───────────────────────────────────────────────────
    private void StartHistoryMode()
    {
        if (_moveHistory.Count == 0)
        {
            LastRequest  = "이동 기록 없음";
            LastResponse = "✅ 큐브가 이미 완성 상태이거나\n기록이 없습니다.";
            StatusMessage = LastResponse;
            return;
        }

        // Stack(LIFO) 순서 그대로 = 최근 이동부터 역방향으로
        var queue = new Queue<(LayerAxis, int, bool)>();
        foreach (var (axis, layer, cw) in _moveHistory)
            queue.Enqueue((axis, layer, !cw));

        _solutionQueue = queue;
        _moveHistory.Clear();
        IsRunning = true;

        LastRequest  = $"이동 기록 {queue.Count}번 — 역방향 복원 요청";
        LastResponse = "🤖 AI 분석 중...";
        StatusMessage = LastResponse;

        var count = queue.Count;
        BeginAiTask(ct => AiSolverService.GetHistoryCommentAsync(count, ct));
    }

    // ── AI 직접 풀이 모드 ─────────────────────────────────────────────
    private void StartAiOnlyMode(string cubeStateDesc)
    {
        IsRunning     = true;
        LastRequest   = cubeStateDesc;
        LastResponse  = "🤖 AI에게 요청 중...";
        StatusMessage = LastResponse;

        BeginAiTask(ct => AiSolverService.GetAiSolutionAsync(cubeStateDesc, ct),
            onComplete: _ => { IsRunning = false; });  // AI 모드: 물리 이동 없음
    }

    // ── 공통 비동기 AI 호출 헬퍼 ─────────────────────────────────────
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
                LastResponse  = result.Response;
                StatusMessage = result.Response;
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
    // 풀이 완료 알림 — Tick()에서 큐가 빌 때 호출
    // ────────────────────────────────────────────────────────────────
    public void NotifyComplete()
    {
        _solutionQueue = null;
        IsRunning      = false;
        StatusMessage  = "✅ AI 풀이 완료!";
        LastResponse   = StatusMessage;
    }

    // ────────────────────────────────────────────────────────────────
    // 풀이 중지 — 중지 버튼 클릭 시 호출
    // ────────────────────────────────────────────────────────────────
    public void StopSolving()
    {
        _cts?.Cancel();
        _cts           = null;
        _solutionQueue = null;
        IsRunning      = false;
        StatusMessage  = "⛔ 풀이 중지됨";
        LastResponse   = StatusMessage;
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
        IsRunning      = false;
        StatusMessage  = "";
        LastRequest    = "";
        LastResponse   = "";
    }
}
