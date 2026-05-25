using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CubeGame.Avalonia.AI;
using CubeGame.Avalonia.Game;
using CubeGame.Avalonia.Render;
using CubeGame.Avalonia.Scene;
using CubeGame.Avalonia.UI;

namespace CubeGame.Avalonia;

public partial class MainWindow : Window
{
    private readonly Cube3x3 _cube = new();
    private readonly CubeRenderer _renderer = new();
    private readonly GameState _game = new();
    private readonly Input _input = new();
    private readonly Overlay _overlay = new();
    private readonly SolveController _solver = new();   // ← AI 풀기 전담
    private LayerTurnAnimation? _turn;
    private Queue<(LayerAxis Axis, int Layer, bool Clockwise)>? _scrambleQueue;
    private static readonly Random Rng = new();

    // ── 솔루션 직접 입력 — 네이티브 TextBox ──────────────────────────
    private TextBox? _solutionBox;

    public MainWindow()
    {
        InitializeComponent();
        Title = "3D Cube Game Simulator";
        Width = 800; Height = 640;
        MinWidth = 700; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = null;

        CubeStateStore.Load(_cube, _renderer);

        // ── 레이아웃: Grid (CubeView 전체 + TextBox 오버레이) ─────────
        var cubeView = new CubeView(this);

        _solutionBox = new TextBox
        {
            AcceptsReturn            = true,
            TextWrapping             = TextWrapping.Wrap,
            PlaceholderText          = "예: R U R' U' F' B L2 D ...",
            FontFamily               = new FontFamily("Courier New"),
            FontSize                 = 11,
            Width                    = SolutionPanelOverlay.TextBoxWidth,
            Height                   = SolutionPanelOverlay.TextBoxHeight,
            HorizontalAlignment      = HorizontalAlignment.Left,
            VerticalAlignment        = VerticalAlignment.Bottom,
            Margin                   = new Thickness(
                                           SolutionPanelOverlay.TextBoxLeft, 0, 0,
                                           SolutionPanelOverlay.TextBoxMarginBottom),
            Background               = new SolidColorBrush(Color.FromArgb(160, 8, 14, 28)),
            Foreground               = new SolidColorBrush(Color.FromArgb(255, 170, 225, 255)),
            BorderBrush              = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),  // 그리기로 대체
            BorderThickness          = new Thickness(0),
            Padding                  = new Thickness(4, 3),
            CaretBrush               = new SolidColorBrush(Colors.Cyan),
            SelectionBrush           = new SolidColorBrush(Color.FromArgb(120, 0, 150, 255)),
            MaxLines                 = 4,
        };

        var grid = new Grid();
        grid.Children.Add(cubeView);
        grid.Children.Add(_solutionBox);
        Content = grid;

        KeyDown += OnKeyDown;
        KeyUp   += (_, e) => _input.OnKeyUp(e);
        Closing += (_, _) => SaveCubeState();
    }

    // ── 키 입력 ─────────────────────────────────────────────────────────
    private void OnKeyDown(object? _, KeyEventArgs e)
    {
        _input.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space: SelectCurrentFace(); break;
            case Key.R:     _game.Reset();       break;
            case Key.H:     _overlay.ShowHelp = !_overlay.ShowHelp; break;
            case Key.T:     _game.ToggleAutoRotate(); break;
            case Key.U: RotateViewLayer(LayerAxis.Y,  1, !e.KeyModifiers.HasFlag(KeyModifiers.Shift)); break;
            case Key.I: RotateViewLayer(LayerAxis.Y,  0, !e.KeyModifiers.HasFlag(KeyModifiers.Shift)); break;
            case Key.O: RotateViewLayer(LayerAxis.Y, -1, !e.KeyModifiers.HasFlag(KeyModifiers.Shift)); break;
            case Key.J: RotateViewLayer(LayerAxis.X, -1, !e.KeyModifiers.HasFlag(KeyModifiers.Shift)); break;
            case Key.K: RotateViewLayer(LayerAxis.X,  0, !e.KeyModifiers.HasFlag(KeyModifiers.Shift)); break;
            case Key.L: RotateViewLayer(LayerAxis.X,  1, !e.KeyModifiers.HasFlag(KeyModifiers.Shift)); break;
            case Key.D1: case Key.NumPad1: _game.SetMode(CubeMode.Standard); break;
            case Key.D2: case Key.NumPad2: _game.SetMode(CubeMode.Current);  break;
            case Key.Escape: Close(); break;
        }
    }

    // ── 큐브 조작 ───────────────────────────────────────────────────────
    private void SelectCurrentFace() => _game.CheckMatch(_renderer.GetFrontFaceName());

    /// <param name="recordMove">수동 이동일 때 true, AI 솔루션 재생 중 false</param>
    private void RotateLayer(LayerAxis axis, int layer, bool clockwise, bool recordMove = true)
    {
        if (_turn is not null) return;

        if (recordMove)
            _solver.RecordMove(axis, layer, clockwise);   // SolveController에 기록

        _turn = new LayerTurnAnimation(axis, layer, clockwise);
        if (_game.AutoRotate) _game.ToggleAutoRotate();
    }

    private void RotateViewLayer(LayerAxis viewGroup, int visualLayer, bool clockwise)
    {
        var (axis, layer) = _renderer.ResolveViewLayer(viewGroup, visualLayer);
        RotateLayer(axis, layer, clockwise);
    }

    // ── 큐브 리셋 ───────────────────────────────────────────────────────
    private void ResetCube()
    {
        _turn = null;
        _solver.Reset();          // AI 풀기 상태 초기화
        _cube.Reset();
        _renderer.ResetView();
        SaveCubeState();
    }

    // ── RANDOM 섞기 ─────────────────────────────────────────────────────
    private void StartScramble()
    {
        // 풀이 중이거나 이미 섞는 중이면 무시
        if (_solver.IsRunning || _scrambleQueue is { Count: > 0 }) return;

        // 기록·상태 초기화 후 새 섞기 시작
        _solver.Reset();
        _turn = null;

        var queue = new Queue<(LayerAxis, int, bool)>();
        LayerAxis lastAxis = (LayerAxis)99;
        int       lastLayer = 99;

        for (int i = 0; i < 20; i++)
        {
            LayerAxis axis;
            int       layer;

            // 같은 축+레이어 연속 방지 (바로 상쇄되는 수 제외)
            do
            {
                axis  = Rng.Next(2) == 0 ? LayerAxis.X : LayerAxis.Y;
                layer = Rng.Next(3) - 1;           // -1, 0, 1
            } while (axis == lastAxis && layer == lastLayer);

            var cw = Rng.Next(2) == 0;
            queue.Enqueue((axis, layer, cw));
            lastAxis  = axis;
            lastLayer = layer;
        }

        _scrambleQueue = queue;
    }

    // ── AI 풀기 ─────────────────────────────────────────────────────────
    private void AiSolve()
    {
        // 기록 역재생: 이동 수 요약 / AI SubAgent: 큐브 6면 상태 + 큐브 객체(검증용) 전달
        var cubeDesc = _solver.UseHistoryMode ? "" : FormatCubeState();
        _solver.RequestSolve(cubeDesc, _solver.UseHistoryMode ? null : _cube);
    }

    // ── 클립보드 복사 ────────────────────────────────────────────────────
    internal void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            _ = clipboard.SetValueAsync(DataFormat.Text, text);
    }

    // ── 솔루션 직접 입력 실행 ───────────────────────────────────────────
    internal void ExecuteSolutionText()
    {
        var text = _solutionBox?.Text ?? "";
        _solver.QueueFromNotation(text, _cube);
    }

    // ── 솔루션 입력 지우기 ──────────────────────────────────────────────
    internal void ClearSolutionText()
    {
        if (_solutionBox != null) _solutionBox.Text = "";
        _solver.SolutionStatus = "";
    }

    // ── 큐브 상태 → AI 전달용 문자열 ────────────────────────────────────
    private string FormatCubeState()
    {
        static char C(Cubie cubie, FaceDir dir)
        {
            var c = cubie.FaceColors[(int)dir];
            if (c.A == 0)                             return '.';
            if (c.R > 200 && c.G > 200 && c.B > 200) return 'W'; // 흰
            if (c.R > 200 && c.G > 180 && c.B <  20) return 'Y'; // 노
            if (c.R > 200 && c.G > 100 && c.B <  20) return 'O'; // 주
            if (c.R > 150 && c.G <  100 && c.B < 100) return 'R'; // 빨
            if (c.G > 150 && c.R < 100)               return 'G'; // 초
            if (c.B > 180 && c.R < 100)               return 'B'; // 파
            return '?';
        }

        string Face((int x, int y, int z, FaceDir d)[] s)
        {
            var ch = new char[9];
            for (int i = 0; i < 9; i++) ch[i] = C(_cube[s[i].x, s[i].y, s[i].z], s[i].d);
            return $"{ch[0]}{ch[1]}{ch[2]}|{ch[3]}{ch[4]}{ch[5]}|{ch[6]}{ch[7]}{ch[8]}";
        }

        var u = Face([(-1,1, 1,FaceDir.Up),  (0,1, 1,FaceDir.Up),  (1,1, 1,FaceDir.Up),
                      (-1,1, 0,FaceDir.Up),  (0,1, 0,FaceDir.Up),  (1,1, 0,FaceDir.Up),
                      (-1,1,-1,FaceDir.Up),  (0,1,-1,FaceDir.Up),  (1,1,-1,FaceDir.Up)]);
        var d = Face([(-1,-1,-1,FaceDir.Down),(0,-1,-1,FaceDir.Down),(1,-1,-1,FaceDir.Down),
                      (-1,-1, 0,FaceDir.Down),(0,-1, 0,FaceDir.Down),(1,-1, 0,FaceDir.Down),
                      (-1,-1, 1,FaceDir.Down),(0,-1, 1,FaceDir.Down),(1,-1, 1,FaceDir.Down)]);
        var f = Face([(-1, 1,-1,FaceDir.Front),(0, 1,-1,FaceDir.Front),(1, 1,-1,FaceDir.Front),
                      (-1, 0,-1,FaceDir.Front),(0, 0,-1,FaceDir.Front),(1, 0,-1,FaceDir.Front),
                      (-1,-1,-1,FaceDir.Front),(0,-1,-1,FaceDir.Front),(1,-1,-1,FaceDir.Front)]);
        var b = Face([ (1, 1,1,FaceDir.Back), (0, 1,1,FaceDir.Back),(-1, 1,1,FaceDir.Back),
                       (1, 0,1,FaceDir.Back), (0, 0,1,FaceDir.Back),(-1, 0,1,FaceDir.Back),
                       (1,-1,1,FaceDir.Back), (0,-1,1,FaceDir.Back),(-1,-1,1,FaceDir.Back)]);
        var l = Face([(-1, 1, 1,FaceDir.Left),(-1, 1, 0,FaceDir.Left),(-1, 1,-1,FaceDir.Left),
                      (-1, 0, 1,FaceDir.Left),(-1, 0, 0,FaceDir.Left),(-1, 0,-1,FaceDir.Left),
                      (-1,-1, 1,FaceDir.Left),(-1,-1, 0,FaceDir.Left),(-1,-1,-1,FaceDir.Left)]);
        var r = Face([ (1, 1,-1,FaceDir.Right),(1, 1, 0,FaceDir.Right),(1, 1, 1,FaceDir.Right),
                       (1, 0,-1,FaceDir.Right),(1, 0, 0,FaceDir.Right),(1, 0, 1,FaceDir.Right),
                       (1,-1,-1,FaceDir.Right),(1,-1, 0,FaceDir.Right),(1,-1, 1,FaceDir.Right)]);

        return new StringBuilder()
            .AppendLine($"U(상): {u}").AppendLine($"D(하): {d}")
            .AppendLine($"F(앞): {f}").AppendLine($"B(뒤): {b}")
            .AppendLine($"L(좌): {l}").Append    ($"R(우): {r}")
            .ToString();
    }

    // ── 저장 ────────────────────────────────────────────────────────────
    private void SaveCubeState() => CubeStateStore.Save(_cube, _renderer);

    // ════════════════════════════════════════════════════════════════════
    //  렌더링 + 게임 루프
    // ════════════════════════════════════════════════════════════════════
    private sealed class CubeView : Control
    {
        private readonly MainWindow _w;
        private readonly DispatcherTimer _timer = new(DispatcherPriority.Render)
            { Interval = TimeSpan.FromMilliseconds(16) };
        private const float RotSpeed = 0.04f;

        public CubeView(MainWindow w)
        {
            _w = w;
            Focusable = true;
            PointerPressed += OnPointerPressed;
            _timer.Tick += (_, _) => { Tick(); InvalidateVisual(); };
            _timer.Start();
        }

        // ── 마우스 클릭 ──────────────────────────────────────────────────
        private void OnPointerPressed(object? _, PointerPressedEventArgs e)
        {
            Focus();
            var pt = e.GetPosition(this);
            int w = (int)Bounds.Width, h = (int)Bounds.Height;

            if (Overlay.TryHitModeButton(pt, out var mode))
                { _w._game.SetMode(mode); Consume(e); return; }

            if (Overlay.HitSelectButton(pt))
                { _w.SelectCurrentFace(); Consume(e); return; }

            if (Overlay.HitRotationButton(pt))
                { _w._game.ToggleAutoRotate(); Consume(e); return; }

            if (Overlay.HitCubeResetButton(pt))
                { _w.ResetCube(); Consume(e); return; }

            if (Overlay.HitScrambleButton(pt))
                { _w.StartScramble(); Consume(e); return; }

            // AI 패널 A: AI버튼·중지버튼·체크박스 / 패널 B: 복사버튼
            var aiHit = AiButtonOverlay.TryHit(pt, w, h);
            if (aiHit != AiButtonOverlay.Hit.None)
            {
                switch (aiHit)
                {
                    case AiButtonOverlay.Hit.AiButton:
                        _w.AiSolve(); break;
                    case AiButtonOverlay.Hit.StopButton:
                        _w._solver.StopSolving(); break;
                    case AiButtonOverlay.Hit.Checkbox:
                        _w._solver.UseHistoryMode = !_w._solver.UseHistoryMode; break;
                    case AiButtonOverlay.Hit.CopyRequest:
                        _w.CopyToClipboard(_w._solver.LastRequest); break;
                    case AiButtonOverlay.Hit.CopyResponse:
                        _w.CopyToClipboard(_w._solver.LastResponse); break;
                }
                Consume(e);
                return;
            }

            // 솔루션 직접 입력 패널: 실행·지우기 버튼
            var solHit = SolutionPanelOverlay.TryHit(pt, h);
            if (solHit != SolutionPanelOverlay.Hit.None)
            {
                switch (solHit)
                {
                    case SolutionPanelOverlay.Hit.Run:
                        _w.ExecuteSolutionText(); break;
                    case SolutionPanelOverlay.Hit.Clear:
                        _w.ClearSolutionText(); break;
                }
                Consume(e);
                return;
            }

            if (Overlay.TryHitLayerButton(pt, out var axis, out var layer))
                { _w.RotateViewLayer(axis, layer, true); Consume(e); }
        }

        private void Consume(PointerPressedEventArgs e)
        { e.Handled = true; InvalidateVisual(); }

        // ── 게임 틱 ──────────────────────────────────────────────────────
        private void Tick()
        {
            // 뷰 회전
            var inp = _w._input;
            var r   = _w._renderer;
            if (inp.Left)  r.Rotate(0,  RotSpeed);
            if (inp.Right) r.Rotate(0, -RotSpeed);
            if (inp.Up)    r.Rotate(-RotSpeed, 0);
            if (inp.Down)  r.Rotate( RotSpeed, 0);
            if (_w._game.AutoRotate && !inp.Left && !inp.Right && !inp.Up && !inp.Down)
                r.Rotate(0.003f, 0.005f);

            // 레이어 애니메이션 진행
            if (_w._turn is not null)
            {
                // 섞는 중엔 3배 빠르게 (20번 × ~130ms ≈ 2.6초)
                float speed = _w._scrambleQueue is { Count: > 0 } ? 0.13f : 0.042f;
                _w._turn.Advance(speed);

                if (_w._turn.IsComplete)
                {
                    _w._cube.RotateLayer(_w._turn.Axis, _w._turn.Layer, _w._turn.Clockwise);
                    _w._turn = null;
                    _w.SaveCubeState();

                    // ── 스크램블 큐 우선 ──────────────────────────────────
                    if (_w._scrambleQueue is { Count: > 0 })
                    {
                        var (sa, sl, scw) = _w._scrambleQueue.Dequeue();
                        _w.RotateLayer(sa, sl, scw, recordMove: true);   // 기록 O
                    }
                    // ── 솔루션 큐 ─────────────────────────────────────────
                    else if (_w._solver.TryDequeueNext(out var a, out var l, out var cw))
                        _w.RotateLayer(a, l, cw, recordMove: false);
                    else if (!_w._solver.HasPending && _w._solver.IsRunning)
                        _w._solver.NotifyComplete();
                }
            }
            // 첫 이동 시작 (turn 없을 때)
            else if (_w._turn is null)
            {
                if (_w._scrambleQueue is { Count: > 0 })
                {
                    var (sa, sl, scw) = _w._scrambleQueue.Dequeue();
                    _w.RotateLayer(sa, sl, scw, recordMove: true);
                }
                else if (_w._solver.HasPending)
                {
                    if (_w._solver.TryDequeueNext(out var a, out var l, out var cw))
                        _w.RotateLayer(a, l, cw, recordMove: false);
                }
            }
        }

        // ── 렌더 ─────────────────────────────────────────────────────────
        public override void Render(DrawingContext dc)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w < 1 || h < 1) return;

            DrawSpaceTunnel(dc, w, h);
            _w._renderer.Render(dc, (int)w, (int)h, _w._cube, _w._turn);
            _w._overlay.Draw(dc, (int)w, (int)h, _w._game,
                _w._renderer.GetDisplayLabel(_w._cube, _w._game.Mode),
                _w._solver);   // ← SolveController 전달
        }

        // ── 우주 터널 배경 ────────────────────────────────────────────────
        private static void DrawSpaceTunnel(DrawingContext dc, double w, double h)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(9, 10, 20)), null, new Rect(0, 0, w, h));

            var cx = CubeRenderer.GetCubeCenterX((int)w);
            var cy = CubeRenderer.GetCubeCenterY((int)h) - 10;
            var maxR = Math.Sqrt(w * w + h * h);

            for (int i = 0; i < 30; i++)
            {
                var angle  = i * Math.PI * 2 / 30.0;
                var wobble = Math.Sin(i * 1.73) * 0.16;
                var ex = cx + Math.Cos(angle + wobble) * maxR;
                var ey = cy + Math.Sin(angle + wobble) * maxR * 0.72;
                var color = i % 3 == 0 ? Color.FromArgb(82, 0, 214, 255)
                                       : Color.FromArgb(48, 148, 92, 255);
                dc.DrawLine(new Pen(new SolidColorBrush(color), i % 5 == 0 ? 1.5 : 0.8),
                    new Point(cx, cy), new Point(ex, ey));
            }

            for (int i = 1; i <= 13; i++)
            {
                var rx    = i * maxR / 14.0;
                var ry    = rx * 0.48;
                var alpha = (byte)Math.Max(16, 94 - i * 5);
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(alpha, 0, 232, 255)), 1.1),
                    new Point(cx, cy), rx, ry);
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.7), 130, 80, 255)), 0.9),
                    new Point(cx, cy), rx * 0.78, ry * 1.16);
            }

            for (int i = 0; i < 90; i++)
            {
                var x     = (i * 73)  % Math.Max(1, (int)w);
                var y     = (i * 151) % Math.Max(1, (int)h);
                var alpha = (byte)(80 + (i * 17) % 120);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, 205, 242, 255)),
                    null, new Point(x, y), i % 7 == 0 ? 1.4 : 0.8, i % 7 == 0 ? 1.4 : 0.8);
            }

            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, 0, 180, 255)),
                null, new Point(cx, cy), 72, 42);
        }
    }
}
