namespace AiAgent.Core.Models;

/// <summary>
/// 對話歷史中的單一條目，記錄對話角色、訊息內容及時間戳記。
/// </summary>
public sealed class ConversationEntry
{
    /// <summary>
    /// 取得此條目的發話角色。
    /// 合法值為：<c>"user"</c>（使用者）、<c>"assistant"</c>（Assistant 回覆）、<c>"tool"</c>（工具結果）。
    /// </summary>
    public required string Role { get; init; }

    /// <summary>取得此條目的文字內容。</summary>
    public required string Content { get; init; }

    /// <summary>
    /// 取得與此條目相關聯的工具名稱。
    /// 僅當 <see cref="Role"/> 為 <c>"tool"</c> 時有值；其他角色為 <see langword="null"/>。
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>取得此條目建立時的 UTC 時間戳記。預設為物件建立當下的時間。</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
