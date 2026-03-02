using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 工廠模式（Factory）：負責根據設定選項建立 Agent 實例的介面。
/// 透過此介面可集中管理 Agent 的建立邏輯，並解耦呼叫端與具體實作。
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// 根據指定的選項建立並回傳一個已設定好的 <see cref="IAgent"/> 實例。
    /// </summary>
    /// <param name="options">包含 Agent 名稱、描述、系統提示詞等設定的選項物件。</param>
    /// <returns>已完整初始化的 <see cref="IAgent"/> 實例。</returns>
    IAgent Create(AgentOptions options);
}
