using AiAgent.Infrastructure.Tools;
using Xunit;

namespace AiAgent.Tests;

/// <summary>
/// 單元測試：驗證 <see cref="EchoTool"/> 的執行行為。
/// </summary>
public class EchoToolTests
{
    /// <summary>
    /// 驗證傳入「message」參數時，工具能正確回傳相同的字串內容。
    /// </summary>
    [Fact]
    public async Task Execute_ReturnsEchoedMessage()
    {
        var tool = new EchoTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, string> { ["message"] = "hello" });
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Output);
    }

    /// <summary>
    /// 驗證未傳入「message」參數時，工具能回傳「(empty)」字串而不拋出例外。
    /// </summary>
    [Fact]
    public async Task Execute_MissingArgument_ReturnsEmpty()
    {
        var tool = new EchoTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, string>());
        Assert.True(result.IsSuccess);
        Assert.Equal("(empty)", result.Output);
    }
}
