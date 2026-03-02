using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 觀察者模式（Observer）：處理 Agent 生命週期事件的介面。
/// 實作此介面可監聽 Agent 的啟動、完成、錯誤及工具呼叫等事件。
/// </summary>
public interface IAgentEventHandler
{
    /// <summary>
    /// 當 Agent 開始執行時觸發。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    Task OnAgentStartedAsync(AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 當 Agent 成功完成執行時觸發。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="response">Agent 執行後產生的回應物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    Task OnAgentCompletedAsync(AgentContext context, AgentResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// 當 Agent 執行發生例外時觸發。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="exception">造成錯誤的例外物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    Task OnAgentErrorAsync(AgentContext context, Exception exception, CancellationToken cancellationToken = default);

    /// <summary>
    /// 當 Agent 呼叫外部工具後觸發。
    /// </summary>
    /// <param name="context">包含本次請求與 Agent 參考的執行上下文。</param>
    /// <param name="toolName">被呼叫的工具名稱。</param>
    /// <param name="arguments">傳入工具的參數字典。</param>
    /// <param name="result">工具執行後的結果物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    Task OnToolCalledAsync(AgentContext context, string toolName, IReadOnlyDictionary<string, string> arguments, ToolResult result, CancellationToken cancellationToken = default);
}
