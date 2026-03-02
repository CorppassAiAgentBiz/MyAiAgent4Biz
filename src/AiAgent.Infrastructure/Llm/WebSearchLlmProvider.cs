using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using AiAgent.Core.Abstractions;
using AiAgent.Core.Models;

namespace AiAgent.Infrastructure.Llm;

/// <summary>
/// 智慧路由 LLM 提供者。
/// <para>
/// 流程：
/// <list type="number">
/// <item>程式碼層工具偵測（時間查詢、Echo 等）→ 觸發工具呼叫</item>
/// <item>中文提問 → Ollama 翻譯成英文 + 意圖分類（SEARCH / DIRECT）</item>
/// <item>SEARCH → 以英文搜尋 DuckDuckGo / Wikipedia → Ollama 以繁體中文自然語言回覆</item>
/// <item>DIRECT → Ollama 直接以繁體中文自然語言回覆</item>
/// </list>
/// 完全免費：Ollama 本地模型 + DuckDuckGo/Wikipedia 公開 API。
/// </para>
/// </summary>
public sealed class WebSearchLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _ollamaBaseUrl;
    private readonly string _ollamaModel;
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ProviderName => $"WebSearch+Ollama ({_ollamaModel})";

    public WebSearchLlmProvider(string ollamaModel = "tinydolphin", string ollamaBaseUrl = "http://localhost:11434", HttpClient? httpClient = null)
    {
        _ollamaModel = ollamaModel;
        _ollamaBaseUrl = ollamaBaseUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AiAgent/1.0");
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var userMessage = request.UserMessage;
        var lowerMessage = userMessage.ToLower();
        var isChinese = ContainsChinese(userMessage);

        // ── 若歷史中有 tool 結果，用 Ollama 摘要工具結果（一律以繁體中文回覆） ──
        if (request.History?.Any(h => h.Role == "tool") == true)
        {
            var toolEntry = request.History!.Last(h => h.Role == "tool");

            // 時間工具不需要 Ollama 摘要，直接格式化並返回
            if (toolEntry.ToolName == "get_current_time")
            {
                if (DateTimeOffset.TryParse(toolEntry.Content, out var dateTime))
                {
                    var taiwanTime = TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"));
                    var formattedTime = taiwanTime.ToString("yyyy年MM月dd日 HH:mm:ss (ddd)", new System.Globalization.CultureInfo("zh-TW"));
                    return new LlmResponse { Content = $"現在時間是：{formattedTime}" };
                }
            }

            var summary = await OllamaChatAsync(
                $"Tool '{toolEntry.ToolName}' returned: {toolEntry.Content}\nUser's original question: {userMessage}\n" +
                "Answer the user's question based on the tool result. Reply in Traditional Chinese (繁體中文). Do not mention the tool name.",
                "You are a helpful AI assistant. Always reply in Traditional Chinese (繁體中文). Answer directly without mentioning tools or searches.",
                cancellationToken);
            return new LlmResponse { Content = await EnsureTraditionalChineseAsync(summary, cancellationToken) };
        }

        // ── 程式碼層工具偵測（可靠） ──
        var isTimeQuery =
            ((userMessage.Contains("現在") || userMessage.Contains("现在")) &&
             (userMessage.Contains("幾點") || userMessage.Contains("几点"))) ||
            lowerMessage.Contains("時間") || lowerMessage.Contains("时间") ||
            (lowerMessage.Contains("what") && lowerMessage.Contains("time")) ||
            (lowerMessage.Contains("when") && lowerMessage.Contains("time")) ||
            lowerMessage.Contains("current time") || lowerMessage.Contains("current_time") ||
            lowerMessage.Contains("hour") || lowerMessage.Contains("clock");

        if (isTimeQuery)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "get_current_time",
                    Arguments = new Dictionary<string, string>()
                }
            };
        }

        if (userMessage.Contains("重複") || userMessage.Contains("重复") ||
            lowerMessage.Contains("echo") || lowerMessage.Contains("repeat"))
        {
            return new LlmResponse
            {
                Content = string.Empty,
                ToolCall = new ToolCallRequest
                {
                    ToolName = "echo",
                    Arguments = new Dictionary<string, string> { ["message"] = ExtractEchoText(userMessage, lowerMessage) }
                }
            };
        }

        // ── 第一步：Ollama 翻譯成英文 + 意圖分類（合併為單一呼叫） ──
        var (englishQuery, needsSearch) = await TranslateAndClassifyAsync(userMessage, isChinese, cancellationToken);

        if (needsSearch)
        {
            // ── 第二步：以英文搜尋網路（英文搜尋品質遠優於中文） ──
            var searchResult = await SearchWebAsync(englishQuery, cancellationToken);

            if (searchResult != null)
            {
                // ── 第三步：用 Ollama 把英文搜尋結果翻譯成繁體中文自然語言回覆 ──
                var response = await OllamaChatAsync(
                    $"User's question (original): {userMessage}\n" +
                    $"User's question (English): {englishQuery}\n\n" +
                    $"Reference information:\n{searchResult.Snippet}\n\n" +
                    "Based on the reference information above, answer the user's question.\n" +
                    "You MUST reply in Traditional Chinese (繁體中文).\n" +
                    "Do NOT say \"according to search results\" — answer naturally as if you already know.\n" +
                    "Keep it concise: 2-4 sentences.",
                    "You are a knowledgeable AI assistant. Always reply in Traditional Chinese (繁體中文). Answer naturally based on the provided reference.",
                    cancellationToken);
                return new LlmResponse { Content = await EnsureTraditionalChineseAsync(response, cancellationToken) };
            }

            // 搜尋無結果，改用 Ollama 直接回覆（繁體中文）
            var fallback = await OllamaChatAsync(
                $"Question: {englishQuery}\nOriginal question: {userMessage}\nAnswer in Traditional Chinese (繁體中文).",
                "You are a helpful AI assistant. Always reply in Traditional Chinese (繁體中文).",
                cancellationToken);
            return new LlmResponse { Content = await EnsureTraditionalChineseAsync(fallback, cancellationToken) };
        }

        // ── 不需搜尋：Ollama 直接回覆（繁體中文） ──
        var directResponse = await OllamaConversationAsync(userMessage, request.History, request.SystemPrompt, cancellationToken);
        return new LlmResponse { Content = await EnsureTraditionalChineseAsync(directResponse, cancellationToken) };
    }

    // ══════════════════════════════════════════════════════
    // Ollama 翻譯 + 意圖分類（合併為單一呼叫）
    // ══════════════════════════════════════════════════════

    private async Task<(string EnglishQuery, bool NeedsSearch)> TranslateAndClassifyAsync(
        string userMessage, bool isChinese, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = isChinese
                ? "Translate the Chinese text to English, then classify.\n" +
                  "Reply in EXACTLY this format (two lines):\n" +
                  "ENGLISH: <english translation>\n" +
                  "ACTION: <SEARCH or DIRECT>\n\n" +
                  "SEARCH = needs facts/knowledge/definitions. DIRECT = casual chat/greetings/creative.\n\n" +
                  "什麼是Docker? ->\nENGLISH: What is Docker?\nACTION: SEARCH\n\n" +
                  "你好嗎？ ->\nENGLISH: How are you?\nACTION: DIRECT\n\n" +
                  "台灣的首都在哪裡？ ->\nENGLISH: Where is the capital of Taiwan?\nACTION: SEARCH\n\n" +
                  "幫我寫一首詩 ->\nENGLISH: Write me a poem\nACTION: DIRECT\n\n" +
                  $"{userMessage} ->"
                : "Classify ONLY. Reply with one word: SEARCH or DIRECT.\n" +
                  "SEARCH = needs facts/knowledge. DIRECT = casual chat.\n\n" +
                  "What is Docker? -> SEARCH\n" +
                  "Hello -> DIRECT\n" +
                  "Who is Einstein? -> SEARCH\n" +
                  "Hi -> DIRECT\n\n" +
                  $"{userMessage} ->";

            var systemPrompt = isChinese
                ? "You translate Chinese to English and classify intent. Reply EXACTLY in the format: ENGLISH: <text>\\nACTION: SEARCH or DIRECT"
                : "Reply with ONLY one word: SEARCH or DIRECT.";

            var maxTokens = isChinese ? 60 : 5;
            var result = await OllamaChatAsync(prompt, systemPrompt, cancellationToken, maxTokens);

            if (isChinese)
            {
                return ParseTranslateAndClassify(result, userMessage);
            }
            else
            {
                var needsSearch = result.Trim().ToUpper().Contains("SEARCH");
                return (userMessage, needsSearch);
            }
        }
        catch
        {
            // 失敗時：中文原文作為查詢，預設搜尋
            return (userMessage, true);
        }
    }

    private static (string EnglishQuery, bool NeedsSearch) ParseTranslateAndClassify(string response, string fallback)
    {
        var englishQuery = fallback;
        var needsSearch = true; // 預設搜尋

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("ENGLISH:", StringComparison.OrdinalIgnoreCase))
            {
                var translated = trimmed.Substring("ENGLISH:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(translated))
                    englishQuery = translated;
            }
            else if (trimmed.StartsWith("ACTION:", StringComparison.OrdinalIgnoreCase))
            {
                needsSearch = trimmed.ToUpper().Contains("SEARCH");
            }
        }

        return (englishQuery, needsSearch);
    }

    // ══════════════════════════════════════════════════════
    // 網路搜尋
    // ══════════════════════════════════════════════════════

    private async Task<SearchResult?> SearchWebAsync(string query, CancellationToken cancellationToken)
    {
        // query 已經是英文（由 TranslateAndClassifyAsync 翻譯）

        // 1. DuckDuckGo Instant Answer（英文查詢效果最佳）
        var ddg = await TryDuckDuckGoAsync(query, cancellationToken);
        if (ddg != null) return ddg;

        // 2. 英文 Wikipedia
        var wiki = await TryWikipediaSearchAsync(query, "en", cancellationToken);
        if (wiki != null) return wiki;

        return null;
    }

    private async Task<SearchResult?> TryDuckDuckGoAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var encodedQuery = HttpUtility.UrlEncode(query);
            var url = $"https://api.duckduckgo.com/?q={encodedQuery}&format=json&no_html=1&skip_disambig=1";
            var json = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var abstractText = root.GetProperty("Abstract").GetString();
            if (!string.IsNullOrWhiteSpace(abstractText))
            {
                return new SearchResult
                {
                    Title = query,
                    Snippet = abstractText,
                    Source = root.GetProperty("AbstractSource").GetString() ?? "DuckDuckGo"
                };
            }

            var answer = root.GetProperty("Answer").GetString();
            if (!string.IsNullOrWhiteSpace(answer))
            {
                return new SearchResult { Title = query, Snippet = answer, Source = "DuckDuckGo" };
            }

            if (root.TryGetProperty("RelatedTopics", out var topics) && topics.GetArrayLength() > 0)
            {
                var snippets = new List<string>();
                foreach (var topic in topics.EnumerateArray().Take(3))
                {
                    if (topic.TryGetProperty("Text", out var textProp))
                    {
                        var text = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            snippets.Add(text);
                    }
                }
                if (snippets.Count > 0)
                    return new SearchResult { Title = query, Snippet = string.Join("\n", snippets), Source = "DuckDuckGo" };
            }
        }
        catch { }
        return null;
    }

    private async Task<SearchResult?> TryWikipediaSearchAsync(string query, string lang, CancellationToken cancellationToken)
    {
        try
        {
            var encodedQuery = HttpUtility.UrlEncode(query);
            var url = $"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={encodedQuery}&utf8=1&format=json&srlimit=3";
            var json = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(json);

            var searchResults = doc.RootElement.GetProperty("query").GetProperty("search");
            if (searchResults.GetArrayLength() == 0) return null;

            var snippets = new List<string>();
            string? firstTitle = null;
            foreach (var result in searchResults.EnumerateArray().Take(3))
            {
                var title = result.GetProperty("title").GetString() ?? "";
                var snippet = result.GetProperty("snippet").GetString() ?? "";
                snippet = Regex.Replace(snippet, @"<[^>]+>", "").Trim();
                firstTitle ??= title;
                if (!string.IsNullOrWhiteSpace(snippet))
                    snippets.Add($"{title}: {snippet}");
            }

            if (snippets.Count > 0)
                return new SearchResult { Title = firstTitle ?? query, Snippet = string.Join("\n", snippets), Source = $"Wikipedia ({lang})" };
        }
        catch { }
        return null;
    }

    // ══════════════════════════════════════════════════════
    // Ollama 呼叫
    // ══════════════════════════════════════════════════════

    private async Task<string> OllamaChatAsync(string prompt, string systemPrompt, CancellationToken cancellationToken, int maxTokens = 300)
    {
        try
        {
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = prompt }
            };

            var ollamaRequest = new
            {
                model = _ollamaModel,
                messages,
                stream = false,
                options = new { temperature = 0.6, num_predict = maxTokens }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_ollamaBaseUrl}/api/chat", ollamaRequest, s_jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(jsonContent, s_jsonOptions);
            return chatResponse?.Message?.Content?.Trim() ?? "（無回應）";
        }
        catch (Exception ex)
        {
            return $"[Ollama 錯誤] {ex.Message}";
        }
    }

    private async Task<string> OllamaConversationAsync(string userMessage, IReadOnlyList<ConversationEntry>? history, string? systemPrompt, CancellationToken cancellationToken)
    {
        try
        {
            var messages = new List<object>();
            messages.Add(new
            {
                role = "system",
                content = "You are a helpful AI assistant. You MUST always reply in Traditional Chinese (繁體中文), no matter what language the user uses. " +
                          (systemPrompt ?? "")
            });

            if (history?.Count > 0)
            {
                foreach (var entry in history)
                {
                    var role = entry.Role switch { "user" => "user", "assistant" => "assistant", _ => "user" };
                    messages.Add(new { role, content = entry.Content });
                }
            }

            messages.Add(new { role = "user", content = userMessage });

            var ollamaRequest = new
            {
                model = _ollamaModel,
                messages,
                stream = false,
                options = new { temperature = 0.7, num_predict = 300 }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_ollamaBaseUrl}/api/chat", ollamaRequest, s_jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(jsonContent, s_jsonOptions);
            return chatResponse?.Message?.Content?.Trim() ?? "（無回應）";
        }
        catch (Exception ex)
        {
            return $"[Ollama 錯誤] {ex.Message}";
        }
    }

    // ══════════════════════════════════════════════════════
    // 工具
    // ══════════════════════════════════════════════════════

    private static bool ContainsChinese(string text) => Regex.IsMatch(text, @"[\u4e00-\u9fff]");

    /// <summary>
    /// 確保回覆為繁體中文。若 Ollama 回了英文，則額外做一次翻譯。
    /// </summary>
    private async Task<string> EnsureTraditionalChineseAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith("[Ollama"))
            return text;

        // 若已包含足夠中文字元，直接回傳
        var chineseCharCount = text.Count(c => c >= '\u4e00' && c <= '\u9fff');
        if (chineseCharCount >= 5)
            return text;

        // 不是中文 → 翻譯
        return await OllamaChatAsync(
            $"Translate the following text to Traditional Chinese (繁體中文). Output ONLY the translation, nothing else.\n\n{text}",
            "You are a translator. Translate to Traditional Chinese (繁體中文). Output ONLY the translated text.",
            cancellationToken, maxTokens: 400);
    }

    private static string ExtractEchoText(string userMessage, string lowerMessage)
    {
        if (userMessage.Contains('：')) return userMessage.Split("：", 2).Last().Trim();
        if (userMessage.Contains(':')) return userMessage.Split(":", 2).Last().Trim();
        if (userMessage.StartsWith("重複") || userMessage.StartsWith("重复"))
        {
            var rest = userMessage.Substring(2).Trim();
            return rest.Length > 0 ? rest : userMessage;
        }
        if (lowerMessage.StartsWith("echo ") || lowerMessage.StartsWith("repeat "))
        {
            var idx = userMessage.IndexOf(' ');
            return idx >= 0 ? userMessage.Substring(idx + 1).Trim() : userMessage;
        }
        return userMessage;
    }

    private sealed class SearchResult
    {
        public string Title { get; set; } = "";
        public string Snippet { get; set; } = "";
        public string Source { get; set; } = "";
    }
}
