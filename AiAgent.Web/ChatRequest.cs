namespace AiAgent.Web;

/// <summary>
/// 聊天請求模型
/// </summary>
public record ChatRequest(
    string SessionId,
    string Message,
    string? ImageBase64 = null);

