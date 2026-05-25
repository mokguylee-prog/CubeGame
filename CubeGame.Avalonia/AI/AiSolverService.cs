using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CubeGame.Avalonia.AI;

/// <summary>AI 요청·응답을 묶어 반환하는 레코드</summary>
public record AiResult(string Prompt, string Response);

/// <summary>
/// OpenRouter 무료 모델 호출 서비스.
/// 환경변수 OPENROUTER_API_KEY (또는 OPENAI_API_KEY) 로 키를 지정하세요.
/// 키가 없으면 로컬 메시지로 대체됩니다.
/// </summary>
public static class AiSolverService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static readonly string[] FreeModels =
    [
        "google/gemma-3-27b-it:free",
        "meta-llama/llama-3.2-11b-vision-instruct:free",
        "mistralai/mistral-7b-instruct:free"
    ];

    // ──────────────────────────────────────────────────────────────────
    // 기록 역재생 모드: 이동 수를 AI에 전달해 한 줄 코멘트를 받음
    // ──────────────────────────────────────────────────────────────────
    public static async Task<AiResult> GetHistoryCommentAsync(int moveCount, CancellationToken ct)
    {
        var prompt =
            $"루빅스 큐브가 {moveCount}번 조작되어 섞였습니다. " +
            $"역방향으로 {moveCount}번 이동해 원래 상태로 복원합니다. " +
            "이 상황을 한 문장으로 짧게 설명해주세요 (한국어).";

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AiResult(prompt, GetLocalComment(moveCount));

        try
        {
            var response = await CallAsync(apiKey, prompt, maxTokens: 100, ct);
            return new AiResult(prompt, response);
        }
        catch
        {
            return new AiResult(prompt, GetLocalComment(moveCount));
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // AI 직접 풀이 모드: 큐브 상태 전체를 AI에 전달해 풀이 조언을 받음
    // ──────────────────────────────────────────────────────────────────
    public static async Task<AiResult> GetAiSolutionAsync(string cubeState, CancellationToken ct)
    {
        var prompt =
            $"루빅스 큐브 현재 상태 (U=상/D=하/F=앞/B=뒤/L=좌/R=우, " +
            $"W=흰·Y=노·G=초·B=파·O=주·R=빨):\n{cubeState}\n\n" +
            "이 큐브 상태를 분석하고, 풀이 전략을 한국어로 3줄 이내로 설명해주세요.";

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AiResult(
                prompt,
                "⚠️ API 키 없음. OPENROUTER_API_KEY 환경변수를 설정하면\n" +
                "   무료 AI 모델에서 풀이 조언을 받을 수 있습니다.");

        try
        {
            var response = await CallAsync(apiKey, prompt, maxTokens: 220, ct);
            return new AiResult(prompt, response);
        }
        catch (OperationCanceledException)
        {
            return new AiResult(prompt, "⛔ 요청이 취소됐습니다.");
        }
        catch (Exception ex)
        {
            return new AiResult(prompt, $"❌ AI 연결 실패: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ──────────────────────────────────────────────────────────────────
    private static async Task<string> CallAsync(
        string apiKey, string prompt, int maxTokens, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = FreeModels[0],
            max_tokens = maxTokens,
            messages = new[] { new { role = "user", content = prompt } }
        });

        var req = new HttpRequestMessage(
            HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Headers.Add("HTTP-Referer", "https://github.com/cubegame");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var resp = await Http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()?.Trim() ?? "(응답 없음)";
    }

    private static string? GetApiKey()
        => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
        ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static string GetLocalComment(int n) => n switch
    {
        0    => "✅ 큐브가 이미 완성 상태입니다!",
        <= 5  => $"🤖 {n}번 이동으로 복원합니다.",
        <= 15 => $"🤖 최적 경로 계산 완료 — {n}단계 복원.",
        _    => $"🤖 복잡한 상태 분석 완료 — {n}번 역방향 복원."
    };
}
