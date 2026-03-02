using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Tools;

/// <summary>
/// 內建工具：回傳目前的 UTC 日期與時間。
/// <para>
/// 此工具不需要任何輸入參數，適合作為 Agent 查詢目前時間的能力範例。
/// </para>
/// </summary>
public sealed class CurrentTimeTool : ITool
{
    /// <summary>取得此工具的唯一名稱。固定為「get_current_time」。</summary>
    public string Name => "get_current_time";

    /// <summary>取得此工具的功能描述，用於向 LLM 說明此工具的用途。</summary>
    public string Description => "Returns the current UTC date and time.";

    /// <summary>取得此工具的參數定義字典。此工具不需要任何參數，固定回傳空字典。</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; } = new Dictionary<string, string>();

    /// <summary>
    /// 以非同步方式取得目前的 UTC 時間，並以 ISO 8601 格式回傳。
    /// </summary>
    /// <param name="arguments">工具執行參數（此工具不使用任何參數）。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    /// <returns>包含目前 UTC 時間字串（ISO 8601 格式）的成功結果。</returns>
    public Task<ToolResult> ExecuteAsync(IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken = default)
        => Task.FromResult(ToolResult.Success(Name, DateTimeOffset.UtcNow.ToString("O")));
}
