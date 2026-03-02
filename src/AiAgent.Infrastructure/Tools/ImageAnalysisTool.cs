using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;
using AiAgent.Infrastructure.Llm;

namespace AiAgent.Infrastructure.Tools;

/// <summary>
/// 圖片分析工具 - 使用視覺 LLM 分析上傳的圖片
/// </summary>
public sealed class ImageAnalysisTool : ITool
{
    private readonly IVisionLlmProvider _visionProvider;

    public string Name => "analyze_image";

    public string Description => "分析上傳的圖片並提供內容描述和分析";

    public IReadOnlyDictionary<string, string> Parameters { get; } =
        new Dictionary<string, string>
        {
            ["image_base64"] = "Base64 編碼的圖片數據",
            ["prompt"] = "（可選）針對圖片的具體分析提示詞"
        };

    public ImageAnalysisTool(IVisionLlmProvider visionProvider)
    {
        _visionProvider = visionProvider ?? throw new ArgumentNullException(nameof(visionProvider));
    }

    public async Task<ToolResult> ExecuteAsync(
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!arguments.TryGetValue("image_base64", out var imageBase64) || string.IsNullOrWhiteSpace(imageBase64))
            {
                return ToolResult.Failure(Name, "未提供有效的圖片數據");
            }

            var prompt = arguments.TryGetValue("prompt", out var p) && !string.IsNullOrWhiteSpace(p)
                ? p
                : "詳細描述這張圖片的內容、顏色、文字、物體等所有可見的元素";

            var analysis = await _visionProvider.AnalyzeImageAsync(imageBase64, prompt, cancellationToken);

            return ToolResult.Success(Name, analysis);
        }
        catch (Exception ex)
        {
            return ToolResult.Failure(Name, $"圖片分析失敗：{ex.Message}");
        }
    }
}
