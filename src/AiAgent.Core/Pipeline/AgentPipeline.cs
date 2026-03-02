using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Core.Pipeline;

/// <summary>
/// 責任鏈模式（Chain of Responsibility）：依序執行已排序的中介軟體，最後呼叫終端處理委派。
/// <para>
/// 中介軟體依 <see cref="IAgentMiddleware.Order"/> 屬性由小到大排列，
/// 先執行 Order 數值較小的中介軟體。每個中介軟體可決定是否繼續傳遞至下一個節點。
/// </para>
/// </summary>
public sealed class AgentPipeline
{
    /// <summary>已依 Order 排序的中介軟體唯讀清單。</summary>
    private readonly IReadOnlyList<IAgentMiddleware> _middlewares;

    /// <summary>
    /// 初始化 <see cref="AgentPipeline"/> 的新執行個體，並依 Order 排序中介軟體。
    /// </summary>
    /// <param name="middlewares">要加入管線的中介軟體集合。</param>
    public AgentPipeline(IEnumerable<IAgentMiddleware> middlewares)
    {
        _middlewares = middlewares.OrderBy(m => m.Order).ToList();
    }

    /// <summary>
    /// 以非同步方式執行管線。中介軟體依序包裹終端委派，形成洋蔥式的責任鏈結構。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="terminal">管線末端的最終處理委派（通常是呼叫 Agent 本身）。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>由管線（含所有中介軟體及終端）最終產生的 <see cref="AgentResponse"/>。</returns>
    public Task<AgentResponse> ExecuteAsync(AgentContext context, AgentMiddlewareDelegate terminal, CancellationToken cancellationToken = default)
    {
        // 從終端委派開始，由後往前依序包裹每個中介軟體，形成責任鏈
        AgentMiddlewareDelegate pipeline = terminal;

        foreach (var middleware in _middlewares.Reverse())
        {
            var current = middleware;
            var next = pipeline;
            pipeline = (ctx, ct) => current.InvokeAsync(ctx, next, ct);
        }

        return pipeline(context, cancellationToken);
    }
}
