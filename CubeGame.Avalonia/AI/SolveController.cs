using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CubeGame.Avalonia.Scene;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// AI 풀기의 이동 기록, 솔루션 큐, 상태를 관리합니다.
/// MainWindow에서 분리된 모든 AI 풀기 제어 로직이 여기에 있습니다.
/// </summary>
public class SolveController
{
    // ── 이동 기록 (수동 조작마다 Push) ─────────────────────────────────
    private readonly Stack<(LayerAxis Axis, int Layer, bool Clockwise)> _moveHistory = new();

    // ── 솔루션 재생 큐 (AiSolve 시 역방향으로 채워짐) ──────────────────
    private Queue<(LayerAxis Axis, int Layer, bool Clockwise)>? _solutionQueue;

    // ── AI API 비동기 요청 취소 토큰 ────────────────────────────────────
    private CancellationTokenSource? _cts;

    // ── 공개 상태 ───────────────────────────────────────────────────────
    public bool IsRunning     { get; private set; }
    public string StatusMessage { get; private set; } = "";
    public bool HasHistory    => _moveHistory.Count > 0;
    public bool HasPending    => _solutionQueue is { Count: > 0 };

    // ────────────────────────────────────────────────────────────────────
    // 수동 이동 기록 (RotateLayer에서 호출)
    // ────────────────────────────────────────────────────────────────────
    public void RecordMove(LayerAxis axis, int layer, bool clockwise)
        => _moveHistory.Push((axis, layer, clockwise));

    // ────────────────────────────────────────────────────────────────────
    // AI 풀기 시작
    //   setStatus : 상태 메시지가 바뀔 때 UI 스레드에서 호출할 콜백
    // ────────────────────────────────────────────────────────────────────
    public void RequestSolve(Action<string> setStatus)
    {
        if (IsRunning) return;

        if (_moveHistory.Count == 0)
        {
            SetStatus("✅ 큐브가 이미 완성 상태이거나 기록이 없습니다.", setStatus);
            return;
        }

        // Stack은 LIFO이므로 그대로 순회하면 역방향 순서가 됨
        var queue = new Queue<(LayerAxis, int, bool)>();
        foreach (var (axis, layer, cw) in _moveHistory)
            queue.Enqueue((axis, layer, !cw));   // 방향 반전

        _solutionQueue = queue;
        _moveHistory.Clear();
        IsRunning = true;
        SetStatus("🤖 AI 분석 중...", setStatus);

        // 무료 AI 모델에 비동기로 설명 요청
        var count = queue.Count;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            var msg = await AiSolverService.GetSolvingCommentAsync(count, ct);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ct.IsCancellationRequested)
                    SetStatus(msg, setStatus);
            });
        }, ct);
    }

    // ────────────────────────────────────────────────────────────────────
    // Tick()에서 호출 — 다음 솔루션 이동을 꺼냄
    // ────────────────────────────────────────────────────────────────────
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

    // ────────────────────────────────────────────────────────────────────
    // 풀이 완료 알림 (Tick에서 큐가 비었을 때 호출)
    // ────────────────────────────────────────────────────────────────────
    public void NotifyComplete(Action<string> setStatus)
    {
        _solutionQueue = null;
        IsRunning = false;
        SetStatus("✅ AI 풀이 완료!", setStatus);
    }

    // ────────────────────────────────────────────────────────────────────
    // 큐브 리셋 시 모든 상태 초기화
    // ────────────────────────────────────────────────────────────────────
    public void Reset()
    {
        _cts?.Cancel();
        _cts = null;
        _solutionQueue = null;
        _moveHistory.Clear();
        IsRunning = false;
        StatusMessage = "";
    }

    // ────────────────────────────────────────────────────────────────────
    private void SetStatus(string msg, Action<string> setStatus)
    {
        StatusMessage = msg;
        setStatus(msg);
    }
}
