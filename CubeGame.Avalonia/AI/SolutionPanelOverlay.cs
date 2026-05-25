using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// 솔루션 직접 입력 패널 — 왼쪽 하단에 독립 패널로 표시.
///
/// 레이아웃 (패널 기준):
///   y=  6 : "🔧 솔루션 직접 입력" 제목
///   y= 22 : ────────── [네이티브 TextBox 영역 60px] ──────────
///   y= 86 : [▶  실행 (126×26)]  [✂  지우기 (120×26)]   ← 드로잉 버튼
///   y=116 : 파싱/검증 상태 (2줄)
///   패널 크기: 278 × 148  위치: (10, h−162)
///
/// TextBox, 실행/지우기 버튼은 MainWindow.cs 에서 네이티브 Avalonia 컨트롤로 배치.
/// </summary>
public static class SolutionPanelOverlay
{
    // ── 패널 / 버튼 영역 (창 높이 기준 상대 계산) ───────────────────
    public static Rect  Panel(int h)    => new(10, h - 162, 278, 148);
    public static Rect  RunBtn(int h)   => new(18, h - 076, 126,  26);
    public static Rect  ClearBtn(int h) => new(148, h - 076, 120, 26);

    // TextBox 영역 (네이티브 컨트롤과 픽셀 단위 일치)
    //   HorizontalAlignment=Left, VerticalAlignment=Bottom
    //   Margin = new Thickness(18, 0, 0, 90)  Width=262  Height=60
    public const double TextBoxLeft        = 18;
    public const double TextBoxMarginBottom = 90;
    public const double TextBoxWidth       = 262;
    public const double TextBoxHeight      = 60;

    // 버튼 (네이티브 Avalonia Button)
    //   Run:   Margin = (18, 0, 0, 60),  Width=126, Height=26
    //   Clear: Margin = (148, 0, 0, 60), Width=120, Height=26
    public const double BtnMarginBottom = 60;

    // ── 히트 테스트 ─────────────────────────────────────────────────
    public enum Hit { None, Run, Clear }

    public static Hit TryHit(Point point, int h)
    {
        if (RunBtn(h).Contains(point))   return Hit.Run;
        if (ClearBtn(h).Contains(point)) return Hit.Clear;
        return Hit.None;
    }

    // ── 그리기 ──────────────────────────────────────────────────────
    public static void Draw(DrawingContext dc, int w, int h, SolveController solver)
    {
        var p = Panel(h);

        // 배경 (초록 계열 테두리 → 솔루션 패널 구분)
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(215, 6, 12, 22)),
            new Pen(new SolidColorBrush(Color.FromArgb(180, 60, 190, 80)), 1.5),
            p, 10, 10);

        // 제목 + 표기법 레이블
        DrawSmallText(dc, "🔧 솔루션 직접 입력", p.X + 8, p.Y + 7,
            Color.FromArgb(230, 110, 230, 110), 12f, bold: true);
        DrawSmallText(dc, "싱마스터 표기법", p.Right - 8 - 80, p.Y + 8,
            Color.FromArgb(160, 140, 200, 255), 9.5f);

        // TextBox 구역 배경 힌트 (실제 TextBox는 네이티브 컨트롤)
        var tbRect = new Rect(p.X + 8, p.Y + 23, p.Width - 16, TextBoxHeight);
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(60, 20, 40, 80)),
            new Pen(new SolidColorBrush(Color.FromArgb(80, 70, 130, 220)), 1),
            tbRect, 4, 4);

        // 버튼 (▶ 실행)
        DrawButton(dc, RunBtn(h), "▶  실행",
            Color.FromRgb(20, 110, 55), Color.FromRgb(50, 200, 90), Colors.White);

        // 버튼 (✂ 지우기)
        DrawButton(dc, ClearBtn(h), "✂  지우기",
            Color.FromRgb(50, 30, 30), Color.FromRgb(140, 80, 80),
            Color.FromRgb(210, 170, 170));

        // 상태 메시지
        DrawStatus(dc, p, solver.SolutionStatus);
    }

    // ── 버튼 ────────────────────────────────────────────────────────
    private static void DrawButton(
        DrawingContext dc, Rect r,
        string text, Color fill, Color border, Color textColor)
    {
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.5), r, 6, 6);

        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
            12, new SolidColorBrush(textColor));
        dc.DrawText(ft, new Point(
            r.X + (r.Width  - ft.Width)  / 2,
            r.Y + (r.Height - ft.Height) / 2));
    }

    // ── 상태 텍스트 (파싱 결과 / 검증 결과) ────────────────────────
    private static void DrawStatus(DrawingContext dc, Rect panel, string status)
    {
        if (string.IsNullOrEmpty(status)) return;

        var lines = status.Replace("\r\n", "\n").Split('\n');
        double y = panel.Y + 117;
        for (int i = 0; i < Math.Min(lines.Length, 2); i++)
        {
            var line  = lines[i].Trim();
            if (line.Length == 0) continue;
            bool isOk = line.StartsWith("✅");
            bool isErr = line.StartsWith("❌") || line.StartsWith("⚠️");
            var color = isOk  ? Color.FromArgb(220, 100, 240, 120)
                      : isErr ? Color.FromArgb(220, 255, 130,  80)
                              : Color.FromArgb(200, 190, 210, 250);
            DrawSmallText(dc, Truncate(line, 40), panel.X + 8, y + i * 13, color, 10.5f);
        }
    }

    // ── 텍스트 헬퍼 ─────────────────────────────────────────────────
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

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 1)] + "…";
}
