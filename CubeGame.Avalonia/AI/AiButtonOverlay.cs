using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// 우하단 AI 버튼의 렌더링과 히트 테스트를 담당합니다.
/// Overlay.cs에서 분리된 AI 전용 UI 코드입니다.
/// </summary>
public static class AiButtonOverlay
{
    // ────────────────────────────────────────────────────────────────────
    // 버튼 영역 (창 크기 기준 우하단 고정)
    // ────────────────────────────────────────────────────────────────────
    public static Rect ButtonRect(int w, int h) => new(w - 180, h - 75, 160, 50);

    // ────────────────────────────────────────────────────────────────────
    // 히트 테스트
    // ────────────────────────────────────────────────────────────────────
    public static bool HitTest(Point point, int w, int h) => ButtonRect(w, h).Contains(point);

    // ────────────────────────────────────────────────────────────────────
    // 버튼 + 상태 메시지 렌더링
    // ────────────────────────────────────────────────────────────────────
    public static void Draw(DrawingContext dc, int w, int h, bool solving, string status)
    {
        DrawButton(dc, ButtonRect(w, h), solving);
        if (!string.IsNullOrEmpty(status))
            DrawStatus(dc, w, h, status);
    }

    // ────────────────────────────────────────────────────────────────────
    // 내부: 버튼 본체
    // ────────────────────────────────────────────────────────────────────
    private static void DrawButton(DrawingContext dc, Rect rect, bool solving)
    {
        // 배경: 대기 = 파랑, 풀이 중 = 초록
        var fillColor  = solving ? Color.FromRgb(20, 140, 80)   : Color.FromRgb(30, 80, 170);
        var borderColor = solving ? Colors.LimeGreen              : Color.FromRgb(80, 160, 255);

        dc.DrawRectangle(
            new SolidColorBrush(fillColor),
            new Pen(new SolidColorBrush(borderColor), 2),
            rect, 10, 10);

        var label = solving ? "⏳ AI 풀이 중..." : "🤖 AI 풀기";
        var font  = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var ft    = new FormattedText(label, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, font, 15,
                        new SolidColorBrush(Colors.White));

        dc.DrawText(ft, new Point(
            rect.X + (rect.Width  - ft.Width)  / 2,
            rect.Y + (rect.Height - ft.Height) / 2));
    }

    // ────────────────────────────────────────────────────────────────────
    // 내부: 상태 메시지 (버튼 위, 우측 정렬)
    // ────────────────────────────────────────────────────────────────────
    private static void DrawStatus(DrawingContext dc, int w, int h, string status)
    {
        var font = new Typeface("Segoe UI");
        var ft   = new FormattedText(status, CultureInfo.CurrentCulture,
                       FlowDirection.LeftToRight, font, 13,
                       new SolidColorBrush(Color.FromArgb(230, 100, 220, 255)));

        double x = w - ft.Width - 20;
        double y = h - 110;

        // 반투명 배경
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(160, 10, 10, 30)),
            null,
            new Rect(x - 8, y - 4, ft.Width + 16, ft.Height + 8),
            6, 6);

        dc.DrawText(ft, new Point(x, y));
    }
}
