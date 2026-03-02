using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Llm;

/// <summary>
/// 視覺 LLM 提供者接口 - 用於分析圖片內容
/// </summary>
public interface IVisionLlmProvider
{
    /// <summary>
    /// 分析圖片並返回描述文字
    /// </summary>
    /// <param name="imageBase64">Base64 編碼的圖片數據</param>
    /// <param name="prompt">分析提示詞</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>圖片分析結果</returns>
    Task<string> AnalyzeImageAsync(string imageBase64, string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 視覺提供者名稱
    /// </summary>
    string ProviderName { get; }
}
