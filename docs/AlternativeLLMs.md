# 替代 Ollama 的免費 LLM 選項

本文檔提供了 5 個替代 Ollama 的免費 LLM 服務及本地解決方案，並提供集成指南。

## 📊 對比表

| 方案 | 類型 | 成本 | 速度 | 部署難度 | 推薦度 |
|------|------|------|------|--------|-------|
| **LM Studio** | 本地 | 完全免費 | 快 | 簡單 | ⭐⭐⭐⭐⭐ |
| **GPT4All** | 本地 | 完全免費 | 中等 | 簡單 | ⭐⭐⭐⭐ |
| **Groq API** | 雲端 | 免費層 (500K tokens/day) | 極快 | 簡單 | ⭐⭐⭐⭐⭐ |
| **HuggingFace Inference** | 雲端 | 免費層 (限速) | 中等 | 簡單 | ⭐⭐⭐ |
| **LocalAI** | 本地 | 開源免費 | 快 | 中等 | ⭐⭐⭐⭐ |

---

## 方案 1: LM Studio (推薦 - 本地)

### 特點
- ✅ 完全本地執行，無網路依賴
- ✅ GUI 簡單友善，一鍵下載模型
- ✅ 兼容 OpenAI API 格式
- ✅ 支援 Mac / Windows / Linux
- ✅ 完全免費

### 安裝步驟

**1. 下載 LM Studio**
```bash
https://lmstudio.ai/
# 選擇你的作業系統並安裝
```

**2. 下載模型**
```
啟動 LM Studio GUI
→ Library (搜尋模型)
→ 搜尋 "mistral" 或 "neural-chat"
→ 點擊下載
```

推薦模型:
- `mistral-7b-instruct` (最平衡，快速)
- `neural-chat-7b` (對話優化)
- `orca-mini-7b` (快速、可靠)

**3. 啟動伺服器**
```
進入 "Local Server" 標籤
→ 選擇模型
→ "Start Server"
→ 預設: http://localhost:1234
```

### 集成到 AiAgent

**修改 Program.cs**:

```csharp
// 改用 LM Studio (相容 OpenAI)
var llmProvider = new LMStudioLlmProvider(
    baseUrl: "http://localhost:1234/v1",
    defaultModel: "mistral-7b-instruct");

// 或保持 Ollama 兼容
var llmProvider = new OllamaLikeProvider(
    baseUrl: "http://localhost:1234",
    model: "mistral-7b-instruct");
```

**建立 LMStudioLlmProvider.cs**:

```csharp
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;
using System.Text.Json;

public sealed class LMStudioLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _defaultModel;

    public string ProviderName => "LM Studio (Local OpenAI-compatible)";

    public LMStudioLlmProvider(string baseUrl = "http://localhost:1234/v1",
                              string defaultModel = "mistral-7b-instruct")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _defaultModel = defaultModel;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        foreach (var entry in request.History)
        {
            messages.Add(new { role = entry.Role, content = entry.Content });
        }

        messages.Add(new { role = "user", content = request.UserMessage });

        var payload = new
        {
            model = _defaultModel,
            messages = messages,
            temperature = 0.7,
            max_tokens = 1000
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/chat/completions",
            payload,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<OpenAIResponse>(content);

        return new LlmResponse
        {
            Content = result?.Choices[0]?.Message?.Content ?? "No response"
        };
    }
}

public class OpenAIResponse
{
    public Choice[]? Choices { get; set; }
}

public class Choice
{
    public Message? Message { get; set; }
}

public class Message
{
    public string? Content { get; set; }
}
```

### 性能指標
- 回應時間: 1-5 秒 (視模型和硬體)
- 成本: 完全免費
- 隱私: 100% 本地，無資料上傳

---

## 方案 2: Groq API (推薦 - 雲端，最快)

### 特點
- ✅ **極快推理速度** (最快且免費的 API)
- ✅ 免費層: 每日 500K tokens
- ✅ 支援多個模型 (Mixtral、LLaMA)
- ✅ 無信用卡，直接註冊
- ✅ API 兼容標準 REST

### 註冊與設置

**1. 建立帳號**
```
https://console.groq.com/
→ 使用 GitHub 或 Google 帳號登錄
→ 複製 API Key
```

**2. 設定環境變數**
```bash
export GROQ_API_KEY=your_api_key_here
```

### 集成到 AiAgent

**建立 GroqLlmProvider.cs**:

```csharp
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;
using System.Text.Json;

public sealed class GroqLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.groq.com/openai/v1/chat/completions";

    // 免費層可用模型
    private const string DefaultModel = "mixtral-8x7b-32768";  // 快速且免費
    // 其他選項: "llama-2-70b-chat", "gemma-7b-it"

    public string ProviderName => "Groq API (Ultra-fast, Free)";

    public GroqLlmProvider(string? apiKey = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")!;
        if (string.IsNullOrEmpty(_apiKey))
            throw new ArgumentException("GROQ_API_KEY not set");

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        foreach (var entry in request.History.TakeLast(10))  // 限制歷史以節省 token
        {
            messages.Add(new { role = entry.Role, content = entry.Content });
        }

        messages.Add(new { role = "user", content = request.UserMessage });

        var payload = new
        {
            model = DefaultModel,
            messages = messages,
            temperature = 0.7,
            max_tokens = 1024
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(BaseUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Groq API error: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<GroqResponse>(responseBody);
        return new LlmResponse
        {
            Content = result?.Choices[0]?.Message?.Content ?? "No response"
        };
    }
}

public class GroqResponse
{
    public GroqChoice[]? Choices { get; set; }
}

public class GroqChoice
{
    public GroqMessage? Message { get; set; }
}

public class GroqMessage
{
    public string? Content { get; set; }
}
```

**修改 Program.cs**:

```csharp
var llmProvider = new GroqLlmProvider(
    apiKey: Environment.GetEnvironmentVariable("GROQ_API_KEY"));

var agentFactory = new AgentFactory(llmProvider, toolRegistry, memoryStore, handlers);
```

### 性能指標
- 回應時間: **0.2-1 秒** (業界最快)
- 每日免費額度: **500,000 tokens**
- 成本: 免費 (超過額度後計費)

### 計算額度使用
```
平均聊天: 100-500 tokens
500K tokens ≈ 1000-5000 次對話
充足運行一整月的 Demo 應用
```

---

## 方案 3: GPT4All (本地)

### 特點
- ✅ 完全本地，無網路
- ✅ 支援 GPU 加速
- ✅ 自動下載和管理模型
- ✅ C# 官方支援

### 安裝步驟

**1. 安裝 GPT4All**
```bash
# Windows / macOS / Linux
https://www.nomic.ai/gpt4all
```

**2. Python 伺服器 (提供 API)**
```bash
pip install gpt4all

# 啟動 API 伺服器
gpt4all --listen 0.0.0.0:8000
```

### 集成到 AiAgent

**建立 GPT4AllLlmProvider.cs**:

```csharp
public sealed class GPT4AllLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public string ProviderName => "GPT4All (Local)";

    public GPT4AllLlmProvider(string baseUrl = "http://localhost:8000",
                             string model = "mistral-7b-instruct-v0.1.Q4_0")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _model,
            prompt = $"{request.SystemPrompt}\n\n{request.UserMessage}",
            temperature = 0.7,
            max_tokens = 1000,
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/v1/completions",
            payload,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<CompletionResponse>(content);

        return new LlmResponse
        {
            Content = result?.Choices[0]?.Text ?? "No response"
        };
    }
}
```

---

## 方案 4: HuggingFace Inference API (免費層)

### 特點
- ✅ 無須註冊信用卡
- ✅ 支援數千個模型
- ✅ 免費層有限速但免費
- ✅ 適合實驗

### 設置

**1. 獲取 Token**
```
https://huggingface.co/settings/tokens
→ 建立讀的 token
```

**2. 集成**

```csharp
public sealed class HuggingFaceProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api-inference.huggingface.co/models";

    public HuggingFaceProvider(string? apiKey = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("HF_TOKEN")!;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // 使用開源模型如 Mistral
        const string model = "mistralai/Mistral-7B-Instruct-v0.1";

        var payload = new
        {
            inputs = $"{request.SystemPrompt}\n{request.UserMessage}"
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{BaseUrl}/{model}",
            payload,
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        // 解析並返回
        return new LlmResponse { Content = "..." };
    }
}
```

---

## 方案 5: LocalAI (開源本地)

### 特點
- ✅ 完全開源
- ✅ Docker 部署簡單
- ✅ 兼容 OpenAI API
- ✅ 支援多個模型

### Docker 部署

```bash
docker run -p 8080:8080 -v ~/.cache:/root/.cache \
  localai/localai:latest-aio-cpu \
  --models-path=/models/

# 或使用 GPU
docker run -p 8080:8080 --gpus all \
  localai/localai:latest-gpu
```

### 集成

```csharp
var llmProvider = new LocalAIProvider(
    baseUrl: "http://localhost:8080",
    model: "mistral-7b");
```

---

## 比較與推薦

### 場景 1: 快速原型開發
**推薦**: **Groq API**
- 無需本地部署
- 極快速度
- 免費額度充足

### 場景 2: 離線運行 / 隱私優先
**推薦**: **LM Studio**
- 簡單易用
- 完全本地
- 無須 GPU

### 場景 3: 生產環境 + 成本控制
**推薦**: **LocalAI + GPU**
- 完全開源
- 可長期運行
- 一次性投資

### 場景 4: 詳細研究 / 各模型測試
**推薦**: **HuggingFace**
- 模型選擇多
- 適合實驗

---

## 實現檢查清單

### 選擇 LM Studio
- [ ] 下載並安裝 LM Studio
- [ ] 下載 `mistral-7b` 模型
- [ ] 啟動本地伺服器 (localhost:1234)
- [ ] 建立 `LMStudioLlmProvider.cs`
- [ ] 修改 `Program.cs` 使用新 provider
- [ ] 測試聊天功能

### 選擇 Groq API
- [ ] 在 https://console.groq.com 註冊
- [ ] 複製 API Key
- [ ] 設定 `GROQ_API_KEY` 環境變數
- [ ] 建立 `GroqLlmProvider.cs`
- [ ] 修改 `Program.cs`
- [ ] 測試並監控 token 使用

### 選擇 LocalAI
- [ ] 安裝 Docker
- [ ] 執行 LocalAI 容器
- [ ] 建立 `LocalAIProvider.cs` (兼容 OpenAI)
- [ ] 修改 `Program.cs`
- [ ] 測試

---

## 性能對比

```
速度排序:
1. Groq API         (0.2-1s)     ⚡⚡⚡⚡⚡
2. LM Studio        (1-5s)       ⚡⚡⚡⚡
3. LocalAI          (2-10s)      ⚡⚡⚡
4. HuggingFace      (3-15s)      ⚡⚡
5. GPT4All          (2-8s)       ⚡⚡⚡

成本排序:
1. Ollama           ($0)  - 本地
2. LM Studio        ($0)  - 本地
3. GPT4All          ($0)  - 本地
4. LocalAI          ($0)  - 本地
5. Groq             ($0)  - 免費層
6. HuggingFace      ($0)  - 免費層 (限速)
```

---

## 遷移建議

**最簡單的遷移路徑**:

1. **立即試用** (5分鐘)
   ```bash
   # Groq API
   export GROQ_API_KEY=你的key
   # 修改 Program.cs 使用 GroqLlmProvider
   ```

2. **長期本地方案** (10分鐘)
   ```bash
   # 下載 LM Studio
   # 選擇模型和啟動
   # 修改 Program.cs 使用 LMStudioLlmProvider
   ```

3. **完全離線 + 自主** (20分鐘)
   ```bash
   # Docker LocalAI
   # 配置並修改 Program.cs
   ```

---

## 故障排除

**問題**: Groq API 返回 401 Unauthorized
**解決**:
```bash
# 確認 API Key
echo $GROQ_API_KEY

# 或在代碼中明確傳遞
var provider = new GroqLlmProvider(apiKey: "您的實際key");
```

**問題**: LM Studio 連接超時
**解決**:
1. 確認 LM Studio 伺服器已啟動
2. 檢查 URL: http://localhost:1234
3. 更新超時時間: `httpClient.Timeout = TimeSpan.FromSeconds(60)`

**問題**: 模型下載緩慢
**解決**:
- LM Studio: 在 GUI 中手動下載
- GPT4All: `gpt4all models --add mistral-7b`
- LocalAI: 預先下載: `docker pull localai/localai:model-mistral`

---

## 下一步

選擇一個方案並集成到你的 AiAgent:

1. **快速開始**: 使用 Groq API (無需本地部署)
2. **生產環保**: 使用 LM Studio 或 LocalAI (完全本地)
3. **評估多個**: 實現抽象層支援多個提供者切換

所有實現都遵循 `ILlmProvider` 介面，可以輕鬆切換！

