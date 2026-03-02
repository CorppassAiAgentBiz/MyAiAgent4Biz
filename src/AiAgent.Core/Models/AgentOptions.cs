namespace AiAgent.Core.Models;

/// <summary>
/// Agent 建立時的設定選項，包含名稱、描述、系統提示詞及迭代次數上限。
/// 透過 <see cref="AiAgent.Core.Abstractions.IAgentFactory"/> 傳入以建立 Agent 實例。
/// </summary>
public sealed class AgentOptions
{
    /// <summary>取得 Agent 的唯一名稱。此為必填欄位。</summary>
    public required string Name { get; init; }

    /// <summary>取得 Agent 的功能描述。預設為空字串。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 取得傳送給 LLM 的系統提示詞（System Prompt），用來定義 Agent 的角色與行為準則。
    /// 預設為「You are a helpful AI assistant.」。
    /// </summary>
    public string SystemPrompt { get; init; } = "You are a helpful AI assistant.";

    /// <summary>
    /// 取得 Agent 在單次請求中，Perceive→Plan→Act 循環的最大迭代次數上限，
    /// 以防止工具呼叫進入無限循環。預設為 10 次。
    /// </summary>
    public int MaxIterations { get; init; } = 10;
}
