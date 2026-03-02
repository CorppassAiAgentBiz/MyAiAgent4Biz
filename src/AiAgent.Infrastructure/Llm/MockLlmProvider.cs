using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Llm;

/// <summary>
/// 策略模式（Strategy）：用於測試與開發階段的模擬 LLM 提供者。
/// <para>
/// 透過注入自訂的 <see cref="Func{LlmRequest, LlmResponse}"/> 工廠函式，
/// 可靈活控制回應內容，便於單元測試中模擬不同的 LLM 行為（如工具呼叫、特定回覆等）。
/// 預設行為會根據使用者輸入智能地決定是否呼叫工具（如「現在幾點」→ CurrentTime 工具）。
/// 正式環境可替換為 OpenAI、Azure OpenAI、Ollama 等真實 LLM 的實作。
/// </para>
/// </summary>
public sealed class MockLlmProvider : ILlmProvider
{
    /// <summary>
    /// 用於產生模擬回應的工廠函式。
    /// 為 <see langword="null"/> 時使用預設行為（根據輸入內容決定工具呼叫或 Echo）。
    /// </summary>
    private readonly Func<LlmRequest, LlmResponse>? _responseFactory;

    /// <summary>取得此 LLM 提供者的名稱。固定為「Mock」。</summary>
    public string ProviderName => "Mock";

    /// <summary>
    /// 初始化 <see cref="MockLlmProvider"/> 的新執行個體。
    /// </summary>
    /// <param name="responseFactory">
    /// 選擇性的回應工廠函式。若為 <see langword="null"/>，
    /// 則預設行為會根據使用者訊息內容智能地識別工具呼叫或回傳 Echo。
    /// </param>
    public MockLlmProvider(Func<LlmRequest, LlmResponse>? responseFactory = null)
    {
        _responseFactory = responseFactory;
    }

    /// <summary>
    /// 以非同步方式產生模擬的 LLM 補全回應。
    /// 若有注入工廠函式則使用工廠；否則根據使用者訊息內容智能決定行為。
    /// </summary>
    /// <param name="request">包含系統提示詞、對話歷史、使用者訊息及可用工具的請求物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    /// <returns>由工廠函式或預設邏輯產生的 <see cref="LlmResponse"/>。</returns>
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        if (_responseFactory != null)
        {
            return Task.FromResult(_responseFactory(request));
        }

        // 預設行為：根據訊息內容智能識別工具呼叫
        var userMessage = request.UserMessage;
        var lowerMessage = userMessage.ToLower();

        // 檢查是否已經處理過當前消息（工具已執行過，現在是第二次 LLM 呼叫）
        // 判斷方式：歷史中有 tool role 的記錄，表示工具已經被執行過
        if (request.History != null && request.History.Any(h => h.Role == "tool"))
        {
            return Task.FromResult(new LlmResponse
            {
                Content = "已根據查詢為您調用相應工具，以上就是結果。"
            });
        }

        // 檢測「現在幾點」/「现在几点」相關的詢問（支持簡體、繁體和英文）
        var isTimeQuery = 
            // 繁體和簡體中文
            ((userMessage.Contains("現在") || userMessage.Contains("现在")) && 
             (userMessage.Contains("幾點") || userMessage.Contains("几点"))) ||
            // 英文和其他
            lowerMessage.Contains("時間") || lowerMessage.Contains("时间") ||
            lowerMessage.Contains("time") ||
            (lowerMessage.Contains("what") && lowerMessage.Contains("time")) ||
            (lowerMessage.Contains("when") && lowerMessage.Contains("time")) ||
            lowerMessage.Contains("hour") || lowerMessage.Contains("clock") ||
            lowerMessage.Contains("current time") || lowerMessage.Contains("current_time");

        if (isTimeQuery)
        {
            return Task.FromResult(new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "get_current_time",
                    Arguments = new Dictionary<string, string>()
                }
            });
        }

        // 檢測「重複」/「echo」相關的請求
        if (userMessage.Contains("重複") || userMessage.Contains("重复") || 
            lowerMessage.Contains("echo") || lowerMessage.Contains("repeat") || 
            userMessage.StartsWith("重複：") || userMessage.StartsWith("重复：") ||
            lowerMessage.StartsWith("echo:") || lowerMessage.StartsWith("repeat:"))
        {
            // 提取要重複的文本
            var textToRepeat = userMessage;
            if (userMessage.Contains("："))
            {
                var parts = userMessage.Split("：");
                textToRepeat = parts.Length > 1 ? parts[1].Trim() : userMessage;
            }
            else if (userMessage.Contains(":"))
            {
                var parts = userMessage.Split(":");
                textToRepeat = parts.Length > 1 ? parts[1].Trim() : userMessage;
            }
            else if (userMessage.StartsWith("重複") || userMessage.StartsWith("重复"))
            {
                // 如果直接说"重复xxx"，则提取xxx部分
                var startIndex = userMessage.StartsWith("重複") ? 2 : 2;
                if (userMessage.Length > startIndex)
                {
                    textToRepeat = userMessage.Substring(startIndex).Trim();
                }
            }

            return Task.FromResult(new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "echo",
                    Arguments = new Dictionary<string, string> { ["message"] = textToRepeat }
                }
            });
        }

        // 預設：回傳 Echo 回應
        return Task.FromResult(new LlmResponse
        {
            Content = $"Echo: {request.UserMessage}"
        });
    }
}
