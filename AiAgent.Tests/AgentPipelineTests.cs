using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;
using AiAgent.Core.Pipeline;
using AiAgent.Infrastructure.Middleware;
using Xunit;

namespace AiAgent.Tests;

/// <summary>
/// 單元測試：驗證 <see cref="AgentPipeline"/> 中介軟體管線的行為。
/// 涵蓋驗證阻擋、正常通過及有序執行等場景。
/// </summary>
public class AgentPipelineTests
{
    /// <summary>
    /// 建立用於測試的 <see cref="AgentContext"/>，內含指定的訊息與工作階段 ID。
    /// </summary>
    /// <param name="message">使用者訊息，預設為「test」。</param>
    /// <param name="session">工作階段 ID，預設為「s1」。</param>
    /// <returns>已初始化的 <see cref="AgentContext"/> 執行個體。</returns>
    private static AgentContext MakeContext(string message = "test", string session = "s1")
    {
        var mockAgent = new FakeAgent();
        return new AgentContext
        {
            Request = new AgentRequest { SessionId = session, UserMessage = message },
            Agent = mockAgent
        };
    }

    /// <summary>
    /// 驗證 <see cref="ValidationMiddleware"/> 在使用者訊息為空白時，
    /// 能攔截請求並回傳包含「empty」關鍵字的失敗回應。
    /// </summary>
    [Fact]
    public async Task Pipeline_ValidationMiddleware_BlocksEmptyMessage()
    {
        var pipeline = new AgentPipeline([new ValidationMiddleware()]);
        var context = MakeContext(message: "  ");

        var response = await pipeline.ExecuteAsync(context, (ctx, ct) =>
            Task.FromResult(AgentResponse.Success(ctx.Request.SessionId, "ok")));

        Assert.False(response.IsSuccess);
        Assert.Contains("empty", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 驗證 <see cref="ValidationMiddleware"/> 在使用者訊息合法時，
    /// 能讓請求通過並由終端委派正常產生成功回應。
    /// </summary>
    [Fact]
    public async Task Pipeline_ValidMessage_PassesThrough()
    {
        var pipeline = new AgentPipeline([new ValidationMiddleware()]);
        var context = MakeContext(message: "hello");

        var response = await pipeline.ExecuteAsync(context, (ctx, ct) =>
            Task.FromResult(AgentResponse.Success(ctx.Request.SessionId, "ok")));

        Assert.True(response.IsSuccess);
        Assert.Equal("ok", response.Content);
    }

    /// <summary>
    /// 驗證管線會依 <see cref="IAgentMiddleware.Order"/> 從小到大依序執行中介軟體，
    /// 即使輸入時順序是反的也應正確排序。
    /// </summary>
    [Fact]
    public async Task Pipeline_OrderedMiddleware_RunsInOrder()
    {
        var executionOrder = new List<string>();
        var m1 = new OrderedMiddleware("first", 1, executionOrder);
        var m2 = new OrderedMiddleware("second", 2, executionOrder);

        // 故意以反序傳入，驗證管線會自動按 Order 排序
        var pipeline = new AgentPipeline([m2, m1]);
        var context = MakeContext();

        await pipeline.ExecuteAsync(context, (ctx, ct) =>
            Task.FromResult(AgentResponse.Success(ctx.Request.SessionId, "done")));

        Assert.Equal(["first", "second"], executionOrder);
    }

    /// <summary>用於測試的假 Agent，不執行任何實際邏輯，直接回傳固定回應。</summary>
    private sealed class FakeAgent : IAgent
    {
        /// <summary>取得假 Agent 的名稱。固定為「fake」。</summary>
        public string Name => "fake";

        /// <summary>取得假 Agent 的描述。固定為「fake agent」。</summary>
        public string Description => "fake agent";

        /// <summary>直接回傳固定的成功回應，不執行任何 LLM 或工具邏輯。</summary>
        public Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(AgentResponse.Success(request.SessionId, "fake"));
    }

    /// <summary>
    /// 用於驗證執行順序的測試用中介軟體，執行時將名稱加入日誌清單。
    /// </summary>
    private sealed class OrderedMiddleware : IAgentMiddleware
    {
        /// <summary>此中介軟體的識別名稱，用於記錄執行順序。</summary>
        private readonly string _name;

        /// <summary>用於記錄執行順序的共享日誌清單。</summary>
        private readonly List<string> _log;

        /// <summary>取得此中介軟體在管線中的執行順序。</summary>
        public int Order { get; }

        /// <summary>
        /// 初始化 <see cref="OrderedMiddleware"/> 的新執行個體。
        /// </summary>
        /// <param name="name">此中介軟體的識別名稱。</param>
        /// <param name="order">執行順序數值。</param>
        /// <param name="log">用於記錄執行順序的共享清單。</param>
        public OrderedMiddleware(string name, int order, List<string> log)
        {
            _name = name;
            Order = order;
            _log = log;
        }

        /// <summary>將自身名稱加入日誌後，繼續傳遞至管線下一節點。</summary>
        public async Task<AgentResponse> InvokeAsync(AgentContext context, AgentMiddlewareDelegate next, CancellationToken cancellationToken = default)
        {
            _log.Add(_name);
            return await next(context, cancellationToken);
        }
    }
}
