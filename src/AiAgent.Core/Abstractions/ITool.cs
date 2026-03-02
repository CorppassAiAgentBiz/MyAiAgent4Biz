using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 代表 Agent 可呼叫的一個工具（技能）。
/// 工具封裝了特定的外部能力（如查詢時間、呼叫 API 等），
/// 並由 <see cref="IToolRegistry"/> 管理與分派。
/// </summary>
public interface ITool
{
    /// <summary>取得工具的唯一名稱，供 LLM 及 Agent 識別使用。</summary>
    string Name { get; }

    /// <summary>取得工具的功能描述，用於向 LLM 說明此工具的用途。</summary>
    string Description { get; }

    /// <summary>
    /// 取得此工具所接受的參數定義字典。
    /// 鍵為參數名稱，值為參數說明。
    /// </summary>
    IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// 以非同步方式執行工具，並回傳執行結果。
    /// </summary>
    /// <param name="arguments">以字典形式傳入的具體執行參數（鍵值對）。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>包含工具輸出與執行狀態的 <see cref="ToolResult"/>。</returns>
    Task<ToolResult> ExecuteAsync(IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken = default);
}
