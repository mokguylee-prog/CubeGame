using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// 우하단 AI 패널 — 두 패널로 분리:
///
/// ┌─────────────────────────────────┐  ← Panel B: AI 로그 (요청/응답)
/// │ 📤 요청:                         │  Height: 200px, 요청 3줄 + 응답 6줄
/// │ (cube state text...)             │
/// │ ─────────────────────────────── │
/// │ 📥 응답:                         │
/// │ (pipeline progress / result...)  │
/// └─────────────────────────────────┘
///   [8px gap]
/// ┌─────────────────────────────────┐  ← Panel A: AI 제어 (버튼+체크+단계표시)
/// │ [🤖 AI 풀기]  [⛔ 풀이 중지]    │  Height: 120px
/// │ [☑] 기록 역재생으로 풀기         │
/// │ ─── 단계 표시바 (AI모드) ───    │
/// └─────────────────────────────────┘
/// </summary>
public static class AiButtonOverlay
{
    // ── 히트 결과 ────────────────────────────────────────────────────
    public enum Hit { None, AiButton, StopButton, Checkbox, CopyRequest, CopyResponse }

    // ── 패널 좌표 ────────────────────────────────────────────────────
    /// Panel A: AI 제어 (버튼+체크박스+단계표시)
    private static Rect PanelA(int w, int h) => new(w - 268, h - 176, 256, 122);
    /// Panel B: AI 로그 (요청+응답)
    private static Rect PanelB(int w, int h) => new(w - 268, h - 386, 256, 202);

    // Panel A 내 요소
    private static Rect AiBtn(int w, int h)       => new(PanelA(w,h).X +   6, PanelA(w,h).Y +  6, 116, 34);
    private static Rect StopBtn(int w, int h)     => new(PanelA(w,h).X + 126, PanelA(w,h).Y +  6, 126, 34);
    private static Rect CheckboxBox(int w, int h) => new(PanelA(w,h).X +   6, PanelA(w,h).Y + 48,  18, 18);

    // Panel B 내 복사 버튼 (헤더 우측 끝에 배치)
    private static Rect CopyReqBtn(int w, int h)  => new(PanelB(w,h).Right - 42, PanelB(w,h).Y +  5, 36, 18);
    private static Rect CopyRespBtn(int w, int h) => new(PanelB(w,h).Right - 42, PanelB(w,h).Y + 71, 36, 18);

    // ── 히트 테스트 ─────────────────────────────────────────────────
    public static Hit TryHit(Point point, int w, int h)
    {
        if (AiBtn(w, h).Contains(point))        return Hit.AiButton;
        if (StopBtn(w, h).Contains(point))      return Hit.StopButton;
        if (CheckboxBox(w, h).Contains(point))  return Hit.Checkbox;
        if (CopyReqBtn(w, h).Contains(point))   return Hit.CopyRequest;
        if (CopyRespBtn(w, h).Contains(point))  return Hit.CopyResponse;
        return Hit.None;
    }

    // ── 전체 그리기 진입점 ──────────────────────────────────────────
    public static void Draw(DrawingContext dc, int w, int h, SolveController solver)
    {
        DrawPanelA(dc, w, h, solver);
        DrawPanelB(dc, w, h, solver);
    }

    // ════════════════════════════════════════════════════════════════
    // Panel A: AI 제어 — 버튼, 체크박스, 단계 표시바
    // ════════════════════════════════════════════════════════════════
    private static void DrawPanelA(DrawingContext dc, int w, int h, SolveController solver)
    {
        var p = PanelA(w, h);
        DrawPanelBg(dc, p, Color.FromArgb(215, 10, 12, 24), Color.FromArgb(180, 60, 100, 200));

        DrawAiButton(dc,   AiBtn(w, h),       solver.IsRunning);
        DrawStopButton(dc, StopBtn(w, h),     solver.IsRunning);
        DrawCheckbox(dc,   CheckboxBox(w, h), solver.UseHistoryMode, p);

        // 단계 표시바 (AI 직접 모드일 때)
        if (!solver.UseHistoryMode)
        {
            DrawDivider(dc, p, relY: 68);
            DrawPipelineSteps(dc, p, solver.Step, solver.Attempt, relY: 74);
        }
    }

    // ── AI 풀기 버튼 ────────────────────────────────────────────────
    private static void DrawAiButton(DrawingContext dc, Rect r, bool running)
    {
        var fill   = running ? Color.FromRgb(20, 130, 75)  : Color.FromRgb(28, 75, 165);
        var border = running ? Colors.LimeGreen              : Color.FromRgb(70, 150, 255);
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.8), r, 8, 8);
        DrawCenteredText(dc, r, running ? "⏳ AI 풀이 중..." : "🤖 AI 풀기", 13.5f);
    }

    // ── 중지 버튼 ───────────────────────────────────────────────────
    private static void DrawStopButton(DrawingContext dc, Rect r, bool running)
    {
        var fill      = running ? Color.FromRgb(170, 30, 30) : Color.FromRgb(40, 40, 50);
        var border    = running ? Colors.OrangeRed             : Color.FromRgb(80, 80, 90);
        var text      = running ? "⛔ 풀이 중지" : "⛔ 중지";
        var textColor = running ? Colors.White : Color.FromRgb(120, 120, 130);
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.5), r, 8, 8);
        DrawCenteredText(dc, r, text, 13.5f, textColor);
    }

    // ── 체크박스 ────────────────────────────────────────────────────
    private static void DrawCheckbox(DrawingContext dc, Rect box, bool isChecked, Rect panel)
    {
        var fill   = isChecked ? Color.FromRgb(28, 95, 180) : Color.FromRgb(30, 32, 45);
        var border = isChecked ? Colors.CornflowerBlue       : Color.FromRgb(90, 95, 115);
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.5), box, 3, 3);

        if (isChecked)
        {
            var tick = new Pen(new SolidColorBrush(Colors.White), 2.2) { LineCap = PenLineCap.Round };
            dc.DrawLine(tick, new Point(box.X + 3, box.Y + 9),  new Point(box.X + 8,  box.Y + 14));
            dc.DrawLine(tick, new Point(box.X + 8, box.Y + 14), new Point(box.X + 15, box.Y + 4));
        }

        var modeText   = isChecked ? "기록 역재생으로 풀기" : "AI에게 직접 풀이 요청";
        var labelColor = isChecked
            ? Color.FromArgb(230, 180, 210, 255)
            : Color.FromArgb(200, 130, 170, 255);
        DrawSmallText(dc, modeText, panel.X + 30, box.Y + 1, labelColor, 11.5f);
    }

    // ── 파이프라인 단계 표시바 ──────────────────────────────────────
    private static void DrawPipelineSteps(
        DrawingContext dc, Rect panel, PipelineStep currentStep, int attempt, double relY)
    {
        double x0    = panel.X + 6;
        double y0    = panel.Y + relY;
        double stepW = (panel.Width - 12) / 4.0;

        var steps = new[]
        {
            ("1.분석",  PipelineStep.Analysis),
            ("2.수식",  PipelineStep.MovesGen),
            ("3.검증",  PipelineStep.Verifying),
            ("4.실행",  PipelineStep.Executing),
        };

        for (int i = 0; i < steps.Length; i++)
        {
            var (label, step) = steps[i];
            double cx = x0 + stepW * i + stepW * 0.5;

            bool isDone   = IsStepDone(step, currentStep);
            bool isActive = step == currentStep;
            bool isFailed = currentStep == PipelineStep.Failed && isActive;

            Color circleColor, textColor;
            if (isFailed)
            { circleColor = Color.FromRgb(200, 40, 40); textColor = Color.FromRgb(255, 120, 120); }
            else if (isActive)
            { circleColor = Color.FromRgb(255, 185, 0); textColor = Color.FromRgb(255, 235, 80); }
            else if (isDone)
            { circleColor = Color.FromRgb(30, 185, 90);  textColor = Color.FromRgb(100, 240, 140); }
            else
            { circleColor = Color.FromRgb(45, 50, 70);   textColor = Color.FromRgb(100, 110, 140); }

            // 연결선
            if (i < steps.Length - 1)
            {
                double lx0 = cx + 10;
                double lx1 = x0 + stepW * (i + 1) + stepW * 0.5 - 10;
                dc.DrawLine(
                    new Pen(new SolidColorBrush(isDone
                        ? Color.FromArgb(150, 30, 185, 90)
                        : Color.FromArgb(70, 80, 90, 120)), 1.5),
                    new Point(lx0, y0 + 7), new Point(lx1, y0 + 7));
            }

            // 원
            dc.DrawEllipse(
                new SolidColorBrush(circleColor),
                new Pen(new SolidColorBrush(Color.FromArgb(isActive ? (byte)220 : (byte)120, 255, 255, 255)),
                        isActive ? 1.5 : 0.8),
                new Point(cx, y0 + 7), 8, 8);

            // 완료 체크
            if (isDone && currentStep != PipelineStep.Failed)
            {
                var pen = new Pen(new SolidColorBrush(Colors.White), 1.8) { LineCap = PenLineCap.Round };
                dc.DrawLine(pen, new Point(cx - 4, y0 + 7), new Point(cx - 1, y0 + 11));
                dc.DrawLine(pen, new Point(cx - 1, y0 + 11), new Point(cx + 5, y0 + 3));
            }

            // 레이블
            DrawSmallText(dc, label, cx - 13, y0 + 18, textColor, 9f);

            // 재시도 번호
            if (isActive && step == PipelineStep.MovesGen && attempt > 0)
                DrawSmallText(dc, $"({attempt}/{SolveController.MaxAttempts})",
                    cx - 11, y0 + 29, Color.FromArgb(200, 255, 210, 80), 8f);
        }
    }

    private static bool IsStepDone(PipelineStep step, PipelineStep current)
    {
        static int Idx(PipelineStep s) => s switch
        {
            PipelineStep.Analysis  => 0,
            PipelineStep.MovesGen  => 1,
            PipelineStep.Verifying => 2,
            PipelineStep.Executing => 3,
            PipelineStep.Done      => 4,
            PipelineStep.Failed    => 4,
            _                      => -1
        };
        return Idx(step) < Idx(current);
    }

    // ════════════════════════════════════════════════════════════════
    // Panel B: AI 로그 — 요청 + 응답 (각각 더 많은 줄)
    // ════════════════════════════════════════════════════════════════
    private static void DrawPanelB(DrawingContext dc, int w, int h, SolveController solver)
    {
        var p = PanelB(w, h);
        DrawPanelBg(dc, p, Color.FromArgb(200, 8, 11, 22), Color.FromArgb(140, 50, 80, 170));

        // ── 📤 요청 (3줄) + 복사 버튼 ──────────────────────────────
        DrawSmallText(dc, "📤 요청:", p.X + 8, p.Y + 8,
            Color.FromArgb(220, 120, 190, 255), 11f, bold: true);
        DrawCopyBtn(dc, CopyReqBtn(w, h), !string.IsNullOrEmpty(solver.LastRequest));
        DrawLogLines(dc, p, solver.LastRequest, relY: 24, maxLines: 3);

        DrawDivider(dc, p, relY: 68);

        // ── 📥 응답 (6줄) + 복사 버튼 ──────────────────────────────
        DrawSmallText(dc, "📥 응답:", p.X + 8, p.Y + 74,
            Color.FromArgb(220, 120, 190, 255), 11f, bold: true);
        DrawCopyBtn(dc, CopyRespBtn(w, h), !string.IsNullOrEmpty(solver.LastResponse));
        DrawLogLines(dc, p, solver.LastResponse, relY: 90, maxLines: 6);
    }

    // ── 📋 복사 버튼 ────────────────────────────────────────────────
    private static void DrawCopyBtn(DrawingContext dc, Rect r, bool hasContent)
    {
        // 내용이 없으면 흐리게, 있으면 밝게
        var fillAlpha   = (byte)(hasContent ? 140 : 50);
        var borderAlpha = (byte)(hasContent ? 180 : 70);
        var textAlpha   = (byte)(hasContent ? 230 : 100);

        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(fillAlpha, 40, 80, 140)),
            new Pen(new SolidColorBrush(Color.FromArgb(borderAlpha, 100, 160, 240)), 1),
            r, 4, 4);

        var ft = new FormattedText("📋 복사", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
            9, new SolidColorBrush(Color.FromArgb(textAlpha, 180, 220, 255)));
        dc.DrawText(ft, new Point(
            r.X + (r.Width  - ft.Width)  / 2,
            r.Y + (r.Height - ft.Height) / 2));
    }

    // ── 공통: 여러 줄 텍스트 그리기 ────────────────────────────────
    private static void DrawLogLines(
        DrawingContext dc, Rect panel, string text, double relY, int maxLines)
    {
        if (string.IsNullOrEmpty(text)) return;
        var lines = WrapText(text, 38);
        for (int i = 0; i < Math.Min(lines.Length, maxLines); i++)
        {
            DrawSmallText(dc, lines[i],
                panel.X + 10, panel.Y + relY + i * 13,
                Color.FromArgb(210, 200, 215, 240), 10.5f);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 공통 그리기 헬퍼
    // ════════════════════════════════════════════════════════════════
    private static void DrawPanelBg(DrawingContext dc, Rect p, Color fill, Color border)
    {
        dc.DrawRectangle(new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(border), 1.5), p, 10, 10);
    }

    private static void DrawDivider(DrawingContext dc, Rect panel, double relY)
    {
        var y = panel.Y + relY;
        dc.DrawLine(
            new Pen(new SolidColorBrush(Color.FromArgb(100, 80, 110, 180)), 1),
            new Point(panel.X + 8, y), new Point(panel.Right - 8, y));
    }

    private static void DrawCenteredText(
        DrawingContext dc, Rect rect, string text, float size, Color? color = null)
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

    // ── 텍스트 줄 나누기 (개행 → 최대 38자) ────────────────────────
    private static string[] WrapText(string text, int maxChars)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var rawLines = text.Split('\n');
        var result   = new System.Collections.Generic.List<string>();

        foreach (var raw in rawLines)
        {
            var line = raw.Trim();
            if (line.Length == 0)        { result.Add(""); continue; }
            if (line.Length <= maxChars) { result.Add(line); continue; }

            int pos = 0;
            while (pos < line.Length)
            {
                int end = Math.Min(pos + maxChars, line.Length);
                if (end < line.Length)
                {
                    int sp = line.LastIndexOf(' ', end - 1, end - pos);
                    if (sp > pos) end = sp + 1;
                }
                result.Add(line[pos..end].TrimEnd());
                pos = end;
                while (pos < line.Length && line[pos] == ' ') pos++;
            }
        }
        return [.. result];
    }
}
