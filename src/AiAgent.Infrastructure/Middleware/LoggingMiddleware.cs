using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Middleware;

/// <summary>
/// 責任鏈模式（Chain of Responsibility）：記錄 Agent 請求與回應資訊的中介軟體。
/// <para>
/// 於請求進入管線時記錄開始訊息，並於回應返回時記錄完成狀態。
/// <see cref="Order"/> 為 0，執行順序在 <see cref="ValidationMiddleware"/>（Order = -10）之後。
/// </para>
/// </summary>
public sealed class LoggingMiddleware : IAgentMiddleware
{
    /// <summary>取得此中介軟體在管線中的執行順序。值為 0，在驗證中介軟體（-10）之後執行。</summary>
    public int Order => 0;

    /// <summary>
    /// 用於輸出日誌訊息的記錄委派函式。
    /// </summary>
    private readonly Action<string>? _logger;

    /// <summary>
    /// 初始化 <see cref="LoggingMiddleware"/> 的新執行個體。
    /// </summary>
    /// <param name="logger">
    /// 選擇性的自訂記錄函式。若為 <see langword="null"/>，則不輸出任何訊息。
    /// </param>
    public LoggingMiddleware(Action<string>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 在請求處理前後記錄日誌訊息，並繼續傳遞至管線中的下一個節點。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="next">代表管線中下一個節點的委派函式。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>由後續管線節點產生的 <see cref="AgentResponse"/>。</returns>
    public async Task<AgentResponse> InvokeAsync(AgentContext context, AgentMiddlewareDelegate next, CancellationToken cancellationToken = default)
    {
        _logger?.Invoke($"[{DateTimeOffset.UtcNow:O}] Agent '{context.Agent.Name}' starting. Session: {context.Request.SessionId}");
        var response = await next(context, cancellationToken);
        _logger?.Invoke($"[{DateTimeOffset.UtcNow:O}] Agent '{context.Agent.Name}' completed. Success: {response.IsSuccess}");
        return response;
    }
}
