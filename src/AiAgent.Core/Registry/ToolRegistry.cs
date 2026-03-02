using System.Collections.Concurrent;
using AiAgent.Core.Abstractions;

namespace AiAgent.Core.Registry;

/// <summary>
/// 外掛/登錄模式（Plugin/Registry）：執行緒安全的工具登錄實作。
/// <para>
/// 使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 儲存工具，
/// 支援多執行緒環境下的並發存取，工具名稱比對不區分大小寫。
/// </para>
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    /// <summary>執行緒安全的工具字典，鍵為工具名稱（不區分大小寫），值為工具實例。</summary>
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 將指定工具註冊至工具登錄中。若已有相同名稱的工具，則覆蓋舊有項目。
    /// </summary>
    /// <param name="tool">要註冊的工具實例，不可為 <see langword="null"/>。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="tool"/> 為 <see langword="null"/> 時拋出。</exception>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// 嘗試依名稱取得已註冊的工具（不區分大小寫）。
    /// </summary>
    /// <param name="name">要查詢的工具名稱。</param>
    /// <param name="tool">若找到則輸出對應的工具實例；否則輸出 <see langword="null"/>。</param>
    /// <returns>找到工具時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    public bool TryGet(string name, out ITool? tool)
        => _tools.TryGetValue(name, out tool);

    /// <summary>
    /// 取得目前所有已註冊工具的唯讀快照清單。
    /// </summary>
    /// <returns>包含所有已註冊 <see cref="ITool"/> 實例的唯讀清單。</returns>
    public IReadOnlyList<ITool> GetAll()
        => _tools.Values.ToList();
}
