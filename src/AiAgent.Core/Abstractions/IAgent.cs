using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 代表一個 AI Agent，能夠處理使用者訊息並呼叫工具以完成任務。
/// </summary>
public interface IAgent
{
    /// <summary>取得 Agent 的唯一名稱。</summary>
    string Name { get; }

    /// <summary>取得 Agent 的功能描述。</summary>
    string Description { get; }

    /// <summary>
    /// 以非同步方式執行 Agent，處理使用者請求並回傳回應。
    /// </summary>
    /// <param name="request">包含對話工作階段 ID 及使用者訊息的請求物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>包含 Agent 回覆內容與執行結果的 <see cref="AgentResponse"/>。</returns>
    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default);
}
