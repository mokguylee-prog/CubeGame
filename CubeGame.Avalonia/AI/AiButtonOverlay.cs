using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// 우하단 AI 패널 렌더링 + 히트 테스트.
///
/// 패널 레이아웃 (패널 좌상단 기준 상대 좌표):
///   y=  8 : [🤖 AI 풀기 버튼]  [⛔ 중지 버튼]
///   y= 52 : [☑] 기록 역재생으로 풀기
///   y= 76 : ─── 구분선 ───
///   y= 80 : 📤 요청:
///   y= 94 : 요청 텍스트 (최대 2줄)
///   y=122 : ─── 구분선 ───
///   y=126 : 📥 응답:
///   y=140 : 응답 텍스트 (최대 2줄)
///   패널 높이 = 176
/// </summary>
public static class AiButtonOverlay
{
    // ── 히트 결과 열거형 ─────────────────────────────────────────────
    public enum Hit { None, AiButton, StopButton, Checkbox }

    // ── 패널/버튼 영역 계산 ─────────────────────────────────────────
    private static Rect Panel(int w, int h)      => new(w - 296, h - 194, 284, 176);
    private static Rect AiBtn(int w, int h)      => new(Panel(w, h).X +   8, Panel(w, h).Y +  8, 126, 36);
    private static Rect StopBtn(int w, int h)    => new(Panel(w, h).X + 142, Panel(w, h).Y +  8, 134, 36);
    private static Rect CheckboxBox(int w, int h) => new(Panel(w, h).X +   8, Panel(w, h).Y + 52,  18, 18);

    // ── 히트 테스트 ─────────────────────────────────────────────────
    public static Hit TryHit(Point point, int w, int h)
    {
        if (AiBtn(w, h).Contains(point))      return Hit.AiButton;
        if (StopBtn(w, h).Contains(point))    return Hit.StopButton;
        if (CheckboxBox(w, h).Contains(point)) return Hit.Checkbox;
        return Hit.None;
    }

    // ── 전체 패널 렌더링 ────────────────────────────────────────────
    public static void Draw(DrawingContext dc, int w, int h, SolveController solver)
    {
        var p = Panel(w, h);

        DrawPanelBackground(dc, p);
        DrawAiButton(dc,   AiBtn(w, h),      solver.IsRunning);
        DrawStopButton(dc, StopBtn(w, h),    solver.IsRunning);
        DrawCheckbox(dc,   CheckboxBox(w, h), solver.UseHistoryMode, p);
        DrawDivider(dc, p, relY: 76);
        DrawLogSection(dc, p, "📤 요청:", solver.LastRequest,  relY: 80);
        DrawDivider(dc, p, relY: 122);
        DrawLogSection(dc, p, "📥 응답:", solver.LastResponse, relY: 126);
    }

    // ── 패널 배경 ───────────────────────────────────────────────────
    private static void DrawPanelBackground(DrawingContext dc, Rect p)
    {
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(210, 12, 14, 26)),
            new Pen(new SolidColorBrush(Color.FromArgb(180, 60, 100, 200)), 1.5),
            p, 10, 10);
    }

    // ── AI 풀기 버튼 ────────────────────────────────────────────────
    private static void DrawAiButton(DrawingContext dc, Rect r, bool running)
    {
        var fill   = running ? Color.FromRgb(20, 130, 75) : Color.FromRgb(28, 75, 165);
        var border = running ? Colors.LimeGreen            : Color.FromRgb(70, 150, 255);
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.8), r, 8, 8);
        var label = running ? "⏳ AI 풀이 중..." : "🤖 AI 풀기";
        DrawCenteredText(dc, r, label, 14);
    }

    // ── 중지 버튼 ───────────────────────────────────────────────────
    private static void DrawStopButton(DrawingContext dc, Rect r, bool running)
    {
        // 활성 = 빨강, 비활성 = 어두운 회색
        var fill   = running ? Color.FromRgb(170, 30, 30) : Color.FromRgb(40, 40, 50);
        var border = running ? Colors.OrangeRed             : Color.FromRgb(80, 80, 90);
        var text   = running ? "⛔ 풀이 중지" : "⛔ 중지";
        var textColor = running ? Colors.White : Color.FromRgb(120, 120, 130);

        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.5), r, 8, 8);
        DrawCenteredText(dc, r, text, 14, textColor);
    }

    // ── 체크박스 + 레이블 ───────────────────────────────────────────
    private static void DrawCheckbox(DrawingContext dc, Rect box, bool isChecked, Rect panel)
    {
        // 박스 배경
        var fill   = isChecked ? Color.FromRgb(28, 95, 180) : Color.FromRgb(30, 32, 45);
        var border = isChecked ? Colors.CornflowerBlue       : Color.FromRgb(90, 95, 115);
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.5), box, 3, 3);

        // 체크 표시
        if (isChecked)
        {
            var tick = new Pen(new SolidColorBrush(Colors.White), 2.2) { LineCap = PenLineCap.Round };
            dc.DrawLine(tick, new Point(box.X + 3, box.Y + 9),  new Point(box.X + 8,  box.Y + 14));
            dc.DrawLine(tick, new Point(box.X + 8, box.Y + 14), new Point(box.X + 15, box.Y + 4));
        }

        // 레이블
        var modeText = isChecked ? "기록 역재생으로 풀기" : "AI에게 직접 풀이 요청";
        var labelColor = isChecked
            ? Color.FromArgb(230, 180, 210, 255)
            : Color.FromArgb(200, 130, 170, 255);
        DrawSmallText(dc, modeText, panel.X + 32, box.Y + 1, labelColor, 11.5f);
    }

    // ── 구분선 ──────────────────────────────────────────────────────
    private static void DrawDivider(DrawingContext dc, Rect panel, double relY)
    {
        var y = panel.Y + relY;
        dc.DrawLine(
            new Pen(new SolidColorBrush(Color.FromArgb(100, 80, 110, 180)), 1),
            new Point(panel.X + 8, y), new Point(panel.Right - 8, y));
    }

    // ── 요청/응답 섹션 ──────────────────────────────────────────────
    private static void DrawLogSection(
        DrawingContext dc, Rect panel, string header, string text, double relY)
    {
        // 섹션 헤더 ("📤 요청:" / "📥 응답:")
        DrawSmallText(dc, header,
            panel.X + 8, panel.Y + relY,
            Color.FromArgb(220, 120, 190, 255), 11f, bold: true);

        // 본문 (최대 2줄, 각 줄 최대 ~34자)
        if (string.IsNullOrEmpty(text)) return;
        var lines = WrapText(text, 34);
        for (int i = 0; i < Math.Min(lines.Length, 2); i++)
        {
            DrawSmallText(dc, lines[i],
                panel.X + 10, panel.Y + relY + 14 + i * 13,
                Color.FromArgb(200, 200, 215, 240), 10.5f);
        }
    }

    // ── 공통 텍스트 헬퍼 ────────────────────────────────────────────
    private static void DrawCenteredText(
        DrawingContext dc, Rect rect, string text, float size,
        Color? color = null)
    {
        var brush = new SolidColorBrush(color ?? Colors.White);
        var font  = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
        var ft    = new FormattedText(text, CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, font, size, brush);
        dc.DrawText(ft, new Point(
            rect.X + (rect.Width  - ft.Width)  / 2,
            rect.Y + (rect.Height - ft.Height) / 2));
    }

    private static void DrawSmallText(
        DrawingContext dc, string text, double x, double y,
        Color color, float size = 11f, bool bold = false)
    {
        var weight = bold ? FontWeight.SemiBold : FontWeight.Normal;
        var font   = new Typeface("Segoe UI", FontStyle.Normal, weight);
        var ft     = new FormattedText(text, CultureInfo.CurrentCulture,
                         FlowDirection.LeftToRight, font, size,
                         new SolidColorBrush(color));
        dc.DrawText(ft, new Point(x, y));
    }

    // ── 텍스트 줄 나누기 (단어/문자 경계) ───────────────────────────
    private static string[] WrapText(string text, int maxChars)
    {
        // 개행 정규화
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

        // 첫 번째 개행이 있으면 그 위치로 분리
        var newlineIdx = text.IndexOf('\n');
        if (newlineIdx >= 0 && newlineIdx <= maxChars)
        {
            var first  = text[..newlineIdx].Trim();
            var second = text[(newlineIdx + 1)..].Replace('\n', ' ').Trim();
            return [first, Truncate(second, maxChars)];
        }

        if (text.Length <= maxChars) return [text];

        // maxChars 이내 마지막 공백에서 분리
        var cut = text.LastIndexOf(' ', maxChars - 1);
        if (cut < 0) cut = maxChars;
        return [text[..cut].TrimEnd(), Truncate(text[cut..].TrimStart(), maxChars)];
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 1)] + "…";
}
