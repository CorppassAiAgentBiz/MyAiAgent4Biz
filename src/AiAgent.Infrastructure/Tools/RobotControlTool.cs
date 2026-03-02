using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;
using AiAgent.Infrastructure.Robots;

namespace AiAgent.Infrastructure.Tools;

/// <summary>
/// 機器人控制工具 - 用於控制遠程機器人
/// </summary>
public sealed class RobotControlTool : ITool
{
    private readonly IRobotClient _robotClient;

    public string Name => "control_robot";

    public string Description => "控制遠程機器人執行各種動作命令";

    public IReadOnlyDictionary<string, string> Parameters { get; } =
        new Dictionary<string, string>
        {
            ["command"] = "機器人命令名稱 (move_forward, move_backward, turn_left, turn_right, stop, grab, release 等)",
            ["parameters"] = "（可選）命令參數 (如 distance=10, angle=45, speed=50)"
        };

    public RobotControlTool(IRobotClient robotClient)
    {
        _robotClient = robotClient ?? throw new ArgumentNullException(nameof(robotClient));
    }

    public async Task<ToolResult> ExecuteAsync(
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 驗證連接
            var isConnected = await _robotClient.IsConnectedAsync();
            if (!isConnected)
            {
                return ToolResult.Failure(Name, $"無法連接到機器人（{_robotClient.ClientName}）。請檢查機器人服務器是否正常運行。");
            }

            // 獲取命令和參數
            if (!arguments.TryGetValue("command", out var command) || string.IsNullOrWhiteSpace(command))
            {
                return ToolResult.Failure(Name, "未指定機器人命令");
            }

            var parameters = arguments.TryGetValue("parameters", out var p) ? p : "";

            // 執行命令
            var result = await _robotClient.ExecuteCommandAsync(command, parameters, cancellationToken);

            // 檢查結果
            if (result.Contains("失敗") || result.Contains("錯誤"))
            {
                return ToolResult.Failure(Name, result);
            }

            return ToolResult.Success(Name, result);
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Failure(Name, "機器人命令執行超時");
        }
        catch (Exception ex)
        {
            return ToolResult.Failure(Name, $"機器人控制異常：{ex.Message}");
        }
    }
}
