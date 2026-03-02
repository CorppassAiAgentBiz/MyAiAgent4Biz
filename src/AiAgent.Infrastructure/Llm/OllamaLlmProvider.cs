using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Llm;

/// <summary>
/// Ollama LLM 提供者（混合策略）。
/// 工具偵測由程式碼邏輯處理（小模型無法可靠遵循 JSON 工具格式），
/// 自然語言回應由 Ollama 本地模型生成。
/// </summary>
public sealed class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly string _baseUrl;
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ProviderName => $"Ollama ({_modelName})";

    public OllamaLlmProvider(string modelName = "tinydolphin", string baseUrl = "http://localhost:11434", HttpClient? httpClient = null)
    {
        _modelName = modelName;
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var userMessage = request.UserMessage;
        var lowerMessage = userMessage.ToLower();

        // 若歷史中有 tool 記錄，表示工具已執行完畢，使用 LLM 生成摘要回應
        var hasToolResult = request.History?.Any(h => h.Role == "tool") == true;
        if (hasToolResult)
        {
            var toolEntry = request.History!.Last(h => h.Role == "tool");
            return await GenerateSummaryAsync(
                $"The tool '{toolEntry.ToolName}' returned: {toolEntry.Content}. Please summarize this result for the user in a helpful way.",
                request.SystemPrompt,
                cancellationToken);
        }

        // ── 程式碼層工具偵測（小模型不可靠，由程式碼處理） ──

        // 時間查詢偵測
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

        // Echo/重複偵測
        if (userMessage.Contains("重複") || userMessage.Contains("重复") ||
            lowerMessage.Contains("echo") || lowerMessage.Contains("repeat"))
        {
            var textToRepeat = ExtractEchoText(userMessage, lowerMessage);
            return new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "echo",
                    Arguments = new Dictionary<string, string> { ["message"] = textToRepeat }
                }
            };
        }

        // ── 非工具呼叫：使用 Ollama 生成自然語言回應 ──
        return await GenerateChatResponseAsync(request, cancellationToken);
    }

    private async Task<LlmResponse> GenerateChatResponseAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var messages = new List<object>();

            var systemPrompt = request.SystemPrompt ?? "You are a helpful AI assistant. Respond concisely.";
            messages.Add(new { role = "system", content = systemPrompt });

            if (request.History?.Count > 0)
            {
                foreach (var entry in request.History)
                {
                    var role = entry.Role switch
                    {
                        "user" => "user",
                        "assistant" => "assistant",
                        _ => "user"
                    };
                    messages.Add(new { role, content = entry.Content });
                }
            }

            messages.Add(new { role = "user", content = request.UserMessage });

            var ollamaRequest = new
            {
                model = _modelName,
                messages,
                stream = false,
                options = new { temperature = 0.7, num_predict = 256 }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/chat", ollamaRequest, s_jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(jsonContent, s_jsonOptions);
            var content = chatResponse?.Message?.Content?.Trim() ?? "（無回應）";

            return new LlmResponse { Content = content };
        }
        catch (HttpRequestException ex)
        {
            return new LlmResponse
            {
                Content = $"[Ollama 連線錯誤] 請確保 Ollama 服務正在執行於 {_baseUrl}。錯誤：{ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new LlmResponse { Content = $"[錯誤] {ex.Message}" };
        }
    }

    private async Task<LlmResponse> GenerateSummaryAsync(string prompt, string? systemPrompt, CancellationToken cancellationToken)
    {
        try
        {
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt ?? "You are a helpful AI assistant. Summarize the tool result concisely." },
                new { role = "user", content = prompt }
            };

            var ollamaRequest = new
            {
                model = _modelName,
                messages,
                stream = false,
                options = new { temperature = 0.5, num_predict = 128 }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/chat", ollamaRequest, s_jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(jsonContent, s_jsonOptions);
            var content = chatResponse?.Message?.Content?.Trim() ?? "（工具已執行完畢）";

            return new LlmResponse { Content = content };
        }
        catch
        {
            return new LlmResponse { Content = "已為您執行工具，以上是結果。" };
        }
    }

    private static string ExtractEchoText(string userMessage, string lowerMessage)
    {
        if (userMessage.Contains("："))
        {
            var parts = userMessage.Split("：", 2);
            return parts.Length > 1 ? parts[1].Trim() : userMessage;
        }
        if (userMessage.Contains(":"))
        {
            var parts = userMessage.Split(":", 2);
            return parts.Length > 1 ? parts[1].Trim() : userMessage;
        }
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

internal sealed class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }
}

internal sealed class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
