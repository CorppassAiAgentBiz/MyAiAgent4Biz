namespace AiAgent.Core.Models;

/// <summary>
/// 工具執行後回傳的結果物件，包含輸出內容、執行成功旗標及錯誤訊息。
/// </summary>
public sealed class ToolResult
{
    /// <summary>取得產生此結果的工具名稱。</summary>
    public required string ToolName { get; init; }

    /// <summary>取得工具執行後輸出的文字內容。執行失敗時為空字串。</summary>
    public required string Output { get; init; }

    /// <summary>取得工具是否執行成功的旗標。預設為 <see langword="true"/>。</summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>取得工具執行失敗時的錯誤訊息。執行成功時為 <see langword="null"/>。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 建立一個表示執行成功的 <see cref="ToolResult"/> 實例。
    /// </summary>
    /// <param name="toolName">執行成功的工具名稱。</param>
    /// <param name="output">工具執行後的輸出文字。</param>
    /// <returns>已設定 <see cref="IsSuccess"/> 為 <see langword="true"/> 的結果物件。</returns>
    public static ToolResult Success(string toolName, string output) =>
        new() { ToolName = toolName, Output = output, IsSuccess = true };

    /// <summary>
    /// 建立一個表示執行失敗的 <see cref="ToolResult"/> 實例。
    /// </summary>
    /// <param name="toolName">執行失敗的工具名稱。</param>
    /// <param name="errorMessage">描述失敗原因的錯誤訊息。</param>
    /// <returns>已設定 <see cref="IsSuccess"/> 為 <see langword="false"/> 的結果物件。</returns>
    public static ToolResult Failure(string toolName, string errorMessage) =>
        new() { ToolName = toolName, Output = string.Empty, IsSuccess = false, ErrorMessage = errorMessage };
}
