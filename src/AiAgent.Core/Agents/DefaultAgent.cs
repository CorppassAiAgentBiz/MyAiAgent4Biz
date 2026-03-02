using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Core.Agents;

/// <summary>
/// 預設具體 Agent 實作，直接繼承 <see cref="BaseAgent"/> 而不覆寫任何範本方法步驟。
/// <para>
/// 適合在無需自訂感知、規劃、行動或反思邏輯的情境下直接使用。
/// 若需要自訂行為，請繼承 <see cref="BaseAgent"/> 並覆寫對應的 Protected 方法。
/// </para>
/// </summary>
public sealed class DefaultAgent : BaseAgent
{
    /// <summary>
    /// 初始化 <see cref="DefaultAgent"/> 的新執行個體。
    /// </summary>
    /// <param name="options">包含 Agent 名稱、描述、系統提示詞等設定的選項物件。</param>
    /// <param name="llmProvider">用於向 LLM 發送補全請求的提供者。</param>
    /// <param name="toolRegistry">管理 Agent 可用工具的工具登錄。</param>
    /// <param name="memoryStore">負責儲存對話歷史的記憶體儲存。</param>
    /// <param name="eventHandlers">訂閱生命週期事件的處理器集合。</param>
    public DefaultAgent(
        AgentOptions options,
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IMemoryStore memoryStore,
        IEnumerable<IAgentEventHandler> eventHandlers)
        : base(options, llmProvider, toolRegistry, memoryStore, eventHandlers)
    {
    }
}
