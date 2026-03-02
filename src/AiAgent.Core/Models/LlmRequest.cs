using AiAgent.Core.Abstractions;

namespace AiAgent.Core.Models;

/// <summary>
/// 傳送至 LLM 的請求物件，包含系統提示詞、對話歷史、使用者訊息及可用工具清單。
/// </summary>
public sealed class LlmRequest
{
    /// <summary>取得傳送給 LLM 的系統提示詞，用於定義 Agent 的角色與行為規範。</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>取得目前工作階段的完整對話歷史清單，依時間順序排列。</summary>
    public required IReadOnlyList<ConversationEntry> History { get; init; }

    /// <summary>取得本輪使用者輸入的訊息內容。</summary>
    public required string UserMessage { get; init; }

    /// <summary>
    /// 取得 LLM 可呼叫的工具清單。LLM 可依需求決定是否呼叫其中一個工具。
    /// 預設為空清單（無可用工具）。
    /// </summary>
    public IReadOnlyList<ITool> AvailableTools { get; init; } = [];
}
