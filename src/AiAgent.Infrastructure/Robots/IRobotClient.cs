namespace AiAgent.Infrastructure.Robots;

/// <summary>
/// 機器人客戶端接口 - 用於與遠程機器人通過 HTTP/WebSocket 通信
/// </summary>
public interface IRobotClient
{
    /// <summary>
    /// 執行機器人命令
    /// </summary>
    /// <param name="command">命令名稱（如 move_forward, turn_left 等）</param>
    /// <param name="parameters">命令參數（如 distance=10, angle=45）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令執行結果</returns>
    Task<string> ExecuteCommandAsync(string command, string parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查機器人是否已連接
    /// </summary>
    Task<bool> IsConnectedAsync();

    /// <summary>
    /// 機器人客戶端名稱
    /// </summary>
    string ClientName { get; }
}
