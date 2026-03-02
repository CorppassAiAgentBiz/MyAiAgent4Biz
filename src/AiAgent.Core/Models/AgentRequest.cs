namespace AiAgent.Core.Models;

/// <summary>
/// 傳送給 Agent 的請求物件，包含工作階段識別碼、使用者訊息及附加中繼資料。
/// </summary>
public sealed class AgentRequest
{
    /// <summary>取得工作階段的唯一識別碼，用於關聯對話歷史記錄。此為必填欄位。</summary>
    public required string SessionId { get; init; }

    /// <summary>取得使用者輸入的訊息內容。此為必填欄位。</summary>
    public required string UserMessage { get; init; }

    /// <summary>
    /// 取得附加的中繼資料字典，可用於傳遞自訂的額外資訊（如使用者 ID、語言偏好等）。
    /// 預設為空字典。
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
