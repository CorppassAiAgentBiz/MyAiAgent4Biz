using AiAgent.Core.Models;

namespace AiAgent.Core.Abstractions;

/// <summary>
/// 策略模式（Strategy）：抽象化底層大型語言模型（LLM）後端的介面。
/// 透過此介面可自由替換不同的 LLM 實作（如 OpenAI、Azure OpenAI、Ollama 等），
/// 而無需修改 Agent 本身的邏輯。
/// </summary>
public interface ILlmProvider
{
    /// <summary>取得此 LLM 提供者的名稱，用於識別目前使用的後端。</summary>
    string ProviderName { get; }

    /// <summary>
    /// 以非同步方式將請求送至 LLM 並取得補全（Completion）回應。
    /// </summary>
    /// <param name="request">包含系統提示詞、對話歷史、使用者訊息及可用工具清單的請求物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>包含 LLM 回覆文字與可能工具呼叫資訊的 <see cref="LlmResponse"/>。</returns>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
