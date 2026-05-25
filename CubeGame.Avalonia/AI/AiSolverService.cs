using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CubeGame.Avalonia.AI;

/// <summary>
/// OpenRouter 무료 AI 모델을 이용해 큐브 풀이 설명을 생성합니다.
/// API Key 없이도 동작하며, 있으면 AI 설명 메시지를 표시합니다.
/// 환경변수 OPENROUTER_API_KEY 또는 OPENAI_API_KEY 로 키를 지정하세요.
/// </summary>
public static class AiSolverService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // OpenRouter 무료 모델 목록 (우선순위 순)
    private static readonly string[] FreeModels =
    [
        "google/gemma-3-27b-it:free",
        "meta-llama/llama-3.2-11b-vision-instruct:free",
        "mistralai/mistral-7b-instruct:free"
    ];

    public static async Task<string> GetSolvingCommentAsync(int movesCount, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            return GetLocalComment(movesCount);

        try
        {
            return await CallOpenRouterAsync(apiKey, movesCount, ct);
        }
        catch
        {
            return GetLocalComment(movesCount);
        }
    }

    private static async Task<string> CallOpenRouterAsync(string apiKey, int movesCount, CancellationToken ct)
    {
        var prompt = movesCount == 0
            ? "큐브가 이미 풀려 있습니다. 한 문장으로 짧게 말해주세요 (한국어)."
            : $"루빅스 큐브가 {movesCount}번의 역방향 조작으로 풀립니다. " +
              "AI가 큐브를 분석하고 푸는 중이라는 짧은 한 문장 메시지를 한국어로 만들어주세요.";

        var body = JsonSerializer.Serialize(new
        {
            model = FreeModels[0],
            max_tokens = 80,
            messages = new[] { new { role = "user", content = prompt } }
        });

        var req = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Headers.Add("HTTP-Referer", "https://github.com/cubegame");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var resp = await Http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content?.Trim() ?? GetLocalComment(movesCount);
    }

    private static string GetLocalComment(int movesCount) => movesCount switch
    {
        0 => "✅ 큐브가 이미 완성 상태입니다!",
        <= 5 => $"🤖 AI 분석 완료 — {movesCount}번 이동으로 복원합니다.",
        <= 15 => $"🤖 AI가 최적 경로를 계산했습니다 — {movesCount}단계 복원 시작.",
        _ => $"🤖 AI가 복잡한 상태를 분석했습니다 — {movesCount}번 역방향 복원.",
    };
}
