using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Core.Agents;

/// <summary>
/// 範本方法模式（Template Method）：定義 Agent 執行演算法骨架的抽象基底類別。
/// <para>
/// 固定執行流程為：<b>感知（Perceive）→ 規劃（Plan）→ 行動（Act）→ 反思（Reflect）</b>。
/// 子類別可覆寫各個步驟方法以自訂行為，但整體流程順序由此基底類別掌控。
/// </para>
/// </summary>
public abstract class BaseAgent : IAgent
{
    /// <summary>用於向 LLM 發送請求的提供者（策略模式實作）。</summary>
    private readonly ILlmProvider _llmProvider;

    /// <summary>用於查詢與執行工具的工具登錄（外掛/登錄模式實作）。</summary>
    private readonly IToolRegistry _toolRegistry;

    /// <summary>用於儲存與讀取對話歷史的記憶體儲存（儲存庫模式實作）。</summary>
    private readonly IMemoryStore _memoryStore;

    /// <summary>訂閱 Agent 生命週期事件的處理器清單（觀察者模式實作）。</summary>
    private readonly IReadOnlyList<IAgentEventHandler> _eventHandlers;

    /// <summary>此 Agent 的設定選項，包含名稱、描述、系統提示詞及最大迭代次數。</summary>
    private readonly AgentOptions _options;

    /// <summary>
    /// 初始化 <see cref="BaseAgent"/> 的新執行個體。
    /// </summary>
    /// <param name="options">包含 Agent 名稱、描述、系統提示詞等設定的選項物件。</param>
    /// <param name="llmProvider">用於向 LLM 發送補全請求的提供者。</param>
    /// <param name="toolRegistry">管理 Agent 可用工具的工具登錄。</param>
    /// <param name="memoryStore">負責儲存對話歷史的記憶體儲存。</param>
    /// <param name="eventHandlers">訂閱生命週期事件的處理器集合。</param>
    protected BaseAgent(
        AgentOptions options,
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IMemoryStore memoryStore,
        IEnumerable<IAgentEventHandler> eventHandlers)
    {
        _options = options;
        _llmProvider = llmProvider;
        _toolRegistry = toolRegistry;
        _memoryStore = memoryStore;
        _eventHandlers = eventHandlers.ToList();
    }

    /// <summary>取得 Agent 的唯一名稱（來自 <see cref="AgentOptions.Name"/>）。</summary>
    public string Name => _options.Name;

    /// <summary>取得 Agent 的功能描述（來自 <see cref="AgentOptions.Description"/>）。</summary>
    public string Description => _options.Description;

    /// <summary>
    /// 主要執行入口點（範本方法）。
    /// 依序執行感知→規劃→行動→反思流程，並通知所有已訂閱的事件處理器。
    /// </summary>
    /// <param name="request">包含工作階段 ID 及使用者訊息的請求物件。</param>
    /// <param name="cancellationToken">用於取消非同步作業的取消權杖。</param>
    /// <returns>包含最終回覆內容與工具呼叫記錄的 <see cref="AgentResponse"/>。</returns>
    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        var context = new AgentContext { Request = request, Agent = this };

        await NotifyStartedAsync(context, cancellationToken);

        try
        {
            // 步驟一：感知（Perceive）——從記憶體載入對話歷史，理解輸入上下文
            var history = await PerceiveAsync(request, cancellationToken);

            // 步驟二：規劃（Plan）——呼叫 LLM 決定下一步行動（循環支援工具呼叫）
            var toolCalls = new List<ToolCallRecord>();
            string finalContent = string.Empty;

            for (int iteration = 0; iteration < _options.MaxIterations; iteration++)
            {
                var llmRequest = BuildLlmRequest(request, history);
                var llmResponse = await PlanAsync(llmRequest, cancellationToken);

                if (!llmResponse.HasToolCall)
                {
                    // 步驟四：反思（Reflect）——對 LLM 的最終輸出進行後處理
                    finalContent = await ReflectAsync(llmResponse.Content, cancellationToken);
                    break;
                }

                // 步驟三：行動（Act）——執行 LLM 所請求的工具
                var toolCallRecord = await ActAsync(context, llmResponse.ToolCall!, cancellationToken);
                toolCalls.Add(toolCallRecord);

                // 將工具呼叫與結果加入歷史，以供下一輪迭代使用
                history.Add(new ConversationEntry
                {
                    Role = "assistant",
                    Content = $"[Tool Call: {toolCallRecord.ToolName}]"
                });
                history.Add(new ConversationEntry
                {
                    Role = "tool",
                    Content = toolCallRecord.Result.Output,
                    ToolName = toolCallRecord.ToolName
                });
            }

            // 將本輪對話（使用者訊息 + Agent 回覆）持久化至記憶體
            await _memoryStore.AppendHistoryAsync(request.SessionId, new ConversationEntry { Role = "user", Content = request.UserMessage }, cancellationToken);
            await _memoryStore.AppendHistoryAsync(request.SessionId, new ConversationEntry { Role = "assistant", Content = finalContent }, cancellationToken);

            var response = AgentResponse.Success(request.SessionId, finalContent, toolCalls);
            await NotifyCompletedAsync(context, response, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await NotifyErrorAsync(context, ex, cancellationToken);
            return AgentResponse.Failure(request.SessionId, ex.Message);
        }
    }

    // ── 範本方法步驟（子類別可覆寫） ────────────────────────────────────

    /// <summary>
    /// 感知步驟：從記憶體載入指定工作階段的對話歷史，準備執行上下文。
    /// 子類別可覆寫此方法以加入額外的上下文處理邏輯。
    /// </summary>
    /// <param name="request">本次使用者請求物件。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>目前工作階段的對話歷史可變清單。</returns>
    protected virtual async Task<List<ConversationEntry>> PerceiveAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var history = await _memoryStore.GetHistoryAsync(request.SessionId, cancellationToken);
        return history.ToList();
    }

    /// <summary>
    /// 規劃步驟：將請求傳送給 LLM，決定下一步行動（直接回覆或呼叫工具）。
    /// 子類別可覆寫此方法以在呼叫 LLM 前後插入自訂邏輯。
    /// </summary>
    /// <param name="llmRequest">包含系統提示詞、歷史及可用工具的 LLM 請求物件。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>LLM 的補全回應，可能包含文字內容或工具呼叫請求。</returns>
    protected virtual Task<LlmResponse> PlanAsync(LlmRequest llmRequest, CancellationToken cancellationToken)
        => _llmProvider.CompleteAsync(llmRequest, cancellationToken);

    /// <summary>
    /// 行動步驟：依 LLM 的指示執行對應工具，並回傳執行記錄。
    /// 若指定的工具名稱不存在於工具登錄中，將回傳失敗結果而不拋出例外。
    /// 子類別可覆寫此方法以加入工具前後處理邏輯。
    /// </summary>
    /// <param name="context">目前的 Agent 執行上下文。</param>
    /// <param name="toolCallRequest">LLM 請求的工具名稱與參數。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>包含工具名稱、參數及執行結果的 <see cref="ToolCallRecord"/>。</returns>
    protected virtual async Task<ToolCallRecord> ActAsync(AgentContext context, ToolCallRequest toolCallRequest, CancellationToken cancellationToken)
    {
        ToolResult result;
        var toolArguments = new Dictionary<string, string>(toolCallRequest.Arguments);

        // 如果是圖片分析工具且請求中包含圖片數據，自動注入圖片
        if (toolCallRequest.ToolName == "analyze_image" &&
            context.Request.Metadata.TryGetValue("image_base64", out var imageBase64) &&
            !string.IsNullOrEmpty(imageBase64) &&
            !toolArguments.ContainsKey("image_base64"))
        {
            toolArguments["image_base64"] = imageBase64;
        }

        if (_toolRegistry.TryGet(toolCallRequest.ToolName, out var tool) && tool != null)
        {
            result = await tool.ExecuteAsync(toolArguments, cancellationToken);
        }
        else
        {
            result = ToolResult.Failure(toolCallRequest.ToolName, $"Tool '{toolCallRequest.ToolName}' not found.");
        }

        await NotifyToolCalledAsync(context, toolCallRequest.ToolName, toolArguments, result, cancellationToken);

        return new ToolCallRecord
        {
            ToolName = toolCallRequest.ToolName,
            Arguments = toolArguments,
            Result = result
        };
    }

    /// <summary>
    /// 反思步驟：對 LLM 的最終輸出進行後處理（如格式化、過濾等）。
    /// 預設實作直接回傳原始內容，子類別可覆寫以加入後處理邏輯。
    /// </summary>
    /// <param name="content">LLM 回覆的原始文字內容。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>後處理完成的最終文字內容。</returns>
    protected virtual Task<string> ReflectAsync(string content, CancellationToken cancellationToken)
    {
        // 清理內容中的重複技術標籤
        if (string.IsNullOrEmpty(content))
            return Task.FromResult(content);

        // 移除重複的 [analyze_image] 或工具執行標籤
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            content,
            @"\[analyze_image\]\s*",
            ""
        );

        // 如果內容中有多個相同的分析結果段落（用雙換行作為分隔符），只保留第一個
        var segments = cleaned.Split("\n\n", System.StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 1)
        {
            // 檢查是否有重複的內容（相同的開頭）
            var uniqueSegments = new System.Collections.Generic.List<string> { segments[0] };
            for (int i = 1; i < segments.Length; i++)
            {
                // 如果當前段落和前一個不同，則添加
                if (!segments[i].Equals(segments[i - 1], System.StringComparison.OrdinalIgnoreCase))
                {
                    uniqueSegments.Add(segments[i]);
                }
            }
            cleaned = string.Join("\n\n", uniqueSegments);
        }

        return Task.FromResult(cleaned);
    }

    // ── 私有輔助方法 ────────────────────────────────────────────────────

    /// <summary>
    /// 根據目前請求與對話歷史建立 LLM 請求物件，附加工具清單。
    /// </summary>
    /// <param name="request">本次使用者請求。</param>
    /// <param name="history">目前工作階段的對話歷史。</param>
    /// <returns>已填入所有必要資訊的 <see cref="LlmRequest"/>。</returns>
    private LlmRequest BuildLlmRequest(AgentRequest request, List<ConversationEntry> history)
        => new()
        {
            SystemPrompt = _options.SystemPrompt,
            History = history,
            UserMessage = request.UserMessage,
            AvailableTools = _toolRegistry.GetAll()
        };

    /// <summary>通知所有事件處理器：Agent 已開始執行。</summary>
    private async Task NotifyStartedAsync(AgentContext context, CancellationToken ct)
    {
        foreach (var handler in _eventHandlers)
            await handler.OnAgentStartedAsync(context, ct);
    }

    /// <summary>通知所有事件處理器：Agent 已完成執行。</summary>
    private async Task NotifyCompletedAsync(AgentContext context, AgentResponse response, CancellationToken ct)
    {
        foreach (var handler in _eventHandlers)
            await handler.OnAgentCompletedAsync(context, response, ct);
    }

    /// <summary>通知所有事件處理器：Agent 執行發生錯誤。</summary>
    private async Task NotifyErrorAsync(AgentContext context, Exception ex, CancellationToken ct)
    {
        foreach (var handler in _eventHandlers)
            await handler.OnAgentErrorAsync(context, ex, ct);
    }

    /// <summary>通知所有事件處理器：Agent 已呼叫工具並取得結果。</summary>
    private async Task NotifyToolCalledAsync(AgentContext context, string toolName, IReadOnlyDictionary<string, string> args, ToolResult result, CancellationToken ct)
    {
        foreach (var handler in _eventHandlers)
            await handler.OnToolCalledAsync(context, toolName, args, result, ct);
    }
}
