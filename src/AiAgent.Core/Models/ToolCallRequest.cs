namespace AiAgent.Core.Models;

/// <summary>
/// LLM 請求呼叫工具時所傳遞的請求物件，包含工具名稱及傳入參數。
/// 此物件由 <see cref="LlmResponse.ToolCall"/> 屬性持有，並由 Agent 解析後執行對應工具。
/// </summary>
public sealed class ToolCallRequest
{
    /// <summary>取得 LLM 請求呼叫的工具名稱，對應至 <see cref="AiAgent.Core.Abstractions.ITool.Name"/>。</summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// 取得 LLM 指定的工具執行參數字典（鍵值對）。
    /// 若 LLM 未指定任何參數，則為空字典。
    /// </summary>
    public IReadOnlyDictionary<string, string> Arguments { get; init; } = new Dictionary<string, string>();
}
