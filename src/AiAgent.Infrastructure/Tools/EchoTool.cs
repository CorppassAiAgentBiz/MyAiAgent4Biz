using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Tools;

/// <summary>
/// 內建工具：將輸入的訊息原封不動地回傳給呼叫端。
/// <para>
/// 此工具主要用於測試 Agent 的工具呼叫流程是否正常運作，
/// 也可作為實作其他工具的入門範例。
/// </para>
/// </summary>
public sealed class EchoTool : ITool
{
    /// <summary>取得此工具的唯一名稱。固定為「echo」。</summary>
    public string Name => "echo";

    /// <summary>取得此工具的功能描述，用於向 LLM 說明此工具的用途。</summary>
    public string Description => "Echoes the provided message back to the caller.";

    /// <summary>
    /// 取得此工具的參數定義字典。
    /// 包含一個名為「message」的參數，其說明為「The message to echo.」。
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; } =
        new Dictionary<string, string> { ["message"] = "The message to echo." };

    /// <summary>
    /// 以非同步方式執行工具，回傳傳入的「message」參數值。
    /// 若未提供「message」參數，則回傳「(empty)」字串。
    /// </summary>
    /// <param name="arguments">工具執行參數，應包含鍵為「message」的字串值。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    /// <returns>包含 Echo 訊息內容的成功結果。</returns>
    public Task<ToolResult> ExecuteAsync(IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken = default)
    {
        arguments.TryGetValue("message", out var message);
        return Task.FromResult(ToolResult.Success(Name, message ?? "(empty)"));
    }
}
