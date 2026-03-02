using System.Net.Http.Json;

namespace AiAgent.Infrastructure.Robots;

/// <summary>
/// HTTP 機器人客戶端 - 通過 REST API 與遠程機器人通信
/// </summary>
public sealed class HttpRobotClient : IRobotClient
{
    private readonly HttpClient _httpClient;
    private readonly string _robotBaseUrl;

    public string ClientName => $"HTTP Robot Client ({_robotBaseUrl})";

    public HttpRobotClient(HttpClient httpClient, string robotBaseUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _robotBaseUrl = robotBaseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(robotBaseUrl));
    }

    public async Task<string> ExecuteCommandAsync(
        string command,
        string parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 構建命令請求
            var requestBody = new
            {
                action = command,
                parameters = parameters
            };

            // 發送 POST 請求到機器人 API
            var response = await _httpClient.PostAsJsonAsync(
                $"{_robotBaseUrl}/api/command",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return $"機器人命令失敗 ({response.StatusCode}): {response.ReasonPhrase}";
            }

            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            return result;
        }
        catch (HttpRequestException ex)
        {
            return $"機器人通信失敗：{ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "機器人命令超時";
        }
        catch (Exception ex)
        {
            return $"機器人執行錯誤：{ex.Message}";
        }
    }

    public async Task<bool> IsConnectedAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_robotBaseUrl}/api/health", HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
