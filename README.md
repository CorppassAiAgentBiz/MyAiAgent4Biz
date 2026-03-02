# Template4AiAgent

以 .NET 10 建構的 AI Agent 框架，採用乾淨架構（Clean Architecture）並實作七種設計模式。  
內建 **智慧路由 LLM 提供者**：使用 [Ollama](https://ollama.com/) 本地模型分析使用者意圖，自動決定「直接回覆」或「搜尋網路後摘要回覆」——完全免費，無需任何 API Key。  
附帶互動式 Web Chat UI，可直接在瀏覽器中與 Agent 對話。

---

## 智慧路由架構

```
使用者訊息
    │
    ├─ 程式碼層工具偵測（時間查詢 / Echo）→ 觸發工具呼叫
    │
    └─ Ollama 意圖分類（SEARCH / DIRECT）
         │
         ├─ SEARCH → DuckDuckGo / Wikipedia 搜尋
         │           → Ollama 以自然語言摘要回覆
         │
         └─ DIRECT → Ollama 直接以對話歷史回覆
```

| 步驟 | 說明 |
|---|---|
| 1. 工具偵測 | 程式碼邏輯偵測時間查詢、Echo 等指令，可靠度 100% |
| 2. 意圖分類 | Ollama 以極簡 prompt 判斷使用者意圖（SEARCH / DIRECT），僅需 5 tokens |
| 3a. 網路搜尋 | DuckDuckGo Instant Answer API → Wikipedia Search API（中／英文） |
| 3b. 摘要生成 | 搜尋結果送入 Ollama，以自然語言生成回覆（不會出現「根據搜尋結果」等字樣） |
| 3c. 直接回覆 | 帶入完整對話歷史，Ollama 直接以自然語言回覆 |

---

## 類別圖

完整類別圖請參閱 **[docs/class-diagram.md](docs/class-diagram.md)**（GitHub 會自動以 Mermaid 渲染）。

---

## 設計模式

| 模式 | 抽象介面 | 具體實作 |
|---|---|---|
| 策略（Strategy） | `ILlmProvider` | `MockLlmProvider`、`OllamaLlmProvider`、`WebSearchLlmProvider` |
| 範本方法（Template Method） | `BaseAgent` | `DefaultAgent` |
| 責任鏈（Chain of Responsibility） | `IAgentMiddleware` / `AgentPipeline` | `ValidationMiddleware`、`LoggingMiddleware` |
| 觀察者（Observer） | `IAgentEventHandler` | `ConsoleEventHandler` |
| 工廠（Factory） | `IAgentFactory` / `AgentFactory` | `DefaultAgent` |
| 儲存庫（Repository） | `IMemoryStore` | `InMemoryMemoryStore` |
| 外掛/登錄（Plugin/Registry） | `IToolRegistry` / `ToolRegistry` | `EchoTool`、`CurrentTimeTool` |

---

## LLM 提供者

本專案實作三種 `ILlmProvider`，可透過依賴注入自由切換：

| 提供者 | 說明 | 適用情境 |
|---|---|---|
| `MockLlmProvider` | 模擬回應，以程式碼邏輯偵測工具呼叫與生成回覆 | 單元測試、離線開發 |
| `OllamaLlmProvider` | 混合策略：程式碼偵測工具 + Ollama 生成自然語言 | 純對話場景 |
| `WebSearchLlmProvider` | **智慧路由**：Ollama 意圖分類 → 搜尋或直接回覆 → Ollama 自然語言生成 | **正式預設** |

---

## 方案結構

```
src/
  AiAgent.Core/            ← 介面、模型、管線、登錄（零外部相依）
  AiAgent.Infrastructure/  ← 具體實作（LLM 提供者、記憶體儲存、工具、中介軟體）
    Llm/
      MockLlmProvider.cs        ← 模擬 LLM（用於測試）
      OllamaLlmProvider.cs      ← Ollama 純對話提供者
      WebSearchLlmProvider.cs   ← 智慧路由提供者（Ollama + 網路搜尋）
    Tools/
      EchoTool.cs               ← Echo 工具
      CurrentTimeTool.cs        ← 時間查詢工具
AiAgent.Web/               ← ASP.NET Core Web 應用程式（Chat UI + REST API）
  wwwroot/                  ← 前端靜態檔案（HTML / CSS / JS）
AiAgent.Tests/              ← 19 個 xUnit 單元與整合測試
```

---

## 方案檔說明

本專案根目錄同時提供兩種 .NET 方案檔，可依所使用的開發工具版本擇一開啟：

| 檔案 | 格式 | 適用工具 |
|---|---|---|
| `AiAgent.sln` | 傳統 `.sln` 格式（Visual Studio Solution File Format Version 12.00） | Visual Studio 2019／2022、VS Code（C# Dev Kit 各版本）、JetBrains Rider、`dotnet` CLI 等所有工具 |
| `AiAgent.slnx` | 新版 XML 格式（.NET 10 引入） | 僅限支援 `.slnx` 的較新版工具 |

> **建議**：若開發工具回報「無法載入方案」或「載入失敗」，請改為開啟 `AiAgent.sln`。  
> 兩個檔案包含完全相同的四個專案，功能上沒有差異。

---

## 如何執行專案程式碼

### 前置需求

| 工具 | 最低版本 | 說明 |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 | 建置與執行所需的 SDK |
| [Ollama](https://ollama.com/) | 任意版本 | 本地 LLM 推論引擎（智慧路由所需） |
| Git | 任意版本 | 取得原始碼 |

### 步驟一：取得原始碼

```bash
git clone https://github.com/CorppassAiAgentBiz/Template4AiAgent.git
cd Template4AiAgent
```

### 步驟二：安裝並啟動 Ollama

```bash
# 安裝 Ollama（Linux / macOS）
curl -fsSL https://ollama.com/install.sh | sh

# 啟動 Ollama 服務
ollama serve &

# 下載 tinydolphin 模型（約 636MB，適合記憶體有限的環境）
ollama pull tinydolphin
```

> **模型選擇提示**：`tinydolphin` 僅需約 636MB 記憶體，適合開發與測試。  
> 若系統記憶體充足（≥ 8GB），可改用 `phi`、`mistral` 等較大的模型以獲得更好的回應品質。  
> 在 `Program.cs` 中修改 `ollamaModel` 參數即可切換模型。

### 步驟三：還原相依套件

```bash
dotnet restore
```

### 步驟四：建置整個方案

```bash
dotnet build AiAgent.sln
```

預期輸出：所有四個專案（`AiAgent.Core`、`AiAgent.Infrastructure`、`AiAgent.Web`、`AiAgent.Tests`）均顯示 `Build succeeded`，警告數為零。

### 步驟五：執行所有測試

```bash
dotnet test AiAgent.sln
```

預期輸出：

```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19
```

### 步驟六：啟動 Web Chat UI

```bash
dotnet run --project AiAgent.Web
```

啟動後在瀏覽器開啟 **http://localhost:5297** 即可與 AI Agent 對話。

#### Web UI 功能

- 即時對話介面，支援中文與英文
- 自動顯示工具呼叫結果（✅ 成功 / ❌ 失敗）
- 多輪對話記憶（同一 Session 內保留上下文）
- 支援鍵盤快捷鍵（Enter 送出、Shift+Enter 換行）

#### REST API

```bash
# POST /api/chat
curl -X POST http://localhost:5297/api/chat \
  -H "Content-Type: application/json" \
  -d '{"sessionId": "session-001", "message": "什麼是 Docker？"}'
```

回應格式：

```json
{
  "success": true,
  "content": "Docker 是一個開源的容器化平台...",
  "toolCalls": [],
  "error": null
}
```

### 步驟七（可選）：在自己的程式碼中使用 Agent

以下範例示範如何組裝並執行一個完整的 Agent：

```csharp
using AiAgent.Core.Factory;
using AiAgent.Core.Models;
using AiAgent.Core.Registry;
using AiAgent.Infrastructure.Events;
using AiAgent.Infrastructure.Llm;
using AiAgent.Infrastructure.Memory;
using AiAgent.Infrastructure.Tools;

// 1. 建立各元件（智慧路由：Ollama 意圖分類 + 網路搜尋 + 自然語言生成）
var llmProvider = new WebSearchLlmProvider(
    ollamaModel: "tinydolphin",
    ollamaBaseUrl: "http://localhost:11434");
var toolRegistry = new ToolRegistry();
var memoryStore  = new InMemoryMemoryStore();
var eventHandler = new ConsoleEventHandler();

// 2. 向登錄中註冊可用工具
toolRegistry.Register(new EchoTool());
toolRegistry.Register(new CurrentTimeTool());

// 3. 透過工廠建立 Agent
var factory = new AgentFactory(llmProvider, toolRegistry, memoryStore, [eventHandler]);
var agent   = factory.Create(new AgentOptions
{
    Name         = "MyAgent",
    Description  = "我的第一個 AI Agent",
    SystemPrompt = "你是一個樂於助人的 AI 助理。",
    MaxIterations = 10
});

// 4. 執行 Agent
var response = await agent.RunAsync(new AgentRequest
{
    SessionId   = "session-001",
    UserMessage = "什麼是機器學習？"
});

// 5. 處理回應
if (response.IsSuccess)
    Console.WriteLine($"Agent 回覆：{response.Content}");
else
    Console.WriteLine($"執行失敗：{response.ErrorMessage}");
```

### 常用指令速查

| 指令 | 說明 |
|---|---|
| `dotnet restore` | 還原 NuGet 套件 |
| `dotnet build AiAgent.sln` | 建置整個方案 |
| `dotnet test AiAgent.sln` | 執行所有測試 |
| `dotnet run --project AiAgent.Web` | 啟動 Web Chat UI（http://localhost:5297） |
| `dotnet test AiAgent.sln --filter "FullyQualifiedName~Integration"` | 只執行整合測試 |
| `dotnet test AiAgent.sln --logger "console;verbosity=detailed"` | 顯示詳細測試輸出 |
| `dotnet build AiAgent.sln -c Release` | 以 Release 模式建置 |

### 擴充 Agent 功能

| 擴充方向 | 做法 |
|---|---|
| 切換 LLM 提供者 | 更換 `WebSearchLlmProvider` 為 `OllamaLlmProvider`（純對話）或 `MockLlmProvider`（測試） |
| 接入商用 LLM | 實作 `ILlmProvider`，對接 OpenAI、Azure OpenAI、Anthropic 等 |
| 新增工具 | 實作 `ITool`，呼叫 `toolRegistry.Register(new MyTool())` |
| 新增中介軟體 | 實作 `IAgentMiddleware`，加入 `AgentPipeline` |
| 自訂 Agent 行為 | 繼承 `BaseAgent`，覆寫 `PerceiveAsync`、`ActAsync` 等方法 |
| 持久化對話歷史 | 實作 `IMemoryStore`，替換 `InMemoryMemoryStore` |
| 更換 Ollama 模型 | 修改 `Program.cs` 的 `ollamaModel` 參數（如 `phi`、`mistral`、`llama3`） |

## 📚 完整文檔

- [系統架構](docs/Architecture.md) - 分層架構、組件模型、依賴管理
- [設計文檔](docs/Design.md) - 7個設計模式、資料流設計、安全性考量
- [實現指南](docs/Implementation.md) - API 參考、擴展指南、部署說明
- [工作流程](docs/Workflow.md) - 聊天流程、完整執行路徑
- [**替代 LLM**](docs/AlternativeLLMs.md) - Ollama 的 5 個免費替代方案（Groq、LM Studio、LocalAI 等）