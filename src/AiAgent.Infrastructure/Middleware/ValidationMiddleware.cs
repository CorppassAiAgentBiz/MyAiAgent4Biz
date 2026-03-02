using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Middleware;

/// <summary>
/// 責任鏈模式（Chain of Responsibility）：在請求進入核心處理流程前驗證其合法性的中介軟體。
/// <para>
/// <see cref="Order"/> 為 -10，確保此中介軟體在所有其他中介軟體之前優先執行。
/// 若驗證失敗，將直接回傳錯誤回應，不繼續傳遞至管線後續節點。
/// </para>
/// </summary>
public sealed class ValidationMiddleware : IAgentMiddleware
{
    /// <summary>
    /// 取得此中介軟體在管線中的執行順序。
    /// 值為 -10，確保在 <see cref="LoggingMiddleware"/>（Order = 0）之前執行。
    /// </summary>
    public int Order => -10;

    /// <summary>
    /// 驗證請求中的使用者訊息與工作階段 ID 是否合法。
    /// 驗證通過則傳遞至管線下一節點；不通過則直接回傳包含錯誤訊息的失敗回應。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="next">代表管線中下一個節點的委派函式。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>
    /// 驗證通過時回傳後續管線節點產生的 <see cref="AgentResponse"/>；
    /// 驗證失敗時回傳 <see cref="AgentResponse.Failure"/> 實例。
    /// </returns>
    public Task<AgentResponse> InvokeAsync(AgentContext context, AgentMiddlewareDelegate next, CancellationToken cancellationToken = default)
    {
        // 驗證使用者訊息不可為空白
        if (string.IsNullOrWhiteSpace(context.Request.UserMessage))
            return Task.FromResult(AgentResponse.Failure(context.Request.SessionId, "User message cannot be empty."));

        // 驗證工作階段 ID 不可為空白
        if (string.IsNullOrWhiteSpace(context.Request.SessionId))
            return Task.FromResult(AgentResponse.Failure(context.Request.SessionId, "Session ID cannot be empty."));

        return next(context, cancellationToken);
    }
}
