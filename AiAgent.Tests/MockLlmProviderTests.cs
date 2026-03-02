using AiAgent.Core.Models;
using AiAgent.Infrastructure.Llm;
using Xunit;

namespace AiAgent.Tests;

/// <summary>
/// 單元測試：驗證 <see cref="MockLlmProvider"/> 的預設與自訂回應行為。
/// </summary>
public class MockLlmProviderTests
{
    /// <summary>
    /// 驗證未注入工廠函式時，預設行為會回傳包含使用者訊息的 Echo 回應。
    /// </summary>
    [Fact]
    public async Task CompleteAsync_DefaultFactory_EchoesUserMessage()
    {
        var provider = new MockLlmProvider();
        var request = new LlmRequest
        {
            SystemPrompt = "You are helpful.",
            History = [],
            UserMessage = "Hello!"
        };

        var response = await provider.CompleteAsync(request);
        Assert.Contains("Hello!", response.Content);
        Assert.False(response.HasToolCall);
    }

    /// <summary>
    /// 驗證注入自訂工廠函式時，回應內容由工廠函式決定而非預設邏輯。
    /// </summary>
    [Fact]
    public async Task CompleteAsync_CustomFactory_UsesFactory()
    {
        var provider = new MockLlmProvider(_ => new LlmResponse { Content = "custom response" });
        var request = new LlmRequest
        {
            SystemPrompt = "sys",
            History = [],
            UserMessage = "input"
        };

        var response = await provider.CompleteAsync(request);
        Assert.Equal("custom response", response.Content);
    }
}
