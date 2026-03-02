namespace AiAgent.Core.Models;

/// <summary>
/// Agent 執行後回傳的回應物件，包含回覆內容、執行狀態、工具呼叫記錄及附加中繼資料。
/// </summary>
public sealed class AgentResponse
{
    /// <summary>取得工作階段的唯一識別碼，與對應請求的 SessionId 相同。</summary>
    public required string SessionId { get; init; }

    /// <summary>取得 Agent 的最終回覆文字內容。執行失敗時為空字串。</summary>
    public required string Content { get; init; }

    /// <summary>取得執行是否成功的旗標。預設為 <see langword="true"/>。</summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>取得執行失敗時的錯誤訊息。執行成功時為 <see langword="null"/>。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>取得本次執行中所有工具呼叫的記錄清單。無工具呼叫時為空清單。</summary>
    public IReadOnlyList<ToolCallRecord> ToolCalls { get; init; } = [];

    /// <summary>
    /// 取得附加的中繼資料字典，可用於回傳自訂的額外資訊（如耗用的 Token 數等）。
    /// 預設為空字典。
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 建立一個表示執行成功的 <see cref="AgentResponse"/> 實例。
    /// </summary>
    /// <param name="sessionId">工作階段識別碼。</param>
    /// <param name="content">Agent 的回覆文字內容。</param>
    /// <param name="toolCalls">本次執行的工具呼叫記錄清單；若無則可傳入 <see langword="null"/>。</param>
    /// <returns>已設定 <see cref="IsSuccess"/> 為 <see langword="true"/> 的回應物件。</returns>
    public static AgentResponse Success(string sessionId, string content, IReadOnlyList<ToolCallRecord>? toolCalls = null) =>
        new() { SessionId = sessionId, Content = content, IsSuccess = true, ToolCalls = toolCalls ?? [] };

    /// <summary>
    /// 建立一個表示執行失敗的 <see cref="AgentResponse"/> 實例。
    /// </summary>
    /// <param name="sessionId">工作階段識別碼。</param>
    /// <param name="errorMessage">描述失敗原因的錯誤訊息。</param>
    /// <returns>已設定 <see cref="IsSuccess"/> 為 <see langword="false"/> 的回應物件。</returns>
    public static AgentResponse Failure(string sessionId, string errorMessage) =>
        new() { SessionId = sessionId, Content = string.Empty, IsSuccess = false, ErrorMessage = errorMessage };
}
