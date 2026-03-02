namespace AiAgent.Core.Models;

/// <summary>
/// LLM 回傳的回應物件，包含文字內容與可能的工具呼叫請求。
/// </summary>
public sealed class LlmResponse
{
    /// <summary>取得 LLM 回覆的文字內容。若 LLM 決定呼叫工具，此欄位可能為空字串。</summary>
    public required string Content { get; init; }

    /// <summary>
    /// 取得 LLM 請求呼叫的工具資訊。
    /// 若 LLM 決定不呼叫任何工具，則為 <see langword="null"/>。
    /// </summary>
    public ToolCallRequest? ToolCall { get; init; }

    /// <summary>
    /// 取得 LLM 是否請求呼叫工具的旗標。
    /// 當 <see cref="ToolCall"/> 不為 <see langword="null"/> 時回傳 <see langword="true"/>。
    /// </summary>
    public bool HasToolCall => ToolCall != null;
}
