using AiAgent.Core.Abstractions;
using AiAgent.Core.Agents;
using AiAgent.Core.Models;

namespace AiAgent.Core.Factory;

/// <summary>
/// 工廠模式（Factory）：負責建立已完整設定的 <see cref="DefaultAgent"/> 實例。
/// <para>
/// 透過此類別可集中管理 Agent 的依賴注入邏輯，呼叫端只需提供
/// <see cref="AgentOptions"/> 即可取得可立即使用的 Agent。
/// </para>
/// </summary>
public sealed class AgentFactory : IAgentFactory
{
    /// <summary>用於向 LLM 發送請求的提供者（策略模式）。</summary>
    private readonly ILlmProvider _llmProvider;

    /// <summary>管理可用工具的工具登錄（外掛/登錄模式）。</summary>
    private readonly IToolRegistry _toolRegistry;

    /// <summary>負責儲存對話歷史的記憶體儲存（儲存庫模式）。</summary>
    private readonly IMemoryStore _memoryStore;

    /// <summary>訂閱 Agent 生命週期事件的處理器集合（觀察者模式）。</summary>
    private readonly IEnumerable<IAgentEventHandler> _eventHandlers;

    /// <summary>
    /// 初始化 <see cref="AgentFactory"/> 的新執行個體，注入所有必要的依賴。
    /// </summary>
    /// <param name="llmProvider">LLM 後端提供者。</param>
    /// <param name="toolRegistry">工具登錄實例。</param>
    /// <param name="memoryStore">記憶體儲存實例。</param>
    /// <param name="eventHandlers">事件處理器集合。</param>
    public AgentFactory(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IMemoryStore memoryStore,
        IEnumerable<IAgentEventHandler> eventHandlers)
    {
        _llmProvider = llmProvider;
        _toolRegistry = toolRegistry;
        _memoryStore = memoryStore;
        _eventHandlers = eventHandlers;
    }

    /// <summary>
    /// 根據指定的選項建立並回傳一個已設定好的 <see cref="DefaultAgent"/> 實例。
    /// </summary>
    /// <param name="options">包含 Agent 名稱、描述、系統提示詞等設定的選項物件。</param>
    /// <returns>已完整初始化、可立即使用的 <see cref="IAgent"/> 實例。</returns>
    public IAgent Create(AgentOptions options)
        => new DefaultAgent(options, _llmProvider, _toolRegistry, _memoryStore, _eventHandlers);
}
