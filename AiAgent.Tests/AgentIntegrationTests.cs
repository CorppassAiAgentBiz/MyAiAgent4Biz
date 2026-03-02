using AiAgent.Core.Factory;
using AiAgent.Core.Models;
using AiAgent.Core.Registry;
using AiAgent.Infrastructure.Events;
using AiAgent.Infrastructure.Llm;
using AiAgent.Infrastructure.Memory;
using AiAgent.Infrastructure.Tools;
using Xunit;

namespace AiAgent.Tests;

/// <summary>
/// 整合測試：驗證 Agent 在完整元件組合下的端對端行為。
/// 包含簡單訊息回覆、工具呼叫、對話歷史持久化及未知工具的容錯處理。
/// </summary>
public class AgentIntegrationTests
{
    /// <summary>
    /// 建立整合測試所需的完整元件組合（工廠、工具登錄、記憶體儲存）。
    /// </summary>
    /// <param name="llmFactory">
    /// 選擇性的 LLM 回應工廠函式。若為 <see langword="null"/> 則使用預設的 Echo 行為。
    /// </param>
    /// <returns>包含 AgentFactory、ToolRegistry 及 InMemoryMemoryStore 的元組。</returns>
    private static (AgentFactory factory, ToolRegistry registry, InMemoryMemoryStore memory) BuildComponents(
        Func<LlmRequest, LlmResponse>? llmFactory = null)
    {
        var llm = new MockLlmProvider(llmFactory);
        var registry = new ToolRegistry();
        var memory = new InMemoryMemoryStore();
        var handlers = new[] { new ConsoleEventHandler() };
        var factory = new AgentFactory(llm, registry, memory, handlers);
        return (factory, registry, memory);
    }

    /// <summary>
    /// 驗證 Agent 處理簡單訊息時，能回傳包含使用者訊息內容的成功回應。
    /// </summary>
    [Fact]
    public async Task Agent_SimpleMessage_ReturnsSuccessResponse()
    {
        var (factory, _, _) = BuildComponents();
        var agent = factory.Create(new AgentOptions { Name = "TestAgent", SystemPrompt = "You are helpful." });

        var response = await agent.RunAsync(new AgentRequest
        {
            SessionId = "session-001",
            UserMessage = "Hello!"
        });

        Assert.True(response.IsSuccess);
        Assert.Contains("Hello!", response.Content);
    }

    /// <summary>
    /// 驗證 Agent 在 LLM 請求工具呼叫時，能正確執行指定工具並記錄呼叫結果。
    /// </summary>
    [Fact]
    public async Task Agent_ToolCall_ExecutesTool()
    {
        // 第一次呼叫 LLM 時回傳工具呼叫請求，第二次回傳最終文字回應
        bool toolCallPhase = true;
        var (factory, registry, _) = BuildComponents(req =>
        {
            if (toolCallPhase)
            {
                toolCallPhase = false;
                return new LlmResponse
                {
                    Content = string.Empty,
                    ToolCall = new ToolCallRequest { ToolName = "echo", Arguments = new Dictionary<string, string> { ["message"] = "world" } }
                };
            }
            return new LlmResponse { Content = "Done with tool result." };
        });

        registry.Register(new EchoTool());
        var agent = factory.Create(new AgentOptions { Name = "ToolAgent", SystemPrompt = "Use tools." });

        var response = await agent.RunAsync(new AgentRequest
        {
            SessionId = "session-002",
            UserMessage = "Echo world"
        });

        Assert.True(response.IsSuccess);
        Assert.Single(response.ToolCalls);
        Assert.Equal("echo", response.ToolCalls[0].ToolName);
        Assert.Equal("world", response.ToolCalls[0].Result.Output);
    }

    /// <summary>
    /// 驗證 Agent 執行後，使用者訊息與 Agent 回覆能正確持久化至記憶體儲存中。
    /// </summary>
    [Fact]
    public async Task Agent_ConversationHistory_PersistedAcrossRuns()
    {
        var (factory, _, memory) = BuildComponents();
        var agent = factory.Create(new AgentOptions { Name = "MemAgent" });
        var sessionId = "session-003";

        await agent.RunAsync(new AgentRequest { SessionId = sessionId, UserMessage = "First message" });
        var history = await memory.GetHistoryAsync(sessionId);

        // 期望歷史中有兩筆：使用者訊息 + Agent 回覆
        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("First message", history[0].Content);
    }

    /// <summary>
    /// 驗證 Agent 在 LLM 請求呼叫不存在的工具時，能優雅地處理錯誤而不拋出例外。
    /// </summary>
    [Fact]
    public async Task Agent_UnknownTool_HandledGracefully()
    {
        bool firstCall = true;
        var (factory, _, _) = BuildComponents(req =>
        {
            if (firstCall)
            {
                firstCall = false;
                return new LlmResponse
                {
                    Content = string.Empty,
                    ToolCall = new ToolCallRequest { ToolName = "nonexistent_tool", Arguments = new Dictionary<string, string>() }
                };
            }
            return new LlmResponse { Content = "Handled gracefully." };
        });

        var agent = factory.Create(new AgentOptions { Name = "GraceAgent" });
        var response = await agent.RunAsync(new AgentRequest { SessionId = "session-004", UserMessage = "Use unknown tool" });

        // Agent 整體應成功（不拋出例外），但工具呼叫結果應為失敗
        Assert.True(response.IsSuccess);
        Assert.Single(response.ToolCalls);
        Assert.False(response.ToolCalls[0].Result.IsSuccess);
    }
}
