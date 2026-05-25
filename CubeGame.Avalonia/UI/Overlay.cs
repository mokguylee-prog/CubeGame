using System.Globalization;
using Avalonia;
using Avalonia.Media;
using CubeGame.Avalonia.AI;
using CubeGame.Avalonia.Game;
using CubeGame.Avalonia.Render;
using CubeGame.Avalonia.Scene;
using CubeGame.Avalonia.UI;

namespace CubeGame.Avalonia.UI;

/// <summary>
/// 게임 HUD 렌더링 전담 클래스.
/// AI 버튼/상태는 AiButtonOverlay에 위임합니다.
/// </summary>
public class Overlay
{
    public bool ShowHelp { get; set; }

    // ── 그리기 진입점 ───────────────────────────────────────────────────
    public void Draw(DrawingContext dc, int w, int h,
                     GameState state, string frontFace,
                     SolveController solver)
    {
        DrawSidePanel(dc, w);
        DrawTopBar(dc, state);
        DrawActionButtons(dc, state);
        DrawFrontFace(dc, w, h, frontFace);

        // AI 패널 A(제어)+B(로그)는 AI 전용 모듈에 위임
        AiButtonOverlay.Draw(dc, w, h, solver);

        // 솔루션 직접 입력 패널 (왼쪽 하단)
        SolutionPanelOverlay.Draw(dc, w, h, solver);

        if (ShowHelp) DrawHelp(dc, w, h);
        if (!string.IsNullOrEmpty(state.LastResult))
            DrawResult(dc, w, h, state.LastResult);
        DrawSignature(dc, h);
    }

    // ── 히트 테스트 ─────────────────────────────────────────────────────
    public static bool TryHitModeButton(Point point, out CubeMode mode)
    {
        if (StandardButtonRect.Contains(point)) { mode = CubeMode.Standard; return true; }
        if (CurrentButtonRect.Contains(point))  { mode = CubeMode.Current;  return true; }
        mode = CubeMode.Standard;
        return false;
    }

    public static bool HitSelectButton(Point point)    => SelectButtonRect.Contains(point);
    public static bool HitRotationButton(Point point)  => RotationButtonRect.Contains(point);
    public static bool HitCubeResetButton(Point point) => CubeResetButtonRect.Contains(point);
    public static bool HitScrambleButton(Point point)  => ScrambleButtonRect.Contains(point);
    // AI 패널 히트테스트는 MainWindow에서 AiButtonOverlay.TryHit()으로 직접 처리

    public static bool TryHitLayerButton(Point point, out LayerAxis axis, out int layer)
    {
        if (TopLayerButtonRect.Contains(point))    { axis = LayerAxis.Y; layer =  1; return true; }
        if (MiddleLayerButtonRect.Contains(point)) { axis = LayerAxis.Y; layer =  0; return true; }
        if (BottomLayerButtonRect.Contains(point)) { axis = LayerAxis.Y; layer = -1; return true; }
        if (LeftLayerButtonRect.Contains(point))   { axis = LayerAxis.X; layer = -1; return true; }
        if (CenterLayerButtonRect.Contains(point)) { axis = LayerAxis.X; layer =  0; return true; }
        if (RightLayerButtonRect.Contains(point))  { axis = LayerAxis.X; layer =  1; return true; }
        axis = LayerAxis.Y; layer = 0;
        return false;
    }

    // ── 상단 바 ─────────────────────────────────────────────────────────
    private static void DrawTopBar(DrawingContext dc, GameState state)
    {
        float x = 20, y = 15;
        var white = new SolidColorBrush(Colors.White);
        var gold  = new SolidColorBrush(Colors.Gold);
        var gray  = new SolidColorBrush(Colors.LightGray);
        var cyan  = new SolidColorBrush(Colors.Cyan);
        var font  = new Typeface("Segoe UI");
        var bold  = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);

        DrawText(dc, $"Score: {state.Score}",         bold, 16, white, x, y); y += 22;
        DrawText(dc, $"Match: {state.TargetFace}",    bold, 18, gold,  x, y); y += 24;
        DrawText(dc, $"Accuracy: {state.AccuracyText}", font, 12, gray, x, y); y += 16;
        DrawText(dc, state.ComboText,                 bold, 14, cyan,  x, y);
    }

    // ── 조작 버튼들 ─────────────────────────────────────────────────────
    private static void DrawActionButtons(DrawingContext dc, GameState state)
    {
        DrawButton(dc, StandardButtonRect,  "Standard cube",
            state.Mode == CubeMode.Standard);
        DrawButton(dc, CurrentButtonRect,   "Current cube",
            state.Mode == CubeMode.Current);
        DrawButton(dc, SelectButtonRect,    "Select",       false);
        DrawButton(dc, RotationButtonRect,
            state.AutoRotate ? "Rotation: On" : "Rotation: Off", state.AutoRotate);
        DrawButton(dc, CubeResetButtonRect, "Reset cube",   false);
        DrawScrambleButton(dc);
        DrawLayerButtons(dc);
    }

    private static void DrawScrambleButton(DrawingContext dc)
    {
        var rect   = ScrambleButtonRect;
        var fill   = Color.FromRgb(170, 110, 0);     // 황금색
        var border = Color.FromRgb(255, 195, 30);
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.8), rect, 6, 6);

        var ft = new FormattedText("🎲 RANDOM  (20회)", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            13, new SolidColorBrush(Colors.White));
        dc.DrawText(ft, new Point(
            rect.X + (rect.Width  - ft.Width)  / 2,
            rect.Y + (rect.Height - ft.Height) / 2));
    }

    // ── 기타 HUD 요소 ───────────────────────────────────────────────────
    private static void DrawFrontFace(DrawingContext dc, int w, int h, string face)
    {
        DrawText(dc, $"Front: {face}", new Typeface("Segoe UI"), 12,
            new SolidColorBrush(Colors.LightGray),
            CubeRenderer.GetCubeCenterX(w) - 45,
            CubeRenderer.GetCubeCenterY(h) + 132);
    }

    private static void DrawSidePanel(DrawingContext dc, int w)
    {
        float x = w - 170, y = 20;
        var white = new SolidColorBrush(Colors.White);
        var gray  = new SolidColorBrush(Colors.LightGray);
        var bold  = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var font  = new Typeface("Segoe UI");

        DrawText(dc, "Controls", bold, 13, white, x, y); y += 22;
        string[] lines =
        [
            "Arrow Keys - View", "U/I/O - Top/Mid/Bottom",
            "J/K/L - Left/Mid/Right", "Shift + move - Reverse",
            "T - Toggle rotation",   "1/2 - Cube mode",
            "R - Reset score",       "H - Toggle help", "ESC - Exit"
        ];
        foreach (var line in lines) { DrawText(dc, line, font, 11, gray, x, y); y += 18; }
    }

    private static void DrawHelp(DrawingContext dc, int w, int h)
    {
        var text = "HOW TO PLAY\n\n" +
                   "Rotate the cube using Arrow Keys.\n" +
                   "Match the target face (top-left).\n" +
                   "Press SPACE to confirm.\n\n" +
                   "3+ combo = +20 bonus\n" +
                   "5+ combo = +50 bonus";
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12,
            new SolidColorBrush(Colors.White));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 20, 20, 30)),
            null, new Rect(15, h - 200, 330, 170), 8, 8);
        dc.DrawText(ft, new Point(28, h - 190));
    }

    private static void DrawResult(DrawingContext dc, int w, int h, string result)
    {
        var color = result == "Match!" ? Colors.Lime : Colors.Red;
        var ft = new FormattedText(result, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            24, new SolidColorBrush(color));
        dc.DrawText(ft, new Point(w / 2f + 40 - ft.Width / 2, h / 2f - 100));
    }

    private static void DrawSignature(DrawingContext dc, int h)
    {
        var brush  = new SolidColorBrush(Color.FromArgb(220, 245, 245, 250));
        var shadow = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
        var font   = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        const string text = "이다안 큐브 by 월평동 이상목";
        float x = 20, y = h - 48;
        DrawText(dc, text, font, 24, shadow, x + 2, y + 2);
        DrawText(dc, text, font, 24, brush,  x,     y);
    }

    // ── 레이어 버튼 ─────────────────────────────────────────────────────
    private static void DrawLayerButtons(DrawingContext dc)
    {
        DrawText(dc, "Layer turns",
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            12, new SolidColorBrush(Colors.White), 20, 318);
        DrawButton(dc, TopLayerButtonRect,    "Top",    false);
        DrawButton(dc, MiddleLayerButtonRect, "Middle", false);
        DrawButton(dc, BottomLayerButtonRect, "Bottom", false);
        DrawButton(dc, LeftLayerButtonRect,   "Left",   false);
        DrawButton(dc, CenterLayerButtonRect, "Center", false);
        DrawButton(dc, RightLayerButtonRect,  "Right",  false);
    }

    // ── 공통 그리기 헬퍼 ────────────────────────────────────────────────
    private static void DrawButton(DrawingContext dc, Rect rect, string text, bool selected)
    {
        var fillColor   = selected ? Color.FromRgb(55, 115, 190) : Color.FromRgb(42, 45, 58);
        var borderColor = selected ? Colors.White : Color.FromRgb(105, 110, 126);
        dc.DrawRectangle(new SolidColorBrush(fillColor),
            new Pen(new SolidColorBrush(borderColor), selected ? 2 : 1), rect, 6, 6);

        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            13, new SolidColorBrush(Colors.White));
        dc.DrawText(ft, new Point(
            rect.X + (rect.Width  - ft.Width)  / 2,
            rect.Y + (rect.Height - ft.Height) / 2));
    }

    private static void DrawText(DrawingContext dc, string text, Typeface font,
                                  float size, IBrush brush, float x, float y)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, font, size, brush);
        dc.DrawText(ft, new Point(x, y));
    }

    // ── 버튼 위치 상수 ──────────────────────────────────────────────────
    private static Rect StandardButtonRect  => new(20,  122, 126, 32);
    private static Rect CurrentButtonRect   => new(154, 122, 120, 32);
    private static Rect SelectButtonRect    => new(20,  162, 254, 34);
    private static Rect RotationButtonRect  => new(20,  204, 254, 34);
    private static Rect CubeResetButtonRect  => new(20,  246, 254, 34);
    private static Rect ScrambleButtonRect   => new(20,  284, 254, 28);  // RANDOM
    private static Rect TopLayerButtonRect    => new(20,  338, 74, 28);  // ↓ 24px
    private static Rect MiddleLayerButtonRect => new(20,  370, 74, 28);
    private static Rect BottomLayerButtonRect => new(20,  402, 74, 28);
    private static Rect LeftLayerButtonRect   => new(102, 370, 66, 28);
    private static Rect CenterLayerButtonRect => new(174, 370, 74, 28);
    private static Rect RightLayerButtonRect  => new(254, 370, 66, 28);
}
