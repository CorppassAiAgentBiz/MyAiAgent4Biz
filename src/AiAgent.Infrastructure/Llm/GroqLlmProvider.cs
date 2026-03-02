using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AiAgent.Infrastructure.Llm;

/// <summary>
/// Groq API LLM 提供者 - 超快速推論（0.1-0.5秒）
/// <para>
/// 免費層：每日 500K tokens
/// 支援模型：llama-3.1-8b-instant（推薦）、mixtral-8x7b-32768、llama-2-70b-chat、gemma-7b-it
/// 註冊：https://console.groq.com
/// </para>
/// </summary>
public sealed class GroqLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    // 免費層可用的超快模型
    private const string DefaultModel = "llama-3.1-8b-instant";
    private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

    public string ProviderName => "Groq API (Ultra-fast, Free)";

    public GroqLlmProvider(string? apiKey = null, string? model = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? throw new ArgumentException(
                "Groq API key not found. Please set it in:\n" +
                "1. appsettings.json: LlmProvider:Groq:ApiKey or\n" +
                "2. Environment variable: GROQ_API_KEY\n" +
                "Register at: https://console.groq.com");

        _model = model ?? DefaultModel;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AiAgent-Groq/1.0");
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var userMessage = request.UserMessage;
        var lowerMessage = userMessage.ToLower();

        // ── 若歷史中有 tool 結果，優先處理工具結果 ──
        if (request.History?.Any(h => h.Role == "tool") == true)
        {
            var toolEntry = request.History!.Last(h => h.Role == "tool");

            // 時間工具不需要 Groq 摘要，直接格式化並返回
            if (toolEntry.ToolName == "get_current_time")
            {
                if (DateTimeOffset.TryParse(toolEntry.Content, out var dateTime))
                {
                    var taiwanTime = TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"));
                    var formattedTime = taiwanTime.ToString("yyyy年MM月dd日 HH:mm:ss (ddd)", new System.Globalization.CultureInfo("zh-TW"));
                    return new LlmResponse { Content = $"現在時間是：{formattedTime}" };
                }
            }

            // Echo 工具直接返回內容
            if (toolEntry.ToolName == "echo")
            {
                return new LlmResponse { Content = toolEntry.Content };
            }

            // 圖片分析工具——清理並改進結果
            if (toolEntry.ToolName == "analyze_image")
            {
                // 清理原始結果中的所有技術標籤和重複內容
                var analysisResult = toolEntry.Content;

                // 移除所有 [analyze_image] 標籤
                var cleanedResult = Regex.Replace(analysisResult, @"\[analyze_image\]\s*", "");

                // 如果有重複的內容（基於相同的提示詞），只保留第一次出現
                var lines = cleanedResult.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                cleanedResult = lines.Length > 0 ? lines[0].Trim() : cleanedResult.Trim();

                // 直接返回清理後的結果
                return new LlmResponse { Content = cleanedResult };
            }

            // 其他工具結果由 Groq 摘要
            // TODO: 可以在這裡添加其他工具結果的摘要邏輯
        }

        // ── 程式碼層工具偵測（可靠） ──
        // 時間查詢
        var isTimeQuery =
            ((userMessage.Contains("現在") || userMessage.Contains("现在")) &&
             (userMessage.Contains("幾點") || userMessage.Contains("几点"))) ||
            lowerMessage.Contains("時間") || lowerMessage.Contains("时间") ||
            (lowerMessage.Contains("what") && lowerMessage.Contains("time")) ||
            (lowerMessage.Contains("when") && lowerMessage.Contains("time")) ||
            lowerMessage.Contains("current time") || lowerMessage.Contains("current_time") ||
            lowerMessage.Contains("hour") || lowerMessage.Contains("clock");

        if (isTimeQuery)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "get_current_time",
                    Arguments = new Dictionary<string, string>()
                }
            };
        }

        /* Echo 工具 - 讓 Agent 重複使用者輸入的內容
           偵測條件：使用者訊息包含「重複」「重复」「echo」或「repeat」
           透過提取關鍵字後的文本作為要回復的內容
        if (userMessage.Contains("重複") || userMessage.Contains("重复") ||
            lowerMessage.Contains("echo") || lowerMessage.Contains("repeat"))
        {
            return new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "echo",
                    Arguments = new Dictionary<string, string> { ["message"] = ExtractEchoText(userMessage, lowerMessage) }
                }
            };
        }
        */

        // 圖片分析工具（當用戶提及圖片時自動觸發）
        if (userMessage.Contains("圖片") || userMessage.Contains("图片") ||
            userMessage.Contains("分析") || userMessage.Contains("图像") || userMessage.Contains("圖像") ||
            lowerMessage.Contains("image") || lowerMessage.Contains("photo") ||
            lowerMessage.Contains("picture") || lowerMessage.Contains("analyze"))
        {
            return new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "analyze_image",
                    Arguments = new Dictionary<string, string>()
                }
            };
        }

        // ── 構建訊息清單 ──
        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        // 限制歷史記錄以節省 tokens (僅保留最近 10 條)
        if (request.History != null)
        {
            foreach (var entry in request.History.TakeLast(10))
            {
                messages.Add(new
                {
                    role = entry.Role,
                    content = entry.Content
                });
            }
        }

        messages.Add(new { role = "user", content = request.UserMessage });

        // 構建請求
        var payload = new
        {
            model = _model,
            messages = messages,
            temperature = 0.7,
            max_tokens = 1024,
            top_p = 0.9,
            stream = false
        };

        // 序列化請求
        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        // 發送請求
        var response = await _httpClient.PostAsync(GroqApiUrl, content, cancellationToken);

        // 處理回應
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? error = null;
            try
            {
                error = JsonSerializer.Deserialize<ErrorResponse>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }

            throw new HttpRequestException(
                $"Groq API error ({response.StatusCode}): {error?.Error?.Message ?? responseBody}");
        }

        // 解析成功回應
        var result = JsonSerializer.Deserialize<GroqResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result?.Choices == null || result.Choices.Length == 0)
        {
            throw new InvalidOperationException("No choices in Groq response");
        }

        var responseContent = result.Choices[0].Message?.Content ?? "No response";

        return new LlmResponse
        {
            Content = responseContent
        };
    }

    // ── 輔助方法 ──
    private static string ExtractEchoText(string userMessage, string lowerMessage)
    {
        if (userMessage.Contains('：')) return userMessage.Split("：", 2).Last().Trim();
        if (userMessage.Contains(':')) return userMessage.Split(":", 2).Last().Trim();
        if (userMessage.StartsWith("重複") || userMessage.StartsWith("重复"))
        {
            var rest = userMessage.Substring(2).Trim();
            return rest.Length > 0 ? rest : userMessage;
        }
        if (lowerMessage.StartsWith("echo ") || lowerMessage.StartsWith("repeat "))
        {
            var idx = userMessage.IndexOf(' ');
            return idx >= 0 ? userMessage.Substring(idx + 1).Trim() : userMessage;
        }
        return userMessage;
    }
}

// DTO 類別
public class GroqResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public GroqChoice[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public GroqUsage? Usage { get; set; }
}

public class GroqChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public GroqMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class GroqMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class GroqUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorDetail? Error { get; set; }
}

public class ErrorDetail
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("param")]
    public string? Param { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

