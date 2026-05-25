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

    // OpenRouter free 모델 목록 (순서대로 fallback)
    // 모델 폐기/rate-limit 시 다음 모델로 자동 전환됩니다.
    private static readonly string[] FreeModels =
    [
        "deepseek/deepseek-v4-flash:free",
        "openai/gpt-oss-20b:free",
        "google/gemma-4-31b-it:free",
        "meta-llama/llama-3.3-70b-instruct:free",
        "meta-llama/llama-3.2-3b-instruct:free",
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
    // SubAgent 1: 큐브 상태 분석 (한국어 설명 반환)
    // ──────────────────────────────────────────────────────────────────
    public static async Task<AiResult> GetCubeAnalysisAsync(string cubeState, CancellationToken ct)
    {
        var prompt =
            "You are a Rubik's cube expert. Analyze the following cube state and briefly describe " +
            "in English what is out of place and what general strategy should be used to solve it. " +
            "Be concise (2-3 sentences max).\n\n" +
            "Cube state (W=White/top, Y=Yellow/bottom, G=Green/front, B=Blue/back, O=Orange/left, R=Red/right):\n" +
            cubeState;

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AiResult(prompt, "No API key — analysis skipped.");

        try
        {
            var response = await CallAsync(apiKey, prompt, maxTokens: 150, ct);
            return new AiResult(prompt, response);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new AiResult(prompt, $"Analysis failed: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // SubAgent 2: 풀이 이동 수식 생성 (표준 표기법만 반환)
    // analysis: Agent 1 결과를 컨텍스트로 제공해 정확도 향상
    // ──────────────────────────────────────────────────────────────────
    public static async Task<AiResult> GetSolvingMovesAsync(
        string cubeState, string analysis, CancellationToken ct)
    {
        // 시스템 프롬프트: 표준 표기법만 출력하도록 강제
        var systemPrompt =
            "You are a Rubik's cube solver. Your ONLY task is to output the solution move sequence " +
            "in standard Rubik's notation. Rules:\n" +
            "- Valid tokens: U U' U2  D D' D2  R R' R2  L L' L2  F F' F2  B B' B2  M M' E E' S S'\n" +
            "- Output ONLY the move tokens separated by spaces, nothing else\n" +
            "- No explanations, no punctuation, no numbering\n" +
            "- Example output: R U R' U' R' F R2 U' R' U' R U R' F'\n" +
            "- Goal: return ALL six faces to a single uniform color";

        var userPrompt =
            $"Cube analysis: {analysis}\n\n" +
            "Cube state (each face row-by-row, | separates rows):\n" +
            cubeState + "\n\n" +
            "Solution moves:";

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new AiResult(userPrompt,
                "⚠️ API 키 없음. OPENROUTER_API_KEY 환경변수를 설정하세요.");

        try
        {
            // 시스템+유저 메시지 두 개를 전달하는 별도 호출
            var response = await CallWithSystemAsync(
                apiKey, systemPrompt, userPrompt, maxTokens: 300, ct);
            return new AiResult(userPrompt, response);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new AiResult(userPrompt, $"❌ AI 연결 실패: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // (구) AI 직접 풀이 모드 — 단순 텍스트 조언 (호환용, 더 이상 기본 경로 아님)
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

    /// <summary>
    /// FreeModels 목록을 순서대로 시도합니다.
    /// 404(모델 없음)·429(rate-limit)이면 다음 모델로 fallback.
    /// </summary>
    private static async Task<string> CallAsync(
        string apiKey, string prompt, int maxTokens, CancellationToken ct)
    {
        string? lastError = null;

        foreach (var model in FreeModels)
        {
            ct.ThrowIfCancellationRequested();

            var body = JsonSerializer.Serialize(new
            {
                model,
                max_tokens = maxTokens,
                messages = new[] { new { role = "user", content = prompt } }
            });

            var req = new HttpRequestMessage(
                HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Headers.Add("HTTP-Referer", "https://github.com/cubegame");
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            string json;
            try
            {
                resp = await Http.SendAsync(req, ct);
                json = await resp.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                lastError = $"❌ 네트워크 오류: {ex.Message}";
                continue;   // 다음 모델 시도
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // API 오류 응답 처리 ({"error": {...}} 형태)
            if (root.TryGetProperty("error", out var errEl))
            {
                var msg  = errEl.TryGetProperty("message", out var m) ? m.GetString() : null;
                int code = 0;
                if (errEl.TryGetProperty("code", out var c))
                    code = c.ValueKind == JsonValueKind.Number ? c.GetInt32()
                         : int.TryParse(c.GetString(), out var n) ? n : 0;

                // 404: 모델 없음 / 429: rate-limit → 다음 모델로
                if (code == 404 || code == 429)
                {
                    lastError = $"⚠️ [{model}] {code}: {msg}";
                    continue;
                }

                // 그 외 오류는 즉시 반환
                return $"❌ API 오류 [{code}]: {msg ?? "알 수 없는 오류"}";
            }

            // 정상 응답에서 content 추출
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.GetArrayLength() == 0)
            {
                lastError = $"⚠️ 응답 형식 오류\n{json[..Math.Min(json.Length, 80)]}";
                continue;
            }

            return choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim() ?? "(응답 없음)";
        }

        // 모든 모델 실패
        return lastError ?? "❌ 사용 가능한 AI 모델이 없습니다. 잠시 후 다시 시도해주세요.";
    }

    // ──────────────────────────────────────────────────────────────────
    // system + user 두 메시지를 전달하는 호출 (SubAgent 2용)
    // ──────────────────────────────────────────────────────────────────
    private static async Task<string> CallWithSystemAsync(
        string apiKey, string systemContent, string userContent,
        int maxTokens, CancellationToken ct)
    {
        string? lastError = null;

        foreach (var model in FreeModels)
        {
            ct.ThrowIfCancellationRequested();

            var body = JsonSerializer.Serialize(new
            {
                model,
                max_tokens = maxTokens,
                messages = new object[]
                {
                    new { role = "system", content = systemContent },
                    new { role = "user",   content = userContent   }
                }
            });

            var req = new HttpRequestMessage(
                HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Headers.Add("HTTP-Referer", "https://github.com/cubegame");
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage resp;
            string json;
            try
            {
                resp = await Http.SendAsync(req, ct);
                json = await resp.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { lastError = $"❌ 네트워크 오류: {ex.Message}"; continue; }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errEl))
            {
                var msg  = errEl.TryGetProperty("message", out var m) ? m.GetString() : null;
                int code = 0;
                if (errEl.TryGetProperty("code", out var c))
                    code = c.ValueKind == JsonValueKind.Number ? c.GetInt32()
                         : int.TryParse(c.GetString(), out var n) ? n : 0;

                if (code == 404 || code == 429) { lastError = $"⚠️ [{model}] {code}"; continue; }
                return $"❌ API 오류 [{code}]: {msg ?? "알 수 없는 오류"}";
            }

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.GetArrayLength() == 0)
            { lastError = "⚠️ 응답 형식 오류"; continue; }

            return choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim() ?? "";
        }

        return lastError ?? "❌ 사용 가능한 AI 모델이 없습니다.";
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
