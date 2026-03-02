using System.Collections.Concurrent;
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Memory;

/// <summary>
/// 儲存庫模式（Repository）：以應用程式記憶體儲存 Agent 對話歷史與鍵值資料的實作。
/// <para>
/// 使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 搭配 <see cref="ConcurrentQueue{T}"/>
/// 實現無鎖定（lock-free）的執行緒安全存取。適用於單一應用程式實例的開發與測試環境。
/// 正式環境可替換為 Redis、資料庫等持久化儲存的實作。
/// </para>
/// </summary>
public sealed class InMemoryMemoryStore : IMemoryStore
{
    /// <summary>執行緒安全的鍵值儲存字典，用於存放任意字串資料。</summary>
    private readonly ConcurrentDictionary<string, string> _keyValueStore = new();

    /// <summary>
    /// 執行緒安全的對話歷史字典，鍵為工作階段 ID，值為有序的對話條目佇列。
    /// 使用 <see cref="ConcurrentQueue{T}"/> 確保訊息按加入順序保存，且無需加鎖。
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConversationEntry>> _histories = new();

    /// <summary>
    /// 以非同步方式將鍵值對儲存至記憶體中。若鍵已存在則覆蓋。
    /// </summary>
    /// <param name="key">用來識別儲存值的唯一鍵。</param>
    /// <param name="value">要儲存的字串值。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _keyValueStore[key] = value;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 以非同步方式依鍵取得對應的字串值。
    /// </summary>
    /// <param name="key">用來識別儲存值的唯一鍵。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    /// <returns>找到時回傳對應的字串值；找不到時回傳 <see langword="null"/>。</returns>
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        _keyValueStore.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    /// <summary>
    /// 以非同步方式取得指定工作階段的完整對話歷史，依加入順序排列。
    /// </summary>
    /// <param name="sessionId">工作階段的唯一識別碼。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    /// <returns>依序排列的 <see cref="ConversationEntry"/> 陣列（以唯讀清單形式回傳）。</returns>
    public Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_histories.TryGetValue(sessionId, out var queue))
            return Task.FromResult<IReadOnlyList<ConversationEntry>>(queue.ToArray());
        return Task.FromResult<IReadOnlyList<ConversationEntry>>([]);
    }

    /// <summary>
    /// 以非同步方式將一筆對話條目附加至指定工作階段的歷史記錄末尾。
    /// 若該工作階段尚無歷史記錄，則自動建立新的佇列。
    /// </summary>
    /// <param name="sessionId">工作階段的唯一識別碼。</param>
    /// <param name="entry">要附加的對話條目。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    public Task AppendHistoryAsync(string sessionId, ConversationEntry entry, CancellationToken cancellationToken = default)
    {
        var queue = _histories.GetOrAdd(sessionId, _ => new ConcurrentQueue<ConversationEntry>());
        queue.Enqueue(entry);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 以非同步方式清除指定工作階段的所有對話歷史記錄。
    /// </summary>
    /// <param name="sessionId">工作階段的唯一識別碼。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖（此實作中未使用）。</param>
    public Task ClearHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _histories.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
