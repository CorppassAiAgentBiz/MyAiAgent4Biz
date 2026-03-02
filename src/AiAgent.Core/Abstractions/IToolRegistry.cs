using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 外掛/登錄模式（Plugin/Registry）：管理 Agent 可用工具的登錄介面。
/// 工具可在執行時期動態註冊，Agent 可透過此介面查詢與取得工具實例。
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 將指定工具註冊至工具登錄中。若已有相同名稱的工具，則覆蓋舊有項目。
    /// </summary>
    /// <param name="tool">要註冊的工具實例，不可為 <see langword="null"/>。</param>
    void Register(ITool tool);

    /// <summary>
    /// 嘗試依名稱取得已註冊的工具。
    /// </summary>
    /// <param name="name">要查詢的工具名稱（不區分大小寫）。</param>
    /// <param name="tool">若找到則輸出對應的工具實例；否則輸出 <see langword="null"/>。</param>
    /// <returns>找到工具時回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    bool TryGet(string name, out ITool? tool);

    /// <summary>
    /// 取得目前所有已註冊工具的唯讀清單。
    /// </summary>
    /// <returns>包含所有已註冊 <see cref="ITool"/> 實例的唯讀清單。</returns>
    IReadOnlyList<ITool> GetAll();
}
