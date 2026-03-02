# AiAgent 系統設計文檔

## 📋 目錄
1. [設計核心](#設計核心)
2. [7個設計模式](#7個設計模式)
3. [資料流設計](#資料流設計)
4. [設計決策](#設計決策)
5. [安全性考量](#安全性考量)
6. [效能優化](#效能優化)

---

## 設計核心

### 設計原則

AiAgent 遵循以下設計原則:

1. **單一職責原則 (SRP)**
   - BaseAgent: 只管 Agent 執行邏輯
   - WebSearchLlmProvider: 只管 LLM 路由與搜尋
   - ToolRegistry: 只管工具登記與查詢

2. **開放-閉合原則 (OCP)**
   - 開放: 可添加新工具、新 LLM、新中介軟體
   - 閉合: BaseAgent 實現無需修改

3. **里氏替換原則 (LSP)**
   - 所有 ILlmProvider 實現可互換
   - 所有 ITool 實現可互換
   - 所有 IMemoryStore 實現可互換

4. **介面隔離原則 (ISP)**
   - 8 個專用介面，各有明確職責
   - 實現者只需實現必要方法

5. **依賴倒置原則 (DIP)**
   - Web 層依賴 Core 介面，不依賴 Infrastructure 具體實現
   - Infrastructure 實現 Core 介面

### 關鍵設計決策

|決策                         |理由                        |
|-----------------------------|---------------------------|
| **Template Method**         | 提供可組態的 Agent 執行流程 |
| **Strategy Pattern**        | 支持不同 LLM 後端的切換     |
| **Repository Pattern**      | 隔離記憶體儲存實現          |
| **Registry Pattern**        | 動態工具擴展                |
| **Observer Pattern**        | 解耦事件通知與處理          |
| **Chain of Responsibility** | 靈活的中介軟體管線          |
| **Factory Pattern**         | 集中化 Agent 建立邏輯       |

---

## 7個設計模式

### 1. Template Method Pattern (BaseAgent)

**目的**: 定義演算法骨架，允許子類化覆蓋特定步驟

**實現位置**: `src/AiAgent.Core/Agents/BaseAgent.cs`

**程式碼範例**:

```csharp
public abstract class IAgent
{
    // 模板方法 - 定義演算法結構
    public async Task<AgentResponse> RunAsync(
        AgentRequest request,
        CancellationToken ct = default)
    {
        // 步驟 1: 感知 (Perceive)
        var history = await PerceiveAsync(request, ct);

        // 步驟 2-3: 計畫-行動迴圈 (Plan-Act Loop)
        for (int i = 0; i < MaxIterations; i++)
        {
            var llmResponse = await PlanAsync(..., ct);

            if (!llmResponse.HasToolCall)
            {
                // 步驟 4: 反思 (Reflect)
                finalContent = await ReflectAsync(llmResponse.Content, ct);
                break;
            }

            var toolRecord = await ActAsync(context, llmResponse.ToolCall, ct);
            // ... 更新歷史
        }

        // 持久化
        await _memoryStore.AppendHistoryAsync(...);

        return AgentResponse.Success(...);
    }

    // 可覆蓋的步驟 (虛方法)
    protected virtual Task<List<ConversationEntry>> PerceiveAsync(...)
        => _memoryStore.GetHistoryAsync(...);

    protected virtual Task<LlmResponse> PlanAsync(LlmRequest req, CancellationToken ct)
        => _llmProvider.CompleteAsync(req, ct);

    protected virtual Task<string> ReflectAsync(string content, CancellationToken ct)
        => Task.FromResult(content);  // 預設無後處理
}
```

**優點**:
- ✅ 演算法結構清晰 (Perceive → Plan → Act → Reflect)
- ✅ 子類可覆蓋特定步驟，如強化上下文或自訂格式
- ✅ 無需修改主邏輯即可擴展

**應用場景**:
- 自訂感知 (添加文檔檢索)
- 自訂反思 (結構化輸出格式)
- 自訂計畫 (多模型輪詢)

---

### 2. Strategy Pattern (ILlmProvider)

**目的**: 在執行時選擇 LLM 實現策略

**實現位置**: `src/AiAgent.Core/Abstractions/ILlmProvider.cs`

**3 個策略實現**:

|  策            略  |  用途  |  特             點  |
|--------------------|--------|--------------------|
|MockLlmProvider     |測試    |返回預設/可配置回應   |
|OllamaLlmProvider   |本地推理|純 Ollama，無智慧路由 |
|WebSearchLlmProvider|生產    |智慧路由 + 搜尋      |

**程式碼範例**:

```csharp
// 抽象策略
public interface ILlmProvider
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken = default);
}

// 具體策略 1: 測試
public sealed class MockLlmProvider : ILlmProvider
{
    private readonly Func<LlmRequest, LlmResponse>? _responseProvider;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken = default)
    {
        return _responseProvider?.Invoke(request)
            ?? new LlmResponse { Content = "Mock response" };
    }
}

// 具體策略 2: 本地 Ollama
public sealed class OllamaLlmProvider : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken = default)
    {
        var response = await OllamaChatAsync(request.UserMessage, ...);
        return new LlmResponse { Content = response };
    }
}

// 具體策略 3: 智慧路由
public sealed class WebSearchLlmProvider : ILlmProvider
{
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken = default)
    {
        // 代碼層工具偵測
        if (IsTimeQuery(request.UserMessage))
            return new LlmResponse
            {
                ToolCall = new ToolCallRequest { ToolName = "get_current_time" }
            };

        // Ollama 意圖分類
        var (needsSearch, query) = await ClassifyIntentAsync(request.UserMessage);

        if (needsSearch)
        {
            var result = await SearchWebAsync(query);
            return await SummarizeResultAsync(result);
        }
        else
        {
            return await DirectConversationAsync(request);
        }
    }
}

// 使用 (運行時選擇)
ILlmProvider provider = new WebSearchLlmProvider(...);  // 或其他策略
var response = await provider.CompleteAsync(request);
```

**優點**:
- ✅ 易於切換 LLM 後端 (Ollama → OpenAI → Azure → 本地)
- ✅ 測試友好 (MockLlmProvider)
- ✅ 各策略獨立開發與測試

**策略切換範例**:

```csharp
// 開發環境: 測試
var llmProvider = new MockLlmProvider(req => new LlmResponse { Content = "Test" });

// 本地開發: 快速
var llmProvider = new OllamaLlmProvider("tinydolphin", "http://localhost:11434");

// 生產環境: 智慧路由
var llmProvider = new WebSearchLlmProvider("tinydolphin", "http://localhost:11434");

var agent = AgentFactory.Create(llmProvider, ...);
```

---

### 3. Repository Pattern (IMemoryStore)

**目的**: 抽象資料存取層，隔離儲存實現

**實現位置**: `src/AiAgent.Core/Abstractions/IMemoryStore.cs`

**當前實現**: `src/AiAgent.Infrastructure/Memory/InMemoryMemoryStore.cs`

**程式碼範例**:

```csharp
// 抽象倉庫
public interface IMemoryStore
{
    Task SaveAsync(string key, string value, CancellationToken = default);
    Task<string?> GetAsync(string key, CancellationToken = default);
    Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(
        string sessionId, CancellationToken = default);
    Task AppendHistoryAsync(
        string sessionId, ConversationEntry entry, CancellationToken = default);
    Task ClearHistoryAsync(string sessionId, CancellationToken = default);
}

// 具體實現: 記憶體儲存
public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<string, string> _keyValueStore = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConversationEntry>>
        _histories = new();

    public async Task SaveAsync(string key, string value, CancellationToken = default)
    {
        _keyValueStore[key] = value;
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(
        string sessionId, CancellationToken = default)
    {
        if (_histories.TryGetValue(sessionId, out var queue))
            return queue.ToList().AsReadOnly();
        return [];
    }

    public async Task AppendHistoryAsync(
        string sessionId, ConversationEntry entry, CancellationToken = default)
    {
        var queue = _histories.GetOrAdd(sessionId, _ => new ConcurrentQueue<ConversationEntry>());
        queue.Enqueue(entry);
        await Task.CompletedTask;
    }
}

// 替代實現: SQL Server
public sealed class SqlServerMemoryStore : IMemoryStore
{
    private readonly SqlConnection _connection;

    public async Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(
        string sessionId, CancellationToken = default)
    {
        var cmd = new SqlCommand(
            "SELECT Role, Content, ToolName, Timestamp FROM ConversationHistory WHERE SessionId = @id",
            _connection);
        cmd.Parameters.AddWithValue("@id", sessionId);

        var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var entries = new List<ConversationEntry>();
        while (await reader.ReadAsync())
        {
            entries.Add(new ConversationEntry
            {
                Role = reader["Role"].ToString()!,
                Content = reader["Content"].ToString()!,
                ToolName = reader["ToolName"] as string,
                Timestamp = (DateTimeOffset)reader["Timestamp"]
            });
        }
        return entries.AsReadOnly();
    }
}

// 替代實現: Redis
public sealed class RedisMemoryStore : IMemoryStore
{
    private readonly IConnectionMultiplexer _redis;

    public async Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(
        string sessionId, CancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync($"history:{sessionId}");
        if (json.IsNull) return [];

        return JsonSerializer.Deserialize<List<ConversationEntry>>(json.ToString())!
            .AsReadOnly();
    }
}

// 使用 (切換儲存後端無需改動 Agent)
IMemoryStore store = new InMemoryMemoryStore();        // 開發
// IMemoryStore store = new SqlServerMemoryStore(...); // 生產
// IMemoryStore store = new RedisMemoryStore(...);     // 分散式

var agent = AgentFactory.Create(..., store);
```

**優點**:
- ✅ 儲存實現與業務邏輯完全解耦
- ✅ 快速切換儲存後端 (Memory → DB → Redis → 混合)
- ✅ 易於測試 (InMemory 快速，無 I/O)

---

### 4. Registry Pattern (ToolRegistry)

**目的**: 動態註冊與查詢可外掛物件

**實現位置**: `src/AiAgent.Core/Registry/ToolRegistry.cs`

**程式碼範例**:

```csharp
public sealed class ToolRegistry : IToolRegistry
{
    // 執行緒安全的工具儲存 (大小寫不敏感)
    private readonly ConcurrentDictionary<string, ITool> _tools =
        new(StringComparer.OrdinalIgnoreCase);

    // 註冊工具
    public void Register(ITool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));
        _tools[tool.Name] = tool;  // 覆蓋現有工具
    }

    // 查詢工具 (用於執行)
    public bool TryGet(string name, out ITool? tool)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            tool = null;
            return false;
        }
        return _tools.TryGetValue(name, out tool);
    }

    // 列出所有工具 (用於 LLM 上下文)
    public IReadOnlyList<ITool> GetAll() => _tools.Values.ToList();
}

// 使用: 程式啟動時註冊
var registry = new ToolRegistry();
registry.Register(new EchoTool());
registry.Register(new CurrentTimeTool());
registry.Register(new WeatherTool());       // 新工具
registry.Register(new DatabaseQueryTool()); // 新工具

// 執行時: BaseAgent 自動使用
var toolCallRequest = new ToolCallRequest { ToolName = "get_weather" };
if (registry.TryGet(toolCallRequest.ToolName, out var tool))
{
    var result = await tool.ExecuteAsync(arguments);
}
```

**設計特點**:
- ✅ ConcurrentDictionary: 執行緒安全，無鎖
- ✅ OrdinalIgnoreCase: 大小寫不敏感查詢
- ✅ 動態註冊: 執行時新增工具無需重新編譯

**擴展場景**:
1. 開機註冊核心工具
2. 載入外掛 DLL 動態註冊工具
3. 使用者授權的工具動態啟用/禁用

---

### 5. Observer Pattern (IAgentEventHandler)

**目的**: 解耦事件通知與處理

**實現位置**:
- 介面: `src/AiAgent.Core/Abstractions/IAgentEventHandler.cs`
- 實現: `src/AiAgent.Infrastructure/Events/ConsoleEventHandler.cs`

**程式碼範例**:

```csharp
// 事件介面
public interface IAgentEventHandler
{
    Task OnAgentStartedAsync(AgentContext context, CancellationToken = default);
    Task OnAgentCompletedAsync(AgentContext context, AgentResponse response, CancellationToken = default);
    Task OnAgentErrorAsync(AgentContext context, Exception exception, CancellationToken = default);
    Task OnToolCalledAsync(
        AgentContext context,
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        ToolResult result,
        CancellationToken = default);
}

// 具體觀察者 1: 主控台日誌
public sealed class ConsoleEventHandler : IAgentEventHandler
{
    private readonly Action<string>? _logger;

    public ConsoleEventHandler(Action<string>? logger = null) => _logger = logger;

    public Task OnAgentStartedAsync(AgentContext context, CancellationToken = default)
    {
        _logger?.Invoke($"[Event] Agent started. Session={context.Request.SessionId}");
        return Task.CompletedTask;
    }

    public Task OnAgentCompletedAsync(AgentContext context, AgentResponse response, CancellationToken = default)
    {
        _logger?.Invoke($"[Event] Agent completed. Success={response.IsSuccess}, Tools={response.ToolCalls.Count}");
        return Task.CompletedTask;
    }

    public Task OnAgentErrorAsync(AgentContext context, Exception ex, CancellationToken = default)
    {
        _logger?.Invoke($"[Event] Agent error: {ex.Message}");
        return Task.CompletedTask;
    }

    public Task OnToolCalledAsync(AgentContext context, string toolName,
        IReadOnlyDictionary<string, string> arguments, ToolResult result, CancellationToken = default)
    {
        _logger?.Invoke($"[Event] Tool '{toolName}' called. Success={result.IsSuccess}");
        return Task.CompletedTask;
    }
}

// 具體觀察者 2: 分析與計量
public sealed class AnalyticsEventHandler : IAgentEventHandler
{
    private readonly IMetricsCollector _metrics;

    public Task OnAgentStartedAsync(AgentContext context, CancellationToken = default)
    {
        _metrics.RecordEvent("agent.started", new { sessionId = context.Request.SessionId });
        return Task.CompletedTask;
    }

    public Task OnAgentCompletedAsync(AgentContext context, AgentResponse response, CancellationToken = default)
    {
        _metrics.RecordEvent("agent.completed",
            new { success = response.IsSuccess, toolCount = response.ToolCalls.Count });
        return Task.CompletedTask;
    }
}

// 具體觀察者 3: 持久化審計日誌
public sealed class AuditEventHandler : IAgentEventHandler
{
    private readonly IDataStore _auditLog;

    public async Task OnAgentCompletedAsync(AgentContext context, AgentResponse response, CancellationToken = default)
    {
        await _auditLog.SaveAsync(new AuditRecord
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = context.Request.SessionId,
            Action = "agent_completed",
            Success = response.IsSuccess,
            Duration = DateTime.UtcNow - context.StartTime
        });
    }
}

// BaseAgent 中的通知機制
public abstract class BaseAgent : IAgent
{
    private readonly IAgentEventHandler[] _eventHandlers;

    private async Task NotifyStartedAsync(AgentContext context, CancellationToken ct)
    {
        foreach (var handler in _eventHandlers)
        {
            try
            {
                await handler.OnAgentStartedAsync(context, ct);
            }
            catch { /* 忽略單個處理者的異常 */ }
        }
    }

    private async Task NotifyCompletedAsync(AgentContext context, AgentResponse response, CancellationToken ct)
    {
        foreach (var handler in _eventHandlers)
        {
            try
            {
                await handler.OnAgentCompletedAsync(context, response, ct);
            }
            catch { /* 忽略單個處理者的異常 */ }
        }
    }
}

// 使用: 多個觀察者
var handlers = new IAgentEventHandler[]
{
    new ConsoleEventHandler(),
    new AnalyticsEventHandler(metricsCollector),
    new AuditEventHandler(auditStore),
    new NotificationEventHandler(notificationService)
};

var factory = new AgentFactory(llmProvider, registry, store, handlers);
var agent = factory.Create(options);

// 現在所有事件會通知所有 4 個觀察者
await agent.RunAsync(request);
```

**優點**:
- ✅ 解耦: Agent 不知道觀察者實現
- ✅ 擴展: 添加新觀察者無需修改 Agent
- ✅ 多重性: 支援多個觀察者同時監聽

**實際應用**:
- 日誌聚合
- 效能監控
- 審計追蹤
- 使用者通知

---

### 6. Chain of Responsibility Pattern (AgentPipeline)

**目的**: 在責任鏈中傳遞請求

**實現位置**: `src/AiAgent.Core/Pipeline/AgentPipeline.cs`

**程式碼範例**:

```csharp
// 中介軟體介面
public interface IAgentMiddleware
{
    int Order { get; }  // 執行順序
    Task<AgentResponse> InvokeAsync(
        AgentContext context,
        AgentMiddlewareDelegate next,
        CancellationToken cancellationToken = default);
}

public delegate Task<AgentResponse> AgentMiddlewareDelegate(
    AgentContext context, CancellationToken cancellationToken);

// 具體中介軟體 1: 驗證
public sealed class ValidationMiddleware : IAgentMiddleware
{
    public int Order => -50;  // 最先執行

    public async Task<AgentResponse> InvokeAsync(
        AgentContext context,
        AgentMiddlewareDelegate next,
        CancellationToken cancellationToken = default)
    {
        // 驗證請求
        if (string.IsNullOrWhiteSpace(context.Request.UserMessage))
        {
            return AgentResponse.Failure(context.Request.SessionId, "Message cannot be empty");
        }

        // 傳遞給下一個中介軟體
        return await next(context, cancellationToken);
    }
}

// 具體中介軟體 2: 日誌記錄
public sealed class LoggingMiddleware : IAgentMiddleware
{
    private readonly Action<string>? _logger;
    public int Order => 0;

    public async Task<AgentResponse> InvokeAsync(
        AgentContext context,
        AgentMiddlewareDelegate next,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger?.Invoke($"[{DateTime.UtcNow:O}] Agent starting. Session: {context.Request.SessionId}");

        var response = await next(context, cancellationToken);

        var duration = DateTime.UtcNow - startTime;
        _logger?.Invoke($"[{DateTime.UtcNow:O}] Agent completed. Success: {response.IsSuccess}, Duration: {duration.TotalMilliseconds}ms");

        return response;
    }
}

// 具體中介軟體 3: 速率限制
public sealed class RateLimitMiddleware : IAgentMiddleware
{
    private readonly Dictionary<string, int> _requestCounts = new();
    public int Order => -10;

    public async Task<AgentResponse> InvokeAsync(
        AgentContext context,
        AgentMiddlewareDelegate next,
        CancellationToken cancellationToken = default)
    {
        var sessionId = context.Request.SessionId;
        _requestCounts.TryGetValue(sessionId, out var count);

        if (count > 100)  // 每分鐘最多 100 個請求
        {
            return AgentResponse.Failure(sessionId, "Rate limit exceeded");
        }

        _requestCounts[sessionId] = count + 1;
        return await next(context, cancellationToken);
    }
}

// 管線編排器
public sealed class AgentPipeline
{
    private readonly AgentMiddlewareDelegate _pipeline;

    public AgentPipeline(IAgentMiddleware[] middlewares, AgentMiddlewareDelegate terminal)
    {
        // 按 Order 升序排序
        var sortedMiddlewares = middlewares.OrderBy(m => m.Order).ToList();

        // 從後向前組裝管線 (洋蔥式)
        var pipeline = terminal;
        foreach (var middleware in sortedMiddlewares.Reverse<IAgentMiddleware>())
        {
            var current = middleware;
            var next = pipeline;
            pipeline = (ctx, ct) => current.InvokeAsync(ctx, next, ct);
        }

        _pipeline = pipeline;
    }

    public Task<AgentResponse> ExecuteAsync(AgentContext context, CancellationToken ct = default)
    {
        return _pipeline(context, ct);
    }
}

// 執行順序範例
// 輸入: [RateLimitMiddleware (order=-10), LoggingMiddleware (order=0), ValidationMiddleware (order=-50)]
// 排序: [ValidationMiddleware (-50), RateLimitMiddleware (-10), LoggingMiddleware (0)]
//
// 執行流:
// ValidationMiddleware (驗證)
//   └─ RateLimitMiddleware (速率限制)
//       └─ LoggingMiddleware (日誌)
//           └─ Terminal (Agent.RunAsync)
//               └─ LoggingMiddleware (記錄完成)
//           └─ RateLimitMiddleware (返回)
//       └─ ValidationMiddleware (返回)
```

**優點**:
- ✅ 職責分離: 每個中介軟體有單一職責
- ✅ 動態組裝: 可運行時動態添加/移除中介軟體
- ✅ 順序控制: Order 屬性精確控制執行順序
- ✅ 洋蔥模型: 請求進入時執行，返回時逆序執行

---

### 7. Factory Pattern (AgentFactory)

**目的**: 集中化複雜物件的建立

**實現位置**: `src/AiAgent.Core/Factory/AgentFactory.cs`

**程式碼範例**:

```csharp
public interface IAgentFactory
{
    IAgent Create(AgentOptions options);
}

public sealed class AgentFactory : IAgentFactory
{
    private readonly ILlmProvider _llmProvider;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMemoryStore _memoryStore;
    private readonly IAgentEventHandler[] _eventHandlers;
    private readonly IAgentMiddleware[] _middlewares;

    public AgentFactory(
        ILlmProvider llmProvider,
        IToolRegistry toolRegistry,
        IMemoryStore memoryStore,
        IAgentEventHandler[] eventHandlers,
        IAgentMiddleware[] middlewares = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _eventHandlers = eventHandlers ?? Array.Empty<IAgentEventHandler>();
        _middlewares = middlewares ?? Array.Empty<IAgentMiddleware>();
    }

    public IAgent Create(AgentOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        // 創建 DefaultAgent 並注入依賴
        var agent = new DefaultAgent(
            options: options,
            llmProvider: _llmProvider,
            toolRegistry: _toolRegistry,
            memoryStore: _memoryStore,
            eventHandlers: _eventHandlers,
            middlewares: _middlewares);

        return agent;
    }
}

// 使用: 簡化了 Agent 的獲取
var factory = new AgentFactory(
    new WebSearchLlmProvider(...),
    new ToolRegistry(),
    new InMemoryMemoryStore(),
    new[] { new ConsoleEventHandler() });

var agent = factory.Create(new AgentOptions
{
    Name = "ChatAgent",
    Description = "Interactive chat",
    SystemPrompt = "You are helpful",
    MaxIterations = 5
});

// 無需了解 DefaultAgent 的建構細節
```

**優點**:
- ✅ 簡化: 呼叫方無需了解複雜的依賴
- ✅ 一致性: 所有 Agent 使用相同的依賴配置
- ✅ 靈活性: 可在工廠內實現複雜的邏輯 (條件建立、快取等)

---

## 資料流設計

### 完整資料流圖

```
HTTP Request (POST /api/chat)
    │
    ├─ ChatRequest { SessionId, Message }
    │
    ▼
Program.cs
    │
    ├─ IAgentFactory.Create(AgentOptions)
    │   │
    │   └─→ Inject:
    │       ├─ ILlmProvider
    │       ├─ IToolRegistry
    │       ├─ IMemoryStore
    │       └─ IAgentEventHandler[]
    │
    ├─ IAgent.RunAsync(AgentRequest)
    │
    ▼ BaseAgent.RunAsync() ──────────────────────────────────┐
    │                                                        │
    ├─ Event: OnAgentStartedAsync()                          │
    │                                                        │
    ├─ Perceive: PerceiveAsync()                             │
    │   │                                                    │
    │   └─→ IMemoryStore.GetHistoryAsync(sessionId)          │
    │       └─ Returns: List<ConversationEntry>              │
    │                                                        │
    ├─ for i = 0 to MaxIterations:                           │
    │   │                                                    │
    │   ├─ Plan: PlanAsync()                                 │
    │   │   │                                                │
    │   │   └─→ ILlmProvider.CompleteAsync(LlmRequest)       │
    │   │       │                                            │
    │   │       ├─ WebSearchLlmProvider                      │
    │   │       │   ├─ Code Detection                        │
    │   │       │   ├─ Ollama: Translate & Classify          │
    │   │       │   ├─ [Branch on needsSearch]               │
    │   │       │   │   ├─ YES: DuckDuckGo/Wikipedia         │
    │   │       │   │   └─ Ollama: Summarize                 │
    │   │       │   │                                        │
    │   │       │   └─ Returns: LlmResponse                  │
    │   │       │       { Content or ToolCall }              │
    │   │       │                                            │
    │   ├─ [Branch: HasToolCall?]                            │
    │   │   │                                                │
    │   │   ├─ NO: Reflect & Break                           │
    │   │   │                                                │
    │   │   └─ YES: Act                                      │
    │   │       │                                            │
    │   │       ├─ Act: ActAsync()                           │
    │   │       │   │                                        │
    │   │       │   ├─ IToolRegistry.TryGet(toolName)        │
    │   │       │   │                                        │
    │   │       │   ├─ [Branch: Found?]                      │
    │   │       │   │   ├─ NO: ToolResult.Failure            │
    │   │       │   │   │                                    │
    │   │       │   │   └─ YES: ITool.ExecuteAsync()         │
    │   │       │   │       ├─ EchoTool.ExecuteAsync()       │
    │   │       │   │       └─ CurrentTimeTool.ExecuteAsync()│
    │   │       │   │           └─ Returns: ToolResult       │
    │   │       │   │                                        │
    │   │       │   ├─ Event: OnToolCalledAsync()            │
    │   │       │   │                                        │
    │   │       │   └─ Returns: ToolCallRecord               │
    │   │       │                                            │
    │   │       └─ Update History & Continue Loop            │
    │   │                                                    │
    │
    ├─ IMemoryStore.AppendHistoryAsync() [Persist]           │
    │                                                        │
    ├─ Event: OnAgentCompletedAsync()                        │
    │                                                        │
    └─ Returns: AgentResponse ───────────────────────────────┘

HTTP Response
    │
    └─ JSON { success, content, toolCalls, error }
```

### 型別轉換鏈

```
ChatRequest
    ↓ (Web Layer)
AgentRequest
    ↓ (BaseAgent.RunAsync)
AgentContext
    ↓ (Plan)
LlmRequest → [ILlmProvider] → LlmResponse
    ↓ (HasToolCall?)
ToolCallRequest
    ↓ (Act)
ToolCallRecord (包含 ToolResult)
    ↓ (所有迭代後)
AgentResponse
    ↓ (Web Layer)
JSON Response
```

---

## 設計決策

### 1. 為什麼用 Template Method?

| 而不是        | 因為                                        |
|---------------|--------------------------------------------|
| 硬編碼執行邏輯 | 允許自訂流程擴展 (Perceive/Plan/Act/Reflect) |
| 回調函式      | Type-safe，易於測試                          |
| 配置驅動      | 邏輯流程需要程式碼實現                        |

### 2. 為什麼用 Strategy?

**LLM 選擇**:
- MockLlmProvider: 開發環境快速測試
- OllamaLlmProvider: 簡單直接的本地推理
- WebSearchLlmProvider: 智慧路由 + 搜尋功能

**好處**: 無需修改 Agent，即可切換 LLM 後端

### 3. 為什麼用 Repository?

**記憶體儲存抽象**:
- InMemoryMemoryStore: 開發/測試
- SqlServerMemoryStore: 生產 DB
- RedisMemoryStore: 快速快取

**好處**: 儲存實現與業務邏輯完全隔離

### 4. 為什麼 Core 零依賴?

```
如果 Core 依賴 Infrastructure:
  ❌ System.Net.Http (Infrastructure dependency)
  ❌ 難以測試 Core 邏輯
  ❌ Core 修改需要 Infrastructure 同步

零依賴設計:
  ✅ Core 只定義契約 (介面)
  ✅ Infrastructure 實現契約
  ✅ Web 層編排 (DI)
  ✅ Core 純業務邏輯 (框架相關)
```

### 5. 為什麼事件系統非同步?

```csharp
// 非同步的原因:
- 事件處理可能涉及 I/O (寫 DB、送通知)
- 無需等待所有事件完成 (異常隔離)
- 支援並行事件處理 (Task.WhenAll)

// 範例: 3 個事件處理器並行
await Task.WhenAll(
    handler1.OnAgentCompletedAsync(...),
    handler2.OnAgentCompletedAsync(...),
    handler3.OnAgentCompletedAsync(...)
);
```

### 6. 為什麼中介軟體有 Order?

```csharp
// 執行順序很重要:
ValidationMiddleware (order=-50)  // 首先驗證
    ↓
RateLimitMiddleware (order=-10)   // 然後速率限制
    ↓
LoggingMiddleware (order=0)       // 最後日誌
    ↓
[Terminal: Agent.RunAsync()]      // 實際執行

// 如果順序錯誤:
// RateLimitMiddleware 先執行無效請求也會被計數
// LoggingMiddleware 不會記錄被限制的請求
```

---

## 安全性考量

### 1. 輸入驗證

```csharp
// ValidationMiddleware
public async Task<AgentResponse> InvokeAsync(...)
{
    // 驗證 UserMessage 不為空
    if (string.IsNullOrWhiteSpace(context.Request.UserMessage))
        return AgentResponse.Failure(..., "Message cannot be empty");

    // 驗證 SessionId 格式
    if (!IsValidSessionId(context.Request.SessionId))
        return AgentResponse.Failure(..., "Invalid session ID");

    // 驗證訊息長度 (防止 DoS)
    if (context.Request.UserMessage.Length > MAX_MESSAGE_LENGTH)
        return AgentResponse.Failure(..., "Message too long");

    return await next(context, cancellationToken);
}
```

### 2. 工具執行沙箱

```csharp
// ActAsync 中的錯誤處理
try
{
    var result = await tool.ExecuteAsync(arguments, cancellationToken);
    if (!result.IsSuccess)
    {
        // 工具執行失敗，安全返回
        return new ToolCallRecord
        {
            ToolName = toolName,
            Result = result
        };
    }
}
catch (Exception ex)
{
    // 工具異常不會導致 Agent 崩潰
    return new ToolCallRecord
    {
        ToolName = toolName,
        Result = ToolResult.Failure(toolName, $"Execution error: {ex.Message}")
    };
}
```

### 3. SQL 注入防護

```csharp
// ✅ 參數化查詢 (安全)
var cmd = new SqlCommand(
    "SELECT * FROM History WHERE SessionId = @id",
    connection);
cmd.Parameters.AddWithValue("@id", sessionId);

// ❌ 字串拼接 (不安全)
// "SELECT * FROM History WHERE SessionId = '" + sessionId + "'"
```

### 4. 速率限制

```csharp
public sealed class RateLimitMiddleware : IAgentMiddleware
{
    private readonly Dictionary<string, int> _requestCounts = new();
    private readonly int _maxRequests = 100;
    private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);

    public async Task<AgentResponse> InvokeAsync(...)
    {
        var sessionId = context.Request.SessionId;

        if (!_requestCounts.ContainsKey(sessionId))
            _requestCounts[sessionId] = 0;

        if (_requestCounts[sessionId]++ > _maxRequests)
        {
            return AgentResponse.Failure(sessionId,
                "Rate limit exceeded. Max 100 requests per minute.");
        }

        // 重置計數器
        _ = Task.Delay(_timeWindow).ContinueWith(_ =>
        {
            _requestCounts[sessionId] = 0;
        });

        return await next(context, cancellationToken);
    }
}
```

### 5. 敏感資訊過濾

```csharp
// 日誌不應包含敏感資訊
public Task OnToolCalledAsync(
    AgentContext context,
    string toolName,
    IReadOnlyDictionary<string, string> arguments,  // ⚠️ 可能包含敏感資訊
    ToolResult result,
    CancellationToken = default)
{
    // ✅ 只記錄工具名與結果狀態，不記錄參數
    _logger?.Invoke($"[Event] Tool '{toolName}' called. Success={result.IsSuccess}");
    return Task.CompletedTask;
}
```

---

## 效能優化

### 1. 非同步全覆蓋

```csharp
// ✅ 非同步 (不阻塞執行緒)
public async Task<string> FetchDataAsync()
{
    using var response = await _httpClient.GetAsync(url);
    return await response.Content.ReadAsStringAsync();
}

// ❌ 同步阻塞 (浪費執行緒資源)
public string FetchDataSync()
{
    var response = _httpClient.GetAsync(url).Result;  // 阻塞
    return response.Content.ReadAsStringAsync().Result;
}
```

### 2. 執行緒安全無鎖設計

```csharp
// ToolRegistry 使用 ConcurrentDictionary (無鎖, O(1) 查詢)
private readonly ConcurrentDictionary<string, ITool> _tools =
    new(StringComparer.OrdinalIgnoreCase);

// InMemoryMemoryStore 使用 ConcurrentQueue (無鎖, 執行緒安全)
private readonly ConcurrentDictionary<string, ConcurrentQueue<ConversationEntry>> _histories
    = new();

// 避免:
// ❌ private Dictionary<string, ITool> _tools;
//    lock (_tools) { ... }  // 鎖會導致爭用
```

### 3. 智慧搜尋避免不必要的 API 呼叫

```csharp
// WebSearchLlmProvider 的決策:
// ✅ 代碼層偵測 (99% 準確，本地)
//    └─ 時間查詢 → 直接工具呼叫，無需 Ollama

// ✅ Ollama 意圖分類 (快速，5 tokens)
//    ├─ SEARCH → DuckDuckGo (平均 < 500ms)
//    └─ DIRECT → Ollama Conversation (< 2s)

// 避免: 所有查詢都進行網路搜尋 (浪費 API 配額，變慢)
```

### 4. 會話歷史快照

```csharp
// PerceiveAsync 返回 List (快速複製)
public async Task<List<ConversationEntry>> PerceiveAsync(...)
{
    var history = await _memoryStore.GetHistoryAsync(...);
    return new List<ConversationEntry>(history);  // 快速複製
}

// 避免: 多次查詢同一歷史
// for (int i = 0; i < MaxIterations; i++)
// {
//     var history = await _memoryStore.GetHistoryAsync(...);  // ❌ 每次都查
// }
```

### 5. 事件異常隔離

```csharp
// 單個事件處理者異常不影響其他處理者
private async Task NotifyCompletedAsync(AgentContext context, AgentResponse response, CancellationToken ct)
{
    foreach (var handler in _eventHandlers)
    {
        try
        {
            await handler.OnAgentCompletedAsync(context, response, ct);
        }
        catch (Exception ex)
        {
            // 日誌記錄但繼續
            System.Diagnostics.Trace.WriteLine($"Event handler error: {ex}");
        }
    }
}

// 效果: 即使一個事件處理者失敗，Agent 執行也完成
```

---

## 設計模式比較

| 模式                    | 何時使用                 | 何時避免       |
|-------------------------|-------------------------|---------------|
| Template Method         | 演算法骨架固定，步驟可自訂 | 演算法頻繁變化 |
| Strategy                | 多個互換實現存在          | 實現差異小     |
| Repository              | 隔離資料存取             | 簡單查詢場景    |
| Registry                | 動態元件註冊             | 靜態容器已足夠  |
| Observer                | 多事件處理者             | 單一處理者      |
| Chain of Responsibility | 順序處理重要             | 平行處理場景    |
| Factory                 | 複雜建立邏輯             | 建立簡單        |

---

## 設計權衡

### 記憶體 vs 速度

```
InMemoryMemoryStore:
✅ 速度快 (無 I/O)
❌ 應用重啟丟失

SqlServerMemoryStore:
✅ 持久化
❌ I/O 延遲 (10-50ms)

解決方案: 混合
├─ 活躍會話 → 記憶體 (快)
└─ 已完成會話 → DB (持久)
```

### 功能 vs 複雜度

```
OllamaLlmProvider:
✅ 簡單，易於理解
❌ 所有查詢都調用 Ollama (慢)

WebSearchLlmProvider:
✅ 智慧路由，效率高
❌ 代碼複雜，邏輯分支多

對於新手: OllamaLlmProvider
對於生產: WebSearchLlmProvider
```

