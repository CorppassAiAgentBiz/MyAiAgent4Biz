# AiAgent 系統架構文檔

## 📋 目錄
1. [系統概述](#系統概述)
2. [分層架構](#分層架構)
3. [組件模型](#組件模型)
4. [通信流程](#通信流程)
5. [依賴管理](#依賴管理)
6. [基礎設施](#基礎設施)

---

## 系統概述

**AiAgent** 是一個智慧型多工具 AI 代理框架，採用**清淨架構 (Clean Architecture)** 設計，實現了完全的關注點分離和高度的可擴充性。

### 核心特點

- ✅ **三層分離**: Core (零依賴) → Infrastructure (實現) → Web (API)
- ✅ **設計模式驅動**: 7 個設計模式有機結合
- ✅ **外掛化工具**: 動態工具註冊與執行
- ✅ **智慧路由**: 自動決策搜尋 vs 直接回覆
- ✅ **開放擴充**: 容易交換 LLM、記憶體、中介軟體

---

## 分層架構

### 整體架構圖

```
┌─────────────────────────────────────────────────────────────┐
│                    AiAgent.Web Layer                        │
│  (HTTP API + REST Endpoints + Frontend Assets)              │
│  ├─ Program.cs (DI Container + Route Configuration)         │
│  ├─ ChatRequest.cs (Request DTO)                            │
│  └─ wwwroot/ (Static Web Assets)                            │
└──────────────────┬──────────────────────────────────────────┘
                   │ (Calls)
                   ▼
┌─────────────────────────────────────────────────────────────┐
│             AiAgent.Infrastructure Layer                    │
│  (Concrete Implementations & External Integrations)         │
│  ├─ Llm/                                                    │
│  │  ├─ MockLlmProvider (Testing)                            │
│  │  ├─ OllamaLlmProvider (Local LLM)                        │
│  │  └─ WebSearchLlmProvider (Smart Routing)                 │
│  ├─ Memory/                                                 │
│  │  └─ InMemoryMemoryStore (Session History)                │
│  ├─ Tools/                                                  │
│  │  ├─ EchoTool                                             │
│  │  └─ CurrentTimeTool                                      │
│  ├─ Middleware/                                             │
│  │  ├─ ValidationMiddleware                                 │
│  │  └─ LoggingMiddleware                                    │
│  └─ Events/                                                 │
│     └─ ConsoleEventHandler                                  │
└──────────────────┬──────────────────────────────────────────┘
                   │ (Implements)
                   ▼
┌─────────────────────────────────────────────────────────────┐
│              AiAgent.Core Layer                             │
│  (Interfaces + Models + Business Logic)                     │
│  Zero External Dependencies (Framework Only)                │
│  ├─ Abstractions/ (8 Core Interfaces)                       │
│  │  ├─ IAgent                                               │
│  │  ├─ ILlmProvider                                         │
│  │  ├─ ITool                                                │
│  │  ├─ IMemoryStore                                         │
│  │  ├─ IToolRegistry                                        │
│  │  ├─ IAgentEventHandler                                   │
│  │  ├─ IAgentMiddleware                                     │
│  │  └─ IAgentFactory                                        │
│  ├─ Models/ (11+ Data Transfer Objects)                     │
│  ├─ Agents/                                                 │
│  │  ├─ BaseAgent (Template Method Pattern)                  │
│  │  └─ DefaultAgent                                         │
│  ├─ Pipeline/ (Chain of Responsibility)                     │
│  │  └─ AgentPipeline                                        │
│  ├─ Registry/ (Plugin Pattern)                              │
│  │  └─ ToolRegistry                                         │
│  └─ Factory/ (Factory Pattern)                              │
│     └─ AgentFactory                                         │
└─────────────────────────────────────────────────────────────┘
```

### 依賴方向

```
Web Layer          Infrastructure Layer       Core Layer
    │                    │                        │
    ├───────────────────→│                        │
    │                    │                        │
    └────────────────────────────────────────────→│
                         │                        │
                         └───────────────────────→│

核心原則:
✓ Web 可以依賴 Infrastructure 和 Core
✓ Infrastructure 只依賴 Core
✓ Core 不依賴任何其他層 (Framework Only)
```

---

## 組件模型

### 核心組件清單

#### 1. Agent 執行引擎 (BaseAgent)

**檔案**: `src/AiAgent.Core/Agents/BaseAgent.cs`

**職責**:
- 實現 Template Method Pattern 的 Perceive → Plan → Act → Reflect 四步骤
- 管理工具呼叫迴圈 (最多 MaxIterations 次)
- 與所有 IAgentEventHandler 通知交互
- 持久化會話歷史到 IMemoryStore

**擴充點** (可子類化覆蓋):
```csharp
protected virtual Task<List<ConversationEntry>> PerceiveAsync(...)
    // 加載會話歷史，可在此強化上下文

protected virtual Task<LlmResponse> PlanAsync(...)
    // 調用 LLM，可在此預處理/後處理

protected virtual Task<ToolCallRecord> ActAsync(...)
    // 執行工具，可在此增加工具執行鉤子

protected virtual Task<string> ReflectAsync(...)
    // 後處理 LLM 輸出，可在此實現自訂格式化
```

#### 2. LLM 提供者 (ILlmProvider)

**介面檔案**: `src/AiAgent.Core/Abstractions/ILlmProvider.cs`

**實現**:

|     實        現     |     位        置     |     用        途     |
|----------------------|----------------------|---------------------|
| MockLlmProvider      | Infrastructure/Llm/  | 測試用，返回預設回應  |
| OllamaLlmProvider    | Infrastructure/Llm/  | 本地 Ollama 連接     |
| WebSearchLlmProvider | Infrastructure/Llm/  | **智慧路由 + 搜尋**   |

**WebSearchLlmProvider 智慧路由邏輯**:

```
User Input
   ├─ [99% 準確] Code Detection (代碼層偵測)
   │   ├─ 時間查詢? → get_current_time 工具呼叫
   │   └─ 回聲查詢? → echo 工具呼叫
   │
   └─ [快速] Intent Classification (意圖分類)
       ├─ SEARCH Intent
       │   ├─ DuckDuckGo API 查詢
       │   ├─ Wikipedia 回退
       │   └─ Ollama 摘要結果
       │
       └─ DIRECT Intent
           └─ Ollama Conversation (完整歷史對話)
```

#### 3. 工具註冊表 (ToolRegistry)

**檔案**: `src/AiAgent.Core/Registry/ToolRegistry.cs`

**技術**, 特點**:
- 執行緒安全: ConcurrentDictionary 儲存
- 大小寫不敏感: OrdinalIgnoreCase 比較
- 動態註冊: 執行時新增工具
- 快速查詢: O(1) 查詢複雜度

**API**:
```csharp
void Register(ITool tool)              // 註冊或覆蓋工具
bool TryGet(string name, out ITool?)   // 大小寫不敏感查詢
IReadOnlyList<ITool> GetAll()          // 全部工具快照
```

#### 4. 記憶體儲存 (InMemoryMemoryStore)

**檔案**: `src/AiAgent.Infrastructure/Memory/InMemoryMemoryStore.cs`

**資料結構**:
```csharp
ConcurrentDictionary<string, string> _keyValueStore
    // 任意鍵值對儲存

ConcurrentDictionary<string, ConcurrentQueue<ConversationEntry>> _histories
    // 會話 ID → 會話歷史佇列
    // 執行緒安全，無鎖操作
```

**特點**:
- ✅ 純記憶體儲存 (快速，開發環境理想)
- ✅ 執行緒安全 (ConcurrentQueue)
- ✅ 會話隔離 (每個 SessionId 獨立歷史)
- ❌ 無持久化 (應用重啟後丟失)

**擴充建議**: 可實現 SQL Server / Redis 後端

#### 5. 工具執行 (ITool)

**內建工具**:

|工具名           |檔案                                   |功能   |輸入   |輸出         |
|----------------|---------------------------------------|-------|-------|------------|
|echo            |Infrastructure/Tools/EchoTool.cs       |回顯訊息|message|回顯文本    |
|get_current_time|Infrastructure/Tools/CurrentTimeTool.cs|取得時間|(無)   |ISO 8601 UTC|

#### 6. 事件系統 (IAgentEventHandler)

**觀察者實現**: `src/AiAgent.Infrastructure/Events/ConsoleEventHandler.cs`

**監聽的事件**:
```csharp
OnAgentStartedAsync(...)      // Agent 開始執行
OnAgentCompletedAsync(...)    // Agent 完成
OnAgentErrorAsync(...)        // Agent 異常
OnToolCalledAsync(...)        // 工具執行
```

**設計特點**:
- 非同步通知 (Task-based)
- 可注入記錄委派 (支援自訂日誌輸出)
- 支援多事件處理器 (觀察者模式)

#### 7. 中介軟體管線 (AgentPipeline)

**檔案**: `src/AiAgent.Core/Pipeline/AgentPipeline.cs`

**責任鏈模式**:
```csharp
// 按 Order 屬性排序 (升序)
// 例如: [M2 (order=2), M1 (order=-10)]
// 排序後: [M1, M2]
// 執行: M1 → M2 → Terminal

IAgentMiddleware[] middlewares = new[]
{
    new ValidationMiddleware(),      // order = -50
    new LoggingMiddleware(),          // order = 0
    new CustomMiddleware()            // order = 10
};

var pipeline = new AgentPipeline(middlewares);
var response = await pipeline.ExecuteAsync(context);
```

**現有中介軟體**:
- ValidationMiddleware: 驗證請求完整性
- LoggingMiddleware: 記錄 Agent 執行

#### 8. 工廠模式 (AgentFactory)

**檔案**: `src/AiAgent.Core/Factory/AgentFactory.cs`

**責任**:
1. 建立 DefaultAgent 實例
2. 注入所有依賴:
   - ILlmProvider
   - IToolRegistry
   - IMemoryStore
   - IAgentEventHandler[]
3. 集中化 Agent 建立邏輯

---

## 通信流程

### 請求流程

```
1. HTTP Request (POST /api/chat)
   └─ ChatRequest { SessionId, Message }

2. Program.cs (Line 85-136)
   ├─ Deserialize ChatRequest
   ├─ IAgentFactory.Create(AgentOptions)
   └─ IAgent.RunAsync(AgentRequest)

3. BaseAgent.RunAsync() - 主執行流程
   ├─ Event: OnAgentStartedAsync()
   │
   ├─ Perceive: PerceiveAsync()
   │  ├─ IMemoryStore.GetHistoryAsync(sessionId)
   │  └─ 返回: List<ConversationEntry> (會話歷史)
   │
   ├─ Loop (i=0 to MaxIterations-1)
   │  ├─ Plan: PlanAsync()
   │  │  ├─ BuildLlmRequest() (包含歷史)
   │  │  ├─ ILlmProvider.CompleteAsync()
   │  │  │  └─ WebSearchLlmProvider
   │  │  │     ├─ 代碼層工具偵測
   │  │  │     ├─ Ollama 意圖分類
   │  │  │     ├─ 條件: DuckDuckGo/Wikipedia 搜尋
   │  │  │     └─ 返回: LlmResponse
   │  │  │
   │  │  └─ 檢查是否有工具呼叫?
   │  │     ├─ NO: 反思 → 跳出循環
   │  │     └─ YES: 執行工具
   │  │
   │  ├─ Act: ActAsync()
   │  │  ├─ ToolRegistry.TryGet(toolName)
   │  │  ├─ ITool.ExecuteAsync()
   │  │  ├─ Event: OnToolCalledAsync()
   │  │  └─ 返回: ToolCallRecord
   │  │
   │  └─ 更新歷史，下個迭代
   │
   ├─ IMemoryStore.AppendHistoryAsync() - 持久化
   │
   ├─ Event: OnAgentCompletedAsync()
   │
   └─ 返回: AgentResponse

4. 構建 HTTP 回應 (Line 124-135)
   └─ JSON { success, content, toolCalls, error }

5. HTTP Response
   ├─ Status: 200 OK
   └─ Body: JSON AgentResponse
```

### 例外處理流程

```
RunAsync() TRY-CATCH:
   │
   ├─ 任何步驟拋出異常
   │
   ├─ CATCH 塊:
   │  ├─ Event: OnAgentErrorAsync()
   │  └─ 返回: AgentResponse.Failure()
   │
   └─ HTTP Response
      ├─ Status: 200 OK (表示 API 成功)
      └─ Body: { success: false, error: "..." }
```

---

## 依賴管理

### 依賴注入配置

**位置**: `AiAgent.Web/Program.cs` (Line 11-28)

```csharp
// LLM Provider
var llmProvider = new WebSearchLlmProvider(
    ollamaModel: "tinydolphin",
    ollamaBaseUrl: "http://localhost:11434");

// Tool Registry
var toolRegistry = new ToolRegistry();
toolRegistry.Register(new EchoTool());
toolRegistry.Register(new CurrentTimeTool());

// Memory Store
var memoryStore = new InMemoryMemoryStore();

// Event Handler
var eventHandler = new ConsoleEventHandler();

// Factory
var agentFactory = new AgentFactory(
    llmProvider,
    toolRegistry,
    memoryStore,
    [eventHandler]);

builder.Services.AddSingleton<IAgentFactory>(agentFactory);
```

### 依賴圖

```
IAgentFactory
  ├─ ILlmProvider (WebSearchLlmProvider)
  │   ├─ HttpClient (外部)
  │   └─ Ollama (外部服務)
  │
  ├─ IToolRegistry (ToolRegistry)
  │   ├─ EchoTool (ITool)
  │   └─ CurrentTimeTool (ITool)
  │
  ├─ IMemoryStore (InMemoryMemoryStore)
  │   └─ ConcurrentDictionary
  │
  └─ IAgentEventHandler[] (ConsoleEventHandler)
      └─ Action<string> (可注入)

IAgent (DefaultAgent)
  └─ BaseAgent
      ├─ ILlmProvider
      ├─ IToolRegistry
      ├─ IMemoryStore
      └─ IAgentEventHandler[]
```

---

## 基礎設施

### 外部依賴

#### 1. Ollama (本地 LLM)

**用途**: 文本推理、翻譯、意圖分類

**配置**:
```
模型: tinydolphin (可配置)
端點: http://localhost:11434/api/chat
認證: 無 (本地)
API 格式: REST + JSON
```

**在 WebSearchLlmProvider 中的用途**:
- 翻譯中文 → 英文
- 意圖分類 (SEARCH vs DIRECT)
- 搜尋結果摘要
- 直接對話生成

#### 2. DuckDuckGo Instant Answer API

**用途**: 快速事實查詢

**特點**:
- 免費，無認證
- 無速率限制
- JSON 回應
- 速度快 (< 500ms)

**回退鏈**:
1. DuckDuckGo (首選)
2. Wikipedia (備用)
3. 無結果 → 直接 Ollama 回答

#### 3. Wikipedia API

**用途**: 詳細文章搜尋

**特點**:
- 免費，無認證
- 支援多語言
- 完整 API 介面

---

### 部署架構

```
┌─────────────┐
│   Browser   │  用戶進行聊天
└──────┬──────┘
       │ HTTP
       ▼
┌─────────────────────────┐
│  AiAgent.Web (.NET App) │  ASP.NET Core 應用
│  Port: 5297             │  靜態資源 + API
└──────┬──────────────────┘
       │ TCP/JSON
       ├──────────┬────────────┬──────────┐
       ▼          ▼            ▼          ▼
    Ollama   DuckDuckGo   Wikipedia   Memory
  (local)    (internet)  (internet)  (app RAM)
```

### 可擴充性考量

#### 目前狀態
- ✅ 單線程執行 (無並發限制)
- ✅ 單應用實例 (無分散式)
- ✅ 記憶體儲存 (無外部 DB)
- ✅ 適合: 開發、教育、小規模應用

#### 生產環境改進

1. **記憶體→データベース**:
   ```csharp
   public sealed class SqlServerMemoryStore : IMemoryStore { ... }
   // 實現數據庫持久化
   ```

2. **HttpClient→連接池**:
   ```csharp
   builder.Services.AddHttpClient<WebSearchLlmProvider>()
       .ConfigureHttpClient(...)
       .AddResilienceHandler("ollama")  // 重試、斷路器
   ```

3. **Ollama→分散式**:
   ```csharp
   // 負載均衡多個 Ollama 實例
   var ollamas = new[] { "http://ollama1:11434", "http://ollama2:11434" };
   ```

4. **添加速率限制中介軟體**:
   ```csharp
   middlewares = new[]
   {
       new RateLimitMiddleware(maxRequests: 100),  // order = -100
       new ValidationMiddleware(),
       new LoggingMiddleware()
   };
   ```

---

## 層間契約

### Core → Infrastructure 契約

Infrastructure 必須實現 Core 定義的 8 個介面:

```csharp
// Core/Abstractions (Core 定義)
public interface IAgent { Task<AgentResponse> RunAsync(...); }
public interface ILlmProvider { Task<LlmResponse> CompleteAsync(...); }
public interface ITool { Task<ToolResult> ExecuteAsync(...); }
public interface IMemoryStore { Task<IReadOnlyList<ConversationEntry>> GetHistoryAsync(...); }
public interface IToolRegistry { void Register(ITool); bool TryGet(...); }
public interface IAgentEventHandler { Task OnAgentStartedAsync(...); }
public interface IAgentMiddleware { Task<AgentResponse> InvokeAsync(...); }
public interface IAgentFactory { IAgent Create(AgentOptions); }

// Infrastructure/... (Infrastructure 實現)
public sealed class DefaultAgent : BaseAgent { ... }        // IAgent
public sealed class WebSearchLlmProvider : ILlmProvider { ... }
public sealed class EchoTool : ITool { ... }
public sealed class InMemoryMemoryStore : IMemoryStore { ... }
public sealed class ToolRegistry : IToolRegistry { ... }
public sealed class ConsoleEventHandler : IAgentEventHandler { ... }
public sealed class LoggingMiddleware : IAgentMiddleware { ... }
public sealed class AgentFactory : IAgentFactory { ... }
```

### Web → Core/Infrastructure 契約

Web 層只知道:
- IAgentFactory (用於建立 Agent)
- AgentRequest/AgentResponse (DTO)
- ChatRequest (Web DTO)

Web 不知道:
- 具體的 LLM 實現
- 具體的工具
- 記憶體儲存細節

---

## 架構總結

|面向            |特點                                                                |
|---------------|--------------------------------------------------------------------|
| **分層設計**   | 清淨架構，關注點分離                                                 |
| **依賴方向**   | Web → Infra → Core (單向依賴)                                       |
| **零依賴核心** | Core 只依賴 .NET 框架                                               |
| **設計模式**   | Template Method, Strategy, Repository, Registry, Observer, Factory |
| **擴充性**     | 易於添加工具、提供者、中介軟體、事件處理                              |
| **非同步優先** | 全 async/await 設計                                                 |
| **執行緒安全** | ConcurrentDictionary、ConcurrentQueue 使用                          |
| **可測試性**   | 高度依賴注入，MockLlmProvider 用於測試                               |

