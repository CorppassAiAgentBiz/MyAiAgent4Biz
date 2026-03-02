namespace AiAgent.Core.Models;

/// <summary>
/// 記錄單次工具呼叫的完整資訊，包含工具名稱、傳入參數、執行結果及呼叫時間。
/// 此記錄會保存於 <see cref="AgentResponse.ToolCalls"/> 清單中，供後續查詢使用。
/// </summary>
public sealed class ToolCallRecord
{
    /// <summary>取得被呼叫的工具名稱。</summary>
    public required string ToolName { get; init; }

    /// <summary>取得傳入工具的參數字典（鍵值對）。</summary>
    public required IReadOnlyDictionary<string, string> Arguments { get; init; }

    /// <summary>取得工具執行後回傳的結果物件，包含輸出內容與成功狀態。</summary>
    public required ToolResult Result { get; init; }

    /// <summary>取得工具被呼叫時的 UTC 時間戳記。預設為物件建立當下的時間。</summary>
    public DateTimeOffset CalledAt { get; init; } = DateTimeOffset.UtcNow;
}
