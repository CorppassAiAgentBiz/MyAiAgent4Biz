using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 儲存庫模式（Repository）：抽象化 Agent 記憶體與對話歷史儲存的介面。
/// 透過此介面可自由替換底層儲存媒介（如記憶體、資料庫、Redis 等），
/// 而無需修改 Agent 本身的邏輯。
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// 以非同步方式將鍵值對儲存至記憶體中。若鍵已存在則覆蓋。
    /// </summary>
    /// <param name="key">用來識別儲存值的唯一鍵。</param>
    /// <param name="value">要儲存的字串值。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    Task SaveAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以非同步方式依鍵取得對應的字串值。
    /// </summary>
    /// <param name="key">用來識別儲存值的唯一鍵。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>找到時回傳對應的字串值；找不到時回傳 <see langword="null"/>。</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以非同步方式取得指定工作階段的完整對話歷史，依加入順序排列。
    /// </summary>
    /// <param name="sessionId">工作階段的唯一識別碼。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>依序排列的 <see cref="ConversationEntry"/> 唯讀清單。</returns>
    Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以非同步方式將一筆對話條目附加至指定工作階段的歷史記錄末尾。
    /// </summary>
    /// <param name="sessionId">工作階段的唯一識別碼。</param>
    /// <param name="entry">要附加的對話條目。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    Task AppendHistoryAsync(string sessionId, ConversationEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以非同步方式清除指定工作階段的所有對話歷史記錄。
    /// </summary>
    /// <param name="sessionId">工作階段的唯一識別碼。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    Task ClearHistoryAsync(string sessionId, CancellationToken cancellationToken = default);
}
