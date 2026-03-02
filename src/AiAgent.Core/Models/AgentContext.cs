using AiAgent.Core.Abstractions;

namespace AiAgent.Core.Models;

/// <summary>
/// Agent 執行上下文，封裝單次請求的所有相關資訊。
/// 此物件會在中介軟體管線與事件處理器之間共享傳遞。
/// </summary>
public sealed class AgentContext
{
    /// <summary>取得觸發本次執行的原始請求物件。</summary>
    public required AgentRequest Request { get; init; }

    /// <summary>取得負責處理本次請求的 Agent 實例。</summary>
    public required IAgent Agent { get; init; }

    /// <summary>
    /// 取得可供中介軟體及事件處理器自由存取的共享狀態字典。
    /// 鍵為字串識別碼，值可為任意物件。
    /// </summary>
    public Dictionary<string, object> Items { get; } = new();
}
