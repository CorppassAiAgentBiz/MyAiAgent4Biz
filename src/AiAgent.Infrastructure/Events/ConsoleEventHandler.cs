using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Events;

/// <summary>
/// 觀察者模式（Observer）：將 Agent 生命週期事件透過可注入的記錄委派輸出的事件處理器實作。
/// <para>
/// 適用於開發期間的除錯與監控。正式環境可替換為寫入結構化日誌（如 Serilog、NLog）的實作。
/// </para>
/// </summary>
public sealed class ConsoleEventHandler : IAgentEventHandler
{
    /// <summary>
    /// 用於輸出事件訊息的記錄委派函式。
    /// </summary>
    private readonly Action<string>? _logger;

    /// <summary>
    /// 初始化 <see cref="ConsoleEventHandler"/> 的新執行個體。
    /// </summary>
    /// <param name="logger">
    /// 選擇性的自訂記錄函式。若為 <see langword="null"/>，則不輸出任何訊息。
    /// </param>
    public ConsoleEventHandler(Action<string>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 當 Agent 開始執行時，若已設定記錄委派，則將工作階段 ID 傳遞至記錄委派。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    public Task OnAgentStartedAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        _logger?.Invoke($"[Event] Agent started. Session={context.Request.SessionId}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 當 Agent 成功完成執行時，若已設定記錄委派，則將執行結果與工具呼叫次數傳遞至記錄委派。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="response">Agent 執行後產生的回應物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    public Task OnAgentCompletedAsync(AgentContext context, AgentResponse response, CancellationToken cancellationToken = default)
    {
        _logger?.Invoke($"[Event] Agent completed. Success={response.IsSuccess}, ToolCalls={response.ToolCalls.Count}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 當 Agent 執行發生例外時，若已設定記錄委派，則將錯誤訊息傳遞至記錄委派。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="exception">造成錯誤的例外物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    public Task OnAgentErrorAsync(AgentContext context, Exception exception, CancellationToken cancellationToken = default)
    {
        _logger?.Invoke($"[Event] Agent error: {exception.Message}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 當 Agent 呼叫工具後，若已設定記錄委派，則將工具名稱與執行結果傳遞至記錄委派。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="toolName">被呼叫的工具名稱。</param>
    /// <param name="arguments">傳入工具的參數字典。</param>
    /// <param name="result">工具執行後的結果物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    public Task OnToolCalledAsync(AgentContext context, string toolName, IReadOnlyDictionary<string, string> arguments, ToolResult result, CancellationToken cancellationToken = default)
    {
        _logger?.Invoke($"[Event] Tool '{toolName}' called. Success={result.IsSuccess}");
        return Task.CompletedTask;
    }
}
