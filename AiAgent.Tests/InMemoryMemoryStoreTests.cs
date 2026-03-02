using AiAgent.Core.Models;
using AiAgent.Infrastructure.Memory;
using Xunit;

namespace AiAgent.Tests;

/// <summary>
/// 單元測試：驗證 <see cref="InMemoryMemoryStore"/> 的鍵值儲存與對話歷史功能。
/// </summary>
public class InMemoryMemoryStoreTests
{
    /// <summary>
    /// 驗證儲存鍵值對後，能正確取回相同的值。
    /// </summary>
    [Fact]
    public async Task SaveAndGet_ReturnsStoredValue()
    {
        var store = new InMemoryMemoryStore();
        await store.SaveAsync("key1", "value1");
        var result = await store.GetAsync("key1");
        Assert.Equal("value1", result);
    }

    /// <summary>
    /// 驗證查詢不存在的鍵時，回傳 <see langword="null"/> 而非拋出例外。
    /// </summary>
    [Fact]
    public async Task Get_UnknownKey_ReturnsNull()
    {
        var store = new InMemoryMemoryStore();
        var result = await store.GetAsync("missing");
        Assert.Null(result);
    }

    /// <summary>
    /// 驗證依序附加對話條目後，取回的歷史清單能保持正確的加入順序。
    /// </summary>
    [Fact]
    public async Task AppendHistory_And_GetHistory_ReturnsInOrder()
    {
        var store = new InMemoryMemoryStore();
        var entry1 = new ConversationEntry { Role = "user", Content = "Hello" };
        var entry2 = new ConversationEntry { Role = "assistant", Content = "Hi!" };
        await store.AppendHistoryAsync("session1", entry1);
        await store.AppendHistoryAsync("session1", entry2);

        var history = await store.GetHistoryAsync("session1");
        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("assistant", history[1].Role);
    }

    /// <summary>
    /// 驗證清除對話歷史後，取回的清單為空而非拋出例外。
    /// </summary>
    [Fact]
    public async Task ClearHistory_RemovesAllEntries()
    {
        var store = new InMemoryMemoryStore();
        await store.AppendHistoryAsync("session1", new ConversationEntry { Role = "user", Content = "Hi" });
        await store.ClearHistoryAsync("session1");
        var history = await store.GetHistoryAsync("session1");
        Assert.Empty(history);
    }
}
