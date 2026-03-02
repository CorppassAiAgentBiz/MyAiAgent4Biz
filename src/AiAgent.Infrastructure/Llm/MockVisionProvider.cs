namespace AiAgent.Infrastructure.Llm;

/// <summary>
/// 模擬視覺提供者 - 用於開發和測試
/// 在實際使用時，應替換為真實的 Claude Vision 或 OpenAI Vision 實現
/// </summary>
public class MockVisionProvider : IVisionLlmProvider
{
    public string ProviderName => "Mock Vision Provider (for testing)";

    public Task<string> AnalyzeImageAsync(
        string imageBase64,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        // 簡單的模擬實現
        if (string.IsNullOrEmpty(imageBase64))
            return Task.FromResult("無效的圖片數據");

        // 根據圖片大小估計內容
        var sizeKb = imageBase64.Length / 1024;
        var sizeDesc = sizeKb > 500 ? "清晰度較高的" : sizeKb > 200 ? "中等清晰度的" : "較小的";

        // 返回自然語言的分析結果（不含技術標籤）
        var description = $"[圖片分析（大小：{sizeKb}KB）] 這是一張{sizeDesc}圖片。根據您的分析需求「{prompt}」，該圖片已被系統捕獲並儲存。\n\n在實際應用中，此處應使用 Claude Vision、GPT-4V 或其他視覺 AI 模型進行深度分析，以提供圖片內容、物體識別、文字提取等詳細信息。";

        return Task.FromResult(description);
    }
}
