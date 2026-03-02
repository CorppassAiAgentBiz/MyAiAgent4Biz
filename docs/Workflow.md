# 聊天網頁工作流程文檔

當用戶在聊天網頁輸入字串並提交時，以下是完整的執行流程。

---

## 📌 Entry Point: HTTP POST /api/chat

**檔案**: `AiAgent.Web/Program.cs` (第 85-136 行)

請求進入 `/api/chat` 端點，接收 `ChatRequest` 物件（包含 SessionId 和 Message）

---

## 🔄 完整執行流程

### **第 1 階段：Agent 建立**

```
Program.cs (第 87-93 行)
  └─ IAgentFactory.Create(agentOptions)
     檔案: AiAgent.Core/Factory/AgentFactory.cs (第 52 行)
     目的: 創建 DefaultAgent 實例

     建立以下依賴:
     ├─ WebSearchLlmProvider
     │  檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs
     │  用途: LLM 推理和搜尋決策
     │
     ├─ ToolRegistry
     │  檔案: AiAgent.Core/Registry/ToolRegistry.cs
     │  包含工具: EchoTool, CurrentTimeTool
     │
     ├─ InMemoryMemoryStore
     │  檔案: AiAgent.Infrastructure/Memory/InMemoryMemoryStore.cs
     │  用途: 會話歷史儲存
     │
     └─ ConsoleEventHandler[]
        檔案: AiAgent.Infrastructure/Events/ConsoleEventHandler.cs
        用途: 事件觀察和日誌記錄

     返回: DefaultAgent 實例
     檔案: AiAgent.Core/Agents/DefaultAgent.cs
```

---

### **第 2 階段：Agent 執行開始**

```
Program.cs (第 95-99 行)
  └─ IAgent.RunAsync(AgentRequest)
     檔案: AiAgent.Core/Agents/BaseAgent.cs (第 65 行)

     1️⃣ 建立 AgentContext
        檔案: AiAgent.Core/Models/AgentContext.cs
        封裝: Request + Agent 參考 + Items 字典

     2️⃣ 觸發事件: OnAgentStartedAsync
        └─ ConsoleEventHandler.OnAgentStartedAsync()
           檔案: AiAgent.Infrastructure/Events/ConsoleEventHandler.cs (第 35 行)
           輸出: "[Event] Agent started. Session={sessionId}"
```

---

### **第 3 階段：感知 (Perceive)**

```
BaseAgent.PerceiveAsync()
檔案: AiAgent.Core/Agents/BaseAgent.cs (第 134 行)
目的: 加載會話歷史

  └─ IMemoryStore.GetHistoryAsync(sessionId)
     檔案: AiAgent.Core/Abstractions/IMemoryStore.cs (第 34 行)
     實現: InMemoryMemoryStore.GetHistoryAsync()
     檔案: AiAgent.Infrastructure/Memory/InMemoryMemoryStore.cs (第 56 行)

     返回: List<ConversationEntry>
     檔案: AiAgent.Core/Models/ConversationEntry.cs
     包含: 過去的所有訊息 (Role: user/assistant/tool)
```

---

### **第 4 階段：主循環 (MaxIterations 次)**

```
BaseAgent.RunAsync() 內的 for 迴圈 (第 80 行)
最多執行 MaxIterations 次

每次迭代步驟:
```

#### **步驟 4.1：規劃 (Plan)**

```
BaseAgent.BuildLlmRequest()
檔案: AiAgent.Core/Agents/BaseAgent.cs (第 199 行)

建立 LlmRequest 物件:
檔案: AiAgent.Core/Models/LlmRequest.cs
包含:
  ├─ SystemPrompt (來自 AgentOptions)
  ├─ History (完整的會話歷史)
  ├─ UserMessage (用戶輸入)
  └─ AvailableTools (工具列表)

然後:
BaseAgent.PlanAsync(llmRequest)
檔案: AiAgent.Core/Agents/BaseAgent.cs (第 147 行)
  └─ ILlmProvider.CompleteAsync(llmRequest)
     檔案: AiAgent.Core/Abstractions/ILlmProvider.cs (第 21 行)
     實現: WebSearchLlmProvider.CompleteAsync()
     檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 45 行)
```

#### **🎯 LLM Provider 邏輯 (關鍵決策點)**

```
WebSearchLlmProvider.CompleteAsync()
檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 45 行)

決策流程:

1️⃣ 檢查歷史中是否有工具結果 (第 52 行)
   ├─ YES: 特殊處理
   │  │
   │  ├─ 如果是時間工具 (第 57 行)
   │  │  └─ 直接格式化台灣時間並返回
   │  │     轉換時區: Taipei Standard Time
   │  │     格式: "現在時間是：2026年02月25日 19:11:11 (二)"
   │  │     返回: LlmResponse.Success
   │  │
   │  └─ 其他工具結果
   │     └─ OllamaChatAsync() (第 67 行)
   │        摘要工具結果
   │        系統提示: "Answer user's question based on tool result"
   │        返回: LlmResponse.Success
   │
   └─ NO: 繼續到步驟 2️⃣

2️⃣ 代碼層面工具偵測 (第 75-110 行)
   程序性判斷用戶意圖

   時間查詢偵測 (第 76 行)
   ├─ 包含: "現在", "现在"
   ├─ 包含: "幾點", "几点", "時間", "时间", "日期", "date", "time"
   └─ 如果匹配: 返回 ToolCall("get_current_time")

   重複/回聲查詢偵測 (第 98 行)
   ├─ 包含: "重複", "重复", "回聲", "回声", "echo", "repeat"
   └─ 如果匹配: 返回 ToolCall("echo")

3️⃣ 如果未偵測到工具:
   └─ TranslateAndClassifyAsync() (第 153 行)
      檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 153 行)

      使用 Ollama 分析意圖:
      └─ OllamaChatAsync() (第 182 行)
         檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 326 行)

         ├─ 將中文翻譯為英文
         ├─ 決策是否需要網路搜尋
         ├─ 返回回應格式: "SEARCH: query" 或 "DIRECT: query"
         │
         └─ HTTP POST 到 Ollama API
            URL: http://localhost:11434/api/chat
            模型: tinydolphin

            如果 Ollama 失敗:
            └─ 回退: 假設 needsSearch=true

4️⃣ 決策分支 (第 115 行): 需要搜尋嗎?

   ├─ YES: SearchWebAsync() (第 118 行)
   │  檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 229 行)
   │
   │  嘗試 DuckDuckGo (第 234 行)
   │  ├─ TryDuckDuckGoAsync()
   │  │  檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 244 行)
   │  │  HTTP: https://api.duckduckgo.com/?q={query}&format=json
   │  │  提取: Abstract, Answer, 或 RelatedTopics
   │  │  返回: SearchResult (Title, Snippet, Source)
   │  │
   │  └─ 如果失敗: 嘗試 Wikipedia (第 238 行)
   │     └─ TryWikipediaSearchAsync()
   │        檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 291 行)
   │        HTTP: https://{lang}.wikipedia.org/w/api.php
   │        提取: 前 3 個搜尋結果
   │
   │  搜尋結果返回後:
   │  └─ OllamaChatAsync() 將結果翻譯為繁體中文 (第 120 行)
   │     └─ EnsureTraditionalChineseAsync() (第 412 行)
   │        驗證是否有足夠的中文字符
   │        如果不足: 再次透過 Ollama 翻譯
   │
   └─ NO: OllamaConversationAsync() (第 145 行)
      檔案: AiAgent.Infrastructure/Llm/WebSearchLlmProvider.cs (第 358 行)
      無需搜尋，直接對話
      包含完整的會話歷史
      確保繁體中文輸出

返回: LlmResponse
檔案: AiAgent.Core/Models/LlmResponse.cs
包含:
  ├─ Content (LLM 回應文本)
  ├─ ToolCall (null 或 ToolCallRequest)
  └─ HasToolCall (computed property)
```

---

#### **步驟 4.2：決策分支 - 是否有工具呼叫?**

```
BaseAgent.RunAsync() (第 85 行)

決策:
├─ NO ToolCall: 進行反思 → 跳到步驟 4.3
│
└─ YES ToolCall: 執行工具 → 跳到步驟 4.4
```

---

#### **步驟 4.3：反思 (Reflect) - 當無工具呼叫時**

```
BaseAgent.ReflectAsync()
檔案: AiAgent.Core/Agents/BaseAgent.cs (第 188 行)
目的: 後處理 LLM 輸出

預設實現: 直接返回內容不變
可在子類中覆蓋以實現自定義邏輯

返回: 最終回應內容文本

然後: 跳出循環 (第 89 行)
```

---

#### **步驟 4.4：行動 (Act) - 當有工具呼叫時**

```
BaseAgent.ActAsync()
檔案: AiAgent.Core/Agents/BaseAgent.cs (第 159 行)

1️⃣ 工具查詢 (第 162 行)
   └─ IToolRegistry.TryGet(toolName)
      檔案: AiAgent.Core/Registry/ToolRegistry.cs (第 35 行)
      實現: 在 ConcurrentDictionary 中查詢
      大小寫不敏感比對

2️⃣ 工具執行

   ├─ 如果未找到 (第 168 行)
   │  └─ 返回 ToolResult.Failure()
   │     檔案: AiAgent.Core/Models/ToolResult.cs (第 35 行)
   │     訊息: "Tool 'X' not found"
   │
   └─ 如果找到: tool.ExecuteAsync(arguments)

      可能的實現:

      🔸 EchoTool.ExecuteAsync()
         檔案: AiAgent.Infrastructure/Tools/EchoTool.cs (第 35 行)
         目的: 回顯使用者訊息
         返回: ToolResult.Success(toolName, echoed message)

      🔸 CurrentTimeTool.ExecuteAsync()
         檔案: AiAgent.Infrastructure/Tools/CurrentTimeTool.cs (第 29 行)
         目的: 取得目前 UTC 時間
         返回: ToolResult.Success(toolName, ISO 8601 timestamp)
         例如: "2026-02-25T11:11:11.0012585+00:00"

3️⃣ 觸發事件: NotifyToolCalledAsync (第 171 行)
   └─ ConsoleEventHandler.OnToolCalledAsync()
      檔案: AiAgent.Infrastructure/Events/ConsoleEventHandler.cs (第 73 行)
      輸出: "[Event] Tool '{toolName}' called. Success={status}"

4️⃣ 建立 ToolCallRecord (第 173 行)
   檔案: AiAgent.Core/Models/ToolCallRecord.cs
   記錄: ToolName, Arguments, Result, CalledAt

5️⃣ 更新會話歷史 (第 97-107 行)

   新增助手訊息:
   └─ Role: "assistant"
      Content: "[Tool Call: {toolName}]"
      檔案: AiAgent.Core/Models/ConversationEntry.cs

   新增工具結果訊息:
   └─ Role: "tool"
      Content: tool output
      ToolName: toolName

6️⃣ 持續下個迭代
   循環回到步驟 4.1 (Plan)，使用更新的歷史
```

---

### **第 5 階段：循環完成後的處理**

```
ℹ️ 循環結束條件:
   ├─ 達到 MaxIterations 次數, 或
   └─ LLM 返回無工具呼叫

當循環結束:

1️⃣ 持久化會話 (第 111-112 行)

   └─ IMemoryStore.AppendHistoryAsync(sessionId, entry)
      檔案: AiAgent.Infrastructure/Memory/InMemoryMemoryStore.cs (第 70 行)

      新增使用者訊息:
      └─ Role: "user"
         Content: 原始使用者輸入

      新增助手回應訊息:
      └─ Role: "assistant"
         Content: 最終回應文本

2️⃣ 建立回應物件 (第 114 行)

   └─ AgentResponse.Success()
      檔案: AiAgent.Core/Models/AgentResponse.cs
      包含:
      ├─ SessionId: 使用者會話識別符
      ├─ Content: 最終回應文本
      ├─ IsSuccess: true
      ├─ ToolCalls: ToolCallRecord[] (所有執行的工具)
      └─ ErrorMessage: null

3️⃣ 觸發事件: OnAgentCompletedAsync (第 115 行)

   └─ ConsoleEventHandler.OnAgentCompletedAsync()
      檔案: AiAgent.Infrastructure/Events/ConsoleEventHandler.cs (第 47 行)
      輸出: "[Event] Agent completed. Success=true, ToolCalls={count}"

4️⃣ 返回 AgentResponse
```

---

### **第 6 階段：錯誤處理**

```
如果 RunAsync() 拋出異常:

BaseAgent.RunAsync() CATCH 塊 (第 118-122 行)

1️⃣ 觸發事件: OnAgentErrorAsync

   └─ ConsoleEventHandler.OnAgentErrorAsync()
      檔案: AiAgent.Infrastructure/Events/ConsoleEventHandler.cs (第 59 行)
      輸出: "[Event] Agent error: {exception message}"

2️⃣ 返回錯誤回應

   └─ AgentResponse.Failure()
      檔案: AiAgent.Core/Models/AgentResponse.cs
      包含:
      ├─ SessionId: 使用者會話識別符
      ├─ Content: null
      ├─ IsSuccess: false
      ├─ ErrorMessage: exception 訊息
      └─ ToolCalls: [] (空)
```

---

### **第 7 階段：HTTP 回應生成**

```
回到 Program.cs (第 101-136 行)

1️⃣ 提取回應內容 (第 102 行)
   └─ response.Content

2️⃣ 附加工具呼叫結果 (第 105-122 行)

   └─ 對每個 ToolCall:
      ├─ 取得結果輸出 (第 109-110 行)
      └─ 附加到內容:
         格式: "\n[{ToolName}] {Result}"

3️⃣ 返回 JSON 回應 (第 124-135 行)

   {
     "success": boolean,
     "content": "最終回應文本（含工具結果）",
     "toolCalls": [
       {
         "toolName": "工具名稱",
         "result": "工具輸出",
         "resultSuccess": boolean
       },
       ...
     ],
     "error": "錯誤訊息（若有）" 或 null
   }
```

---

## 📊 視覺化流程圖

```
用戶輸入 "現在幾點"
         ↓
      /api/chat POST
         ↓
   AgentFactory.Create()
         ↓
   BaseAgent.RunAsync()
         ↓
   ┌────────────────────────┐
   │觸發 OnAgentStartedAsync│
   └────────────────────────┘
         ↓
   ┌─────────────┐
   │PerceiveAsync│
   │(加載會話歷史)│
   └─────────────┘
         ↓
   ┌─────────────────────────┐
   │PlanAsync                │
   │└─ WebSearchLlmProvider  │
   │   └─ 檢測時間查詢        │
   │      └─ 返回時間工具呼叫 │
   └─────────────────────────┘
         ↓
   ┌─────────────────────────────────┐
   │ActAsync                         │
   │└─ToolRegistry.TryGet()          │
   │└─CurrentTimeTool.ExecuteAsync() │
   │└─返回 UTC 時間戳                 │
   │└─OnToolCalledAsync              │
   └─────────────────────────────────┘
         ↓
   ┌───────────────────┐
   │更新歷史，再次 Plan │
   │└─ LLM 決定無需工具 │
   └───────────────────┘
         ↓
   ┌──────────────┐
   │ReflectAsync  │
   │(返回最終內容) │
   └──────────────┘
         ↓
   ┌─────────────────────┐
   │AppendHistoryAsync() │
   │(儲存會話記錄)        │
   └─────────────────────┘
         ↓
   ┌──────────────────────┐
   │OnAgentCompletedAsync │
   │(發送完成事件)         │
   └──────────────────────┘
         ↓
   返回 AgentResponse
         ↓
   構建 JSON 回應
         ↓
   返回給用戶
   "現在時間是：2026年02月25日 19:11:11 (二)"
```

---

## 🔑 關鍵類別與介面速查表

| 類別/介面           | 檔案路徑                                    | 主要方法                             | 用途 |
|--------------------|--------------------------------------------|--------------------------------------|--------------------|
|BaseAgent           |Core/Agents/BaseAgent.cs                    |RunAsync()                            |代理執行引擎           |
|DefaultAgent        |Core/Agents/DefaultAgent.cs                 | -                                    |BaseAgent 的預設實現 |
|WebSearchLlmProvider|Infrastructure/Llm/WebSearchLlmProvider.cs  |CompleteAsync()                       |LLM推理與搜尋        |
|ToolRegistry        |Core/Registry/ToolRegistry.cs               |TryGet()                              |工具註冊表             |
|ThCurrentTimeTool   |Infrastructure/Tools/CurrentTimeTool.cs     |ExecuteAsync()                        |時間查詢工具           |
|EchoTool            |Infrastructure/Tools/EchoTool.cs            |ExecuteAsync()                        |回聲工具               |
|InMemoryMemoryStore |Infrastructure/Memory/InMemoryMemoryStore.cs|GetHistoryAsync()/AppendHistoryAsync()|會話記憶儲存           |
|ConsoleEventHandler |Infrastructure/Events/ConsoleEventHandler.cs|OnAgentStartedAsync()等               |事件觀察               |
|AgentFactory        |Core/Factory/AgentFactory.cs                |Create()                              |Agent 工廠           |

---

## 💡 重要度量和設定

- **最大迭代次數**: `AgentOptions.MaxIterations = 5` (可配置)
- **LLM 模型**: `tinydolphin` (透過 Ollama)
- **Ollama 服務**: `http://localhost:11434`
- **時區**: `Taipei Standard Time` (台灣時間)
- **搜尋引擎**: DuckDuckGo (主要) / Wikipedia (備用)
- **UI 語言**: 繁體中文 (Traditional Chinese)

---

## 🎯 常見執行路徑範例

### 場景 1：時間查詢
```
"現在幾點?"
  → 代碼偵測 (時間關鍵詞)
  → 工具呼叫: get_current_time
  → 執行工具，格式化台灣時間
  → 返回格式化時間
```

### 場景 2：簡單對話
```
"你好"
  → 從 LLM 取得回應
  → 無工具呼叫
  → 直接返回 LLM 回應
```

### 場景 3：需要搜尋的查詢
```
"最新的 AI 新聞是什麼?"
  → LLM 分析需要搜尋
  → DuckDuckGo/Wikipedia 搜尋
  → 新增搜尋結果到歷史
  → 再次呼叫 LLM 進行回應
  → 返回包含搜尋資訊的回應
```

---

## 📝 註記

此工作流描述了系統在 **標準運作狀態** 下的預期行為。實際執行可能因以下因素而有所不同：

- **網路連接**: Ollama 或搜尋 API 不可用時的回退行為
- **自訂延伸**: 使用者可延伸工具、LLM 提供者或中介軟體
- **非同步取消**: CancellationToken 可隨時中止執行
- **例外處理**: 任何步驟的例外都會觸發錯誤回應

