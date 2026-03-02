using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 責任鏈模式（Chain of Responsibility）：定義 Agent 處理管線中介軟體的介面。
/// 每個中介軟體可在請求傳遞至下一個節點前後執行自訂邏輯（如驗證、記錄等）。
/// </summary>
public interface IAgentMiddleware
{
    /// <summary>
    /// 取得此中介軟體在管線中的執行順序。數值越小越優先執行。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 以非同步方式執行中介軟體邏輯，並在適當時機呼叫管線中的下一個節點。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="next">代表管線中下一個節點的委派函式。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>由後續管線節點或最終 Agent 所產生的 <see cref="AgentResponse"/>。</returns>
    Task<AgentResponse> InvokeAsync(AgentContext context, AgentMiddlewareDelegate next, CancellationToken cancellationToken = default);
}

/// <summary>
/// 代表 Agent 處理管線中下一個節點的委派型別。
/// </summary>
/// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
/// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
/// <returns>後續管線節點產生的 <see cref="AgentResponse"/>。</returns>
public delegate Task<AgentResponse> AgentMiddlewareDelegate(AgentContext context, CancellationToken cancellationToken);
