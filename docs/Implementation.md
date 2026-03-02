# AiAgent 系統實現文檔

## 📋 目錄
1. [快速開始](#快速開始)
2. [API 參考](#api-參考)
3. [擴展指南](#擴展指南)
4. [配置指南](#配置指南)
5. [部署指南](#部署指南)
6. [故障排除](#故障排除)

---

## 快速開始

### 前置需求

```
- .NET 10.0 SDK
- Ollama (本地 LLM)
  ├─ 下載: https://ollama.ai
  ├─ 啟動: ollama serve
  ├─ 拉取模型: ollama pull tinydolphin
  └─ 驗證: curl http://localhost:11434/api/tags

- 網路連接 (用於 DuckDuckGo/Wikipedia 搜尋)
```

### 建置與執行

```bash
# 1. 複製專案
git clone <repository>
cd Template4AiAgent

# 2. 還原依賴
dotnet restore

# 3. 建置
dotnet build

# 4. 執行
dotnet run --project AiAgent.Web

# 5. 存取應用
# 打開瀏覽器: http://localhost:5297
```

### 首次使用

```
1. 在聊天框輸入: "現在幾點"
   → 系統偵測時間查詢
   → 執行 get_current_time 工具
   → 返回: "現在時間是：2026年02月25日 19:11:11 (二)"

2. 在聊天框輸入: "你好"
   → 系統分類為 DIRECT 查詢
   → 調用 Ollama 直接對話
   → 返回: AI 的友善回應

3. 在聊天框輸入: "最新的 AI 新聞"
   → 系統分類為 SEARCH 查詢
   → 調用 DuckDuckGo 搜尋
   → 摘要搜尋結果
   → 返回: 搜尋結果摘要 (繁體中文)
```

---

## API 參考

### REST API

#### POST /api/chat

執行聊天請求並返回 Agent 回應。

**請求格式**:

```json
{
  "sessionId": "user-session-123",
  "message": "現在幾點?"
}
```

**請求欄位**:

| 欄位      | 型別    | 必需  | 說明                          |
|-----------|--------|-------|------------------------------|
| sessionId | string |  ✅  | 使用者會話識別符 (用於歷史追蹤) |
| message   | string |  ✅  | 使用者輸入訊息                 |

**回應格式 (成功)**:

```json
{
  "success": true,
  "content": "現在時間是：2026年02月25日 19:11:11 (二)",
  "toolCalls": [
    {
      "toolName": "get_current_time",
      "result": "2026-02-25T11:11:11.0012585+00:00",
      "resultSuccess": true
    }
  ],
  "error": null
}
```

**回應格式 (失敗)**:

```json
{
  "success": false,
  "content": null,
  "toolCalls": [],
  "error": "Rate limit exceeded"
}
```

**回應欄位**:

| 欄位      | 型別          | 說明            |
|-----------|--------------|-----------------|
| success   | boolean      | 執行是否成功     |
| content   | string       | Agent 回應內容   |
| toolCalls | ToolCall[]   | 執行的工具清單    |
| error     | string\|null | 錯誤訊息 (若失敗) |

**ToolCall 物件**:

```json
{
  "toolName": "get_current_time",
  "result": "2026-02-25T11:11:11...",
  "resultSuccess": true
}
```

**cURL 範例**:

```bash
curl -X POST http://localhost:5297/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "user-123",
    "message": "現在幾點?"
  }'
```

**C# 客戶端範例**:

```csharp
using HttpClient client = new();

var request = new
{
    sessionId = "user-123",
    message = "現在幾點?"
};

var content = new StringContent(
    JsonSerializer.Serialize(request),
    Encoding.UTF8,
    "application/json");

var response = await client.PostAsync(
    "http://localhost:5297/api/chat",
    content);

var responseBody = await response.Content.ReadAsStringAsync();
Console.WriteLine(responseBody);
```

---

### C# API

#### IAgent 介面

```csharp
public interface IAgent
{
    /// <summary>Agent 名稱</summary>
    string Name { get; }

    /// <summary>Agent 描述</summary>
    string Description { get; }

    /// <summary>執行 Agent 並返回回應</summary>
    Task<AgentResponse> RunAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);
}

// 使用範例
var agent = agentFactory.Create(new AgentOptions
{
    Name = "ChatBot",
    Description = "Interactive assistant",
    SystemPrompt = "You are helpful",
    MaxIterations = 5
});

var response = await agent.RunAsync(new AgentRequest
{
    SessionId = "session-001",
    UserMessage = "Hello"
});

Console.WriteLine($"Success: {response.IsSuccess}");
Console.WriteLine($"Content: {response.Content}");
foreach (var toolCall in response.ToolCalls)
{
    Console.WriteLine($"Tool: {toolCall.ToolName}, Result: {toolCall.Result.Output}");
}
```

#### AgentRequest 物件

```csharp
public sealed class AgentRequest
{
    /// <summary>會話識別符 (用於追蹤歷史)</summary>
    public required string SessionId { get; init; }

    /// <summary>使用者輸入訊息</summary>
    public required string UserMessage { get; init; }

    /// <summary>額外的中繼資料</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}
```

#### AgentResponse 物件

```csharp
public sealed class AgentResponse
{
    public required string SessionId { get; init; }

    /// <summary>Agent 回應內容</summary>
    public required string Content { get; init; }

    /// <summary>執行是否成功</summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>錯誤訊息 (若失敗)</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>執行的工具清單</summary>
    public IReadOnlyList<ToolCallRecord> ToolCalls { get; init; } = [];

    /// <summary>建立成功回應</summary>
    public static AgentResponse Success(
        string sessionId,
        string content,
        IReadOnlyList<ToolCallRecord>? toolCalls = null) { ... }

    /// <summary>建立失敗回應</summary>
    public static AgentResponse Failure(
        string sessionId,
        string errorMessage) { ... }
}
```

#### IToolRegistry 介面

```csharp
public interface IToolRegistry
{
    /// <summary>註冊工具 (可覆蓋現有)</summary>
    void Register(ITool tool);

    /// <summary>查詢工具 (大小寫不敏感)</summary>
    bool TryGet(string name, out ITool? tool);

    /// <summary>取得所有工具</summary>
    IReadOnlyList<ITool> GetAll();
}

// 使用範例
var registry = new ToolRegistry();
registry.Register(new EchoTool());
registry.Register(new CurrentTimeTool());

if (registry.TryGet("get_current_time", out var tool))
{
    var result = await tool.ExecuteAsync(new Dictionary<string, string>());
    Console.WriteLine(result.Output);
}
```

#### IMemoryStore 介面

```csharp
public interface IMemoryStore
{
    /// <summary>儲存鍵值對</summary>
    Task SaveAsync(string key, string value, CancellationToken = default);

    /// <summary>取得鍵值對</summary>
    Task<string?> GetAsync(string key, CancellationToken = default);

    /// <summary>取得會話歷史</summary>
    Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(
        string sessionId, CancellationToken = default);

    /// <summary>添加會話項目</summary>
    Task AppendHistoryAsync(
        string sessionId,
        ConversationEntry entry,
        CancellationToken = default);

    /// <summary>清除會話歷史</summary>
    Task ClearHistoryAsync(string sessionId, CancellationToken = default);
}

// 使用範例
var store = new InMemoryMemoryStore();

// 儲存
await store.SaveAsync("key1", "value1");

// 取得歷史
var history = await store.GetHistoryAsync("session-001");
foreach (var entry in history)
{
    Console.WriteLine($"[{entry.Role}] {entry.Content}");
}
```

---

## 擴展指南

### 1. 添加新工具

**步驟 1: 實現 ITool 介面**

```csharp
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

public sealed class WeatherTool : ITool
{
    public string Name => "get_weather";

    public string Description => "Get current weather for a city";

    public IReadOnlyDictionary<string, string> Parameters { get; } =
        new Dictionary<string, string>
        {
            ["city"] = "City name (e.g., Tokyo, New York)"
        };

    public async Task<ToolResult> ExecuteAsync(
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 取得城市名稱
            if (!arguments.TryGetValue("city", out var city))
            {
                return ToolResult.Failure(Name, "Missing 'city' parameter");
            }

            // 調用天氣 API (範例: OpenWeatherMap)
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(
                $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid=YOUR_API_KEY",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ToolResult.Failure(Name, "Failed to fetch weather data");
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var weather = JsonSerializer.Deserialize<WeatherResponse>(jsonContent);

            var result = $"Weather in {city}: {weather?.Main?.Temp}°C, {weather?.Weather?[0]?.Main}";
            return ToolResult.Success(Name, result);
        }
        catch (Exception ex)
        {
            return ToolResult.Failure(Name, $"Error: {ex.Message}");
        }
    }
}
```

**步驟 2: 在 Program.cs 中註冊**

```csharp
var toolRegistry = new ToolRegistry();
toolRegistry.Register(new EchoTool());
toolRegistry.Register(new CurrentTimeTool());
toolRegistry.Register(new WeatherTool());  // 新增

var agentFactory = new AgentFactory(llmProvider, toolRegistry, memoryStore, handlers);
```

**步驟 3: 測試工具**

```csharp
[Fact]
public async Task WeatherTool_Execute_ReturnsCityWeather()
{
    var tool = new WeatherTool();
    var result = await tool.ExecuteAsync(new Dictionary<string, string>
    {
        ["city"] = "Tokyo"
    });

    Assert.True(result.IsSuccess);
    Assert.Contains("Weather in Tokyo", result.Output);
}
```

**LLM 會自動檢測工具**:
用戶輸入 "東京的天氣怎麼樣?" → LLM 建議 → 執行 get_weather 工具 → 返回結果

---

### 2. 實現新的 LLM 提供者

**步驟 1: 實現 ILlmProvider 介面**

```csharp
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

public sealed class OpenAiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public string ProviderName => "OpenAI-GPT4";

    public OpenAiLlmProvider(string apiKey, string model = "gpt-4")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model;
        _httpClient = new HttpClient();
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // 構建 OpenAI 請求
        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        foreach (var entry in request.History)
        {
            messages.Add(new { role = entry.Role, content = entry.Content });
        }

        messages.Add(new { role = "user", content = request.UserMessage });

        // 構建工具定義
        var tools = new List<object>();
        foreach (var tool in request.AvailableTools)
        {
            tools.Add(new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = new
                    {
                        type = "object",
                        properties = tool.Parameters.ToDictionary(
                            kv => kv.Key,
                            kv => new { type = "string", description = kv.Value }),
                        required = tool.Parameters.Keys.ToList()
                    }
                }
            });
        }

        var openAiRequest = new
        {
            model = _model,
            messages = messages,
            tools = tools,
            tool_choice = "auto"
        };

        // 調用 OpenAI API
        var content = new StringContent(
            JsonSerializer.Serialize(openAiRequest),
            Encoding.UTF8,
            "application/json");

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            content,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseBody);

        // 解析回應
        var choice = openAiResponse?.Choices?[0];
        if (choice?.Message?.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
        {
            var toolCall = choice.Message.ToolCalls[0];
            return new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = toolCall.Function.Name,
                    Arguments = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        toolCall.Function.Arguments) ?? new Dictionary<string, string>()
                }
            };
        }

        return new LlmResponse
        {
            Content = choice?.Message?.Content ?? "No response"
        };
    }
}
```

**步驟 2: 在 Program.cs 切換提供者**

```csharp
// 開發環境: 測試
// var llmProvider = new MockLlmProvider(...);

// 本地開發: Ollama
// var llmProvider = new OllamaLlmProvider("tinydolphin", "http://localhost:11434");

// 生產環境: OpenAI
var llmProvider = new OpenAiLlmProvider(apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

var agentFactory = new AgentFactory(llmProvider, toolRegistry, memoryStore, handlers);
```

---

### 3. 實現新的記憶體儲存

**步驟 1: 實現 IMemoryStore 介面**

```csharp
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

public sealed class SqlServerMemoryStore : IMemoryStore
{
    private readonly string _connectionString;

    public SqlServerMemoryStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task SaveAsync(string key, string value, CancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new SqlCommand(
            @"INSERT INTO KeyValueStore (Key, Value) VALUES (@key, @value)
              ON CONFLICT(Key) DO UPDATE SET Value = @value",
            connection);

        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetAsync(string key, CancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new SqlCommand(
            "SELECT Value FROM KeyValueStore WHERE Key = @key",
            connection);

        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(
        string sessionId,
        CancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new SqlCommand(
            @"SELECT Role, Content, ToolName, Timestamp
              FROM ConversationHistory
              WHERE SessionId = @sessionId
              ORDER BY Timestamp ASC",
            connection);

        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        var entries = new List<ConversationEntry>();
        using var reader = await cmd.ExecuteReaderAsync();

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

    public async Task AppendHistoryAsync(
        string sessionId,
        ConversationEntry entry,
        CancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new SqlCommand(
            @"INSERT INTO ConversationHistory (SessionId, Role, Content, ToolName, Timestamp)
              VALUES (@sessionId, @role, @content, @toolName, @timestamp)",
            connection);

        cmd.Parameters.AddWithValue("@sessionId", sessionId);
        cmd.Parameters.AddWithValue("@role", entry.Role);
        cmd.Parameters.AddWithValue("@content", entry.Content);
        cmd.Parameters.AddWithValue("@toolName", entry.ToolName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@timestamp", entry.Timestamp);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearHistoryAsync(string sessionId, CancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new SqlCommand(
            "DELETE FROM ConversationHistory WHERE SessionId = @sessionId",
            connection);

        cmd.Parameters.AddWithValue("@sessionId", sessionId);
        await cmd.ExecuteNonQueryAsync();
    }
}
```

**步驟 2: 建立資料庫表**

```sql
CREATE TABLE KeyValueStore (
    Key NVARCHAR(255) PRIMARY KEY,
    Value NVARCHAR(MAX)
);

CREATE TABLE ConversationHistory (
    Id BIGINT PRIMARY KEY IDENTITY(1,1),
    SessionId NVARCHAR(255) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    ToolName NVARCHAR(255),
    Timestamp DATETIMEOFFSET NOT NULL,
    INDEX IX_SessionId (SessionId)
);
```

**步驟 3: 在 Program.cs 使用**

```csharp
var memoryStore = new SqlServerMemoryStore(
    connectionString: "Server=localhost;Database=AiAgent;Integrated Security=true;");

var agentFactory = new AgentFactory(llmProvider, toolRegistry, memoryStore, handlers);
```

---

### 4. 添加自訂中介軟體

**步驟 1: 實現 IAgentMiddleware**

```csharp
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

public sealed class AuthenticationMiddleware : IAgentMiddleware
{
    private readonly HashSet<string> _authorizedSessions;

    public int Order => -100;  // 首先驗證身份

    public AuthenticationMiddleware(HashSet<string> authorizedSessions)
    {
        _authorizedSessions = authorizedSessions;
    }

    public async Task<AgentResponse> InvokeAsync(
        AgentContext context,
        AgentMiddlewareDelegate next,
        CancellationToken cancellationToken = default)
    {
        // 檢查會話是否授權
        if (!_authorizedSessions.Contains(context.Request.SessionId))
        {
            return AgentResponse.Failure(
                context.Request.SessionId,
                "Unauthorized: Session not authenticated");
        }

        // 傳遞給下一個中介軟體
        return await next(context, cancellationToken);
    }
}

public sealed class InputSanitizationMiddleware : IAgentMiddleware
{
    public int Order => -80;

    public async Task<AgentResponse> InvokeAsync(
        AgentContext context,
        AgentMiddlewareDelegate next,
        CancellationToken cancellationToken = default)
    {
        // 清理輸入
        var sanitized = SanitizeInput(context.Request.UserMessage);

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return AgentResponse.Failure(
                context.Request.SessionId,
                "Invalid input: Message contains only whitespace");
        }

        // 更新上下文
        context.Items["sanitized_message"] = sanitized;

        return await next(context, cancellationToken);
    }

    private string SanitizeInput(string input)
    {
        // 移除 HTML 標籤、特殊字符等
        return System.Text.RegularExpressions.Regex.Replace(input, "<[^>]*>", "").Trim();
    }
}
```

**步驟 2: 組合中介軟體**

```csharp
var middlewares = new IAgentMiddleware[]
{
    new AuthenticationMiddleware(authorizedSessions),
    new InputSanitizationMiddleware(),
    new ValidationMiddleware(),
    new RateLimitMiddleware(),
    new LoggingMiddleware()
};

// 注: 當前 AgentFactory 需要修改以支援中介軟體注入
```

---

### 5. 添加自訂事件處理器

**步驟 1: 實現 IAgentEventHandler**

```csharp
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

public sealed class MetricsCollectorEventHandler : IAgentEventHandler
{
    private readonly IMetricsCollector _metrics;

    public MetricsCollectorEventHandler(IMetricsCollector metrics)
    {
        _metrics = metrics;
    }

    public Task OnAgentStartedAsync(AgentContext context, CancellationToken = default)
    {
        _metrics.IncrementCounter("agent.requests", new { agentName = context.Agent.Name });
        return Task.CompletedTask;
    }

    public Task OnAgentCompletedAsync(
        AgentContext context,
        AgentResponse response,
        CancellationToken = default)
    {
        _metrics.RecordMetric("agent.response_time_ms", 100);  // 實際計算
        _metrics.RecordMetric("agent.tool_calls", response.ToolCalls.Count);

        if (!response.IsSuccess)
            _metrics.IncrementCounter("agent.errors");

        return Task.CompletedTask;
    }

    public Task OnAgentErrorAsync(
        AgentContext context,
        Exception exception,
        CancellationToken = default)
    {
        _metrics.IncrementCounter("agent.exceptions", new { type = exception.GetType().Name });
        return Task.CompletedTask;
    }

    public Task OnToolCalledAsync(
        AgentContext context,
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        ToolResult result,
        CancellationToken = default)
    {
        _metrics.RecordMetric($"tool.{toolName}.calls", 1);
        if (!result.IsSuccess)
            _metrics.IncrementCounter($"tool.{toolName}.errors");

        return Task.CompletedTask;
    }
}

public sealed class DatabaseAuditEventHandler : IAgentEventHandler
{
    private readonly IDataStore _auditDb;

    public async Task OnAgentCompletedAsync(
        AgentContext context,
        AgentResponse response,
        CancellationToken = default)
    {
        await _auditDb.SaveAsync(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            SessionId = context.Request.SessionId,
            UserInput = context.Request.UserMessage,
            AgentResponse = response.Content,
            Success = response.IsSuccess,
            ToolsUsed = response.ToolCalls.Count
        });
    }
}
```

**步驟 2: 在 Program.cs 註冊**

```csharp
var eventHandlers = new IAgentEventHandler[]
{
    new ConsoleEventHandler(),
    new MetricsCollectorEventHandler(metricsCollector),
    new DatabaseAuditEventHandler(auditDb)
};

var agentFactory = new AgentFactory(llmProvider, toolRegistry, memoryStore, eventHandlers);
```

---

## 配置指南

### 環境變數

```bash
# .env 或系統環境變數

# Ollama 配置
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=tinydolphin

# OpenAI 配置 (若使用 OpenAiLlmProvider)
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-4

# SQL Server 配置 (若使用 SqlServerMemoryStore)
SQL_CONNECTION_STRING=Server=localhost;Database=AiAgent;Integrated Security=true;

# 應用配置
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5297

# Logging
LOGGING_LEVEL=Information
```

### Program.cs 配置

```csharp
var builder = WebApplication.CreateBuilder(args);

// 讀取環境變數
var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "tinydolphin";

// 配置 LLM Provider
ILlmProvider llmProvider = builder.Environment.IsDevelopment()
    ? new MockLlmProvider()  // 開發環境: 模擬
    : new WebSearchLlmProvider(ollamaModel, ollamaBaseUrl);  // 生產: 真實

// 配置記憶體儲存
IMemoryStore memoryStore = builder.Environment.IsProduction()
    ? new SqlServerMemoryStore(Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")!)
    : new InMemoryMemoryStore();

// 組建
var toolRegistry = new ToolRegistry();
toolRegistry.Register(new EchoTool());
toolRegistry.Register(new CurrentTimeTool());

var eventHandlers = new[] { new ConsoleEventHandler() };
var agentFactory = new AgentFactory(llmProvider, toolRegistry, memoryStore, eventHandlers);

builder.Services.AddSingleton<IAgentFactory>(agentFactory);
```

---

## 部署指南

### Docker 部署

**Dockerfile**:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 複製檔案
COPY . .

# 建置
RUN dotnet publish -c Release -o /app/publish

# 執行時
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# 環境變數
ENV ASPNETCORE_URLS=http://+:5297
ENV OLLAMA_BASE_URL=http://host.docker.internal:11434

EXPOSE 5297

ENTRYPOINT ["dotnet", "AiAgent.Web.dll"]
```

**docker-compose.yml**:

```yaml
version: '3.8'

services:
  aiagent:
    build: .
    ports:
      - "5297:5297"
    environment:
      OLLAMA_BASE_URL: http://ollama:11434
      ASPNETCORE_ENVIRONMENT: Production
    depends_on:
      - ollama
    volumes:
      - ./wwwroot:/app/wwwroot

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    command: serve

volumes:
  ollama_data:
```

**執行**:

```bash
docker-compose up
# 訪問: http://localhost:5297
```

### Kubernetes 部署

**deployment.yaml**:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: aiagent
spec:
  replicas: 3
  selector:
    matchLabels:
      app: aiagent
  template:
    metadata:
      labels:
        app: aiagent
    spec:
      containers:
      - name: aiagent
        image: your-registry/aiagent:1.0
        ports:
        - containerPort: 5297
        env:
        - name: OLLAMA_BASE_URL
          value: http://ollama-service:11434
        - name: SQL_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: db-credentials
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /
            port: 5297
          initialDelaySeconds: 10
          periodSeconds: 10

---
apiVersion: v1
kind: Service
metadata:
  name: aiagent-service
spec:
  type: LoadBalancer
  selector:
    app: aiagent
  ports:
  - protocol: TCP
    port: 80
    targetPort: 5297

---
apiVersion: v1
kind: ConfigMap
metadata:
  name: db-credentials
data:
  connection-string: "Server=sql-server;Database=AiAgent;..."
```

---

## 故障排除

### Ollama 連接失敗

**症狀**: `Connection refused (localhost:11434)`

**解決方案**:

```bash
# 1. 檢查 Ollama 是否執行
curl http://localhost:11434/api/tags

# 2. 啟動 Ollama
ollama serve

# 3. 拉取模型
ollama pull tinydolphin

# 4. 驗證配置
# 檢查 Program.cs OLLAMA_BASE_URL 是否正確
```

### 時間查詢返回錯誤

**症狀**: `[Ollama 錯誤] Connection refused (localhost:11434) [get_current_time]...`

**根本原因**: WebSearchLlmProvider 嘗試用 Ollama 摘要工具結果

**解決方案**: 已於 WebSearchLlmProvider 第 57-65 行修復，會直接格式化時間工具結果

### 會話歷史丟失

**症狀**: 刷新頁面後對話歷史消失

**原因**: 預設使用 InMemoryMemoryStore (應用重啟時丟失)

**解決方案**:

```csharp
// 改用持久化儲存
var memoryStore = new SqlServerMemoryStore(connectionString);

// 或使用 Redis
var memoryStore = new RedisMemoryStore(redisConnection);
```

### 效能變慢

**診斷**:

1. 檢查 Ollama 回應時間:
```bash
time curl http://localhost:11434/api/generate -d '{"model":"tinydolphin","prompt":"hello"}'
```

2. 檢查網路搜尋延遲:
   - DuckDuckGo: 通常 < 500ms
   - Wikipedia: 通常 < 1s

3. 分析日誌:
   - LoggingMiddleware 記錄往返時間
   - 檢查 WebSearchLlmProvider 是否進行不必要的搜尋

**最佳化**:

```csharp
// 1. 使用更快的模型
ollama pull orca-mini   // 比 tinydolphin 快

// 2. 增加 Ollama 記憶體
# 在環境變數設定
OLLAMA_NUM_PARALLEL=4

// 3. 添加快取層 (Redis)
var memoryStore = new RedisMemoryStore(...);  // 快取會話
```

### 工具不執行

**症狀**: LLM 建議工具但未執行

**檢查**:

```csharp
// 1. 驗證工具已註冊
var registry = new ToolRegistry();
registry.Register(new MyTool());

var tools = registry.GetAll();
Assert.Contains(tools, t => t.Name == "my_tool");

// 2. 檢查工具名稱大小寫
// ToolRegistry 使用 OrdinalIgnoreCase，應該不敏感

// 3. 驗證 LLM 能見到工具
public async Task<LlmResponse> CompleteAsync(LlmRequest request, ...)
{
    Console.WriteLine($"Available tools: {string.Join(", ", request.AvailableTools.Select(t => t.Name))}");
    // 確認你的工具在列表中
}
```

### 中文輸出亂碼

**症狀**: 回應包含亂碼或不正確的中文

**原因**: 字符編碼問題

**解決方案**:

```csharp
// 1. 確保 HTTP 回應編碼
Response.ContentType = "application/json; charset=utf-8";

// 2. JSON 序列化設定
var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var json = JsonSerializer.Serialize(response, options);

// 3. 資料庫連接字符集 (SQL Server)
// Connection String: ...;Encrypt=false;TrustServerCertificate=false;
```

---

## 常見問題 (FAQ)

**Q: 如何添加身份驗證?**
A: 實現 AuthenticationMiddleware (參見 4. 添加自訂中介軟體)

**Q: 如何限制使用者的工具存取?**
A: 實現自訂 ToolRegistry，根據會話 ID 篩選工具

**Q: 如何支援多語言?**
A: WebSearchLlmProvider 已支援繁體中文，可擴展以支援其他語言

**Q: 邊界條件如何測試?**
A: 使用 MockLlmProvider 注入特定回應，參見 AiAgent.Tests

**Q: 如何監控應用?**
A: 實現 MetricsCollectorEventHandler，集成 Prometheus/DataDog/Splunk

