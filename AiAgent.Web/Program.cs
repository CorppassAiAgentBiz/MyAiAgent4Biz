using AiAgent.Core.Abstractions;
using AiAgent.Core.Factory;
using AiAgent.Core.Models;
using AiAgent.Core.Registry;
using AiAgent.Web;
using AiAgent.Infrastructure.Events;
using AiAgent.Infrastructure.Llm;
using AiAgent.Infrastructure.Memory;
using AiAgent.Infrastructure.Tools;
using AiAgent.Infrastructure.Robots;

var builder = WebApplication.CreateBuilder(args);

// 配置 Agent 依賴注入
// 從 appsettings.json 讀取 Groq API 設置
var groqApiKey = builder.Configuration["LlmProvider:Groq:ApiKey"];
var groqModel = builder.Configuration["LlmProvider:Groq:Model"] ?? "llama-3.1-8b-instant";
var llmProvider = new GroqLlmProvider(
    apiKey: groqApiKey,
    model: groqModel);  // 超快模型，推薦！
var toolRegistry = new ToolRegistry();
var memoryStore = new InMemoryMemoryStore();
var eventHandler = new ConsoleEventHandler();

// 註冊工具
toolRegistry.Register(new EchoTool());
toolRegistry.Register(new CurrentTimeTool());

// 註冊視覺工具（圖片分析）
var visionProvider = new MockVisionProvider();  // 替換為真實實現（如 ClaudeVisionProvider）
var imageAnalysisTool = new ImageAnalysisTool(visionProvider);
toolRegistry.Register(imageAnalysisTool);

// 註冊機器人控制工具（可選，基於配置）
var robotBaseUrl = builder.Configuration["Robot:BaseUrl"];
if (!string.IsNullOrEmpty(robotBaseUrl))
{
    var robotHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var robotClient = new HttpRobotClient(robotHttpClient, robotBaseUrl);
    var robotControlTool = new RobotControlTool(robotClient);
    toolRegistry.Register(robotControlTool);
}

// 建立並註冊 Agent 工廠
var agentFactory = new AgentFactory(llmProvider, toolRegistry, memoryStore, [eventHandler]);
builder.Services.AddSingleton<IAgentFactory>(agentFactory);

// 添加 CORS 支持
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// 设置 wwwroot 路径 - 考虑运行上下文
var currentDir = Directory.GetCurrentDirectory();
var wwwrootPath = Path.Combine(currentDir, "AiAgent.Web", "wwwroot");

// 如果从 AiAgent.Web 目录运行，尝试本地 wwwroot
if (!Directory.Exists(wwwrootPath))
{
    wwwrootPath = Path.Combine(currentDir, "wwwroot");
}

// 如果仍然不存在，使用 AppContext 基础目录
if (!Directory.Exists(wwwrootPath))
{
    wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
}

// 确保目录存在，如果不存在则创建
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}

var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider,
    RequestPath = ""
});

// 靜態文件根路徑
app.MapGet("/", (HttpContext context) =>
{
    var indexPath = Path.Combine(wwwrootPath, "index.html");
    if (File.Exists(indexPath))
    {
        return Results.Content(File.ReadAllText(indexPath), "text/html");
    }
    context.Response.StatusCode = 404;
    return Results.Text("Error: index.html not found at " + indexPath, "text/plain");
});

// API 端點：獲取初始問候
app.MapPost("/api/greet", async (ChatRequest request, IAgentFactory factory) =>
{
    // 自動檢測用戶輸入的語言（如果有），或預設為中文
    var detectedLanguage = !string.IsNullOrEmpty(request.Message)
        ? LanguageDetectionHelper.DetectLanguage(request.Message)
        : LanguageDetectionHelper.Language.Chinese;
    var languageInstructions = LanguageDetectionHelper.GetLanguageInstructions(detectedLanguage);

    var agent = factory.Create(new AgentOptions
    {
        Name = "ChatAgent",
        Description = "互動式對話 AI 助理",
        SystemPrompt = $@"你是一個樂於助人的 AI 助理。請簡潔地回答問題。

你可以使用以下工具：
- get_current_time：獲取當前台灣時間
- echo：回應使用者的訊息

當用戶詢問時間相關問題時，請務必使用 get_current_time 工具。

{languageInstructions}",
        MaxIterations = 5
    });

    // 生成問候訊息
    var response = await agent.RunAsync(new AgentRequest
    {
        SessionId = request.SessionId,
        UserMessage = "請用 1-2 句話簡潔地介紹你自己，並提示用戶可以詢問的事項。"
    });

    return Results.Json(new
    {
        success = response.IsSuccess,
        content = response.Content,
        error = response.ErrorMessage
    });
});

// API 端點：執行 Agent
app.MapPost("/api/chat", async (ChatRequest request, IAgentFactory factory) =>
{
    // 自動檢測用戶輸入的語言
    var detectedLanguage = LanguageDetectionHelper.DetectLanguage(request.Message);
    var languageInstructions = LanguageDetectionHelper.GetLanguageInstructions(detectedLanguage);

    // 動態構建系統提示詞
    var systemPrompt = $@"你是一個樂於助人的 AI 助理。請簡潔地回答問題。

你可以使用以下工具：
- get_current_time：獲取當前台灣時間
- echo：回應使用者的訊息
- analyze_image：分析上傳的圖片內容";

    // 如果啟用了機器人，添加機器人工具描述
    if (!string.IsNullOrEmpty(robotBaseUrl))
    {
        systemPrompt += @"
- control_robot：控制遠程機器人
  支援命令：move_forward, move_backward, turn_left, turn_right, stop, grab, release
  例：{""command"": ""move_forward"", ""parameters"": ""distance=10""}";
    }

    systemPrompt += $@"

當用戶詢問時間相關問題時，請務必使用 get_current_time 工具。
當用戶上傳圖片時，自動使用 analyze_image 工具進行分析。
{(string.IsNullOrEmpty(robotBaseUrl) ? "" : "當用戶請求控制機器人時，使用 control_robot 工具。\n")}
{languageInstructions}";

    var agent = factory.Create(new AgentOptions
    {
        Name = "ChatAgent",
        Description = "互動式對話 AI 助理",
        SystemPrompt = systemPrompt,
        MaxIterations = 5
    });

    // 處理圖片上傳
    var userMessage = request.Message;
    var metadata = new Dictionary<string, string>();

    if (!string.IsNullOrEmpty(request.ImageBase64))
    {
        // 在消息中附加圖片分析請求
        userMessage = string.IsNullOrEmpty(request.Message)
            ? "請分析這張圖片的內容"
            : $"{request.Message}\n\n請同時分析剛剛上傳的圖片。";

        // 將圖片 Base64 存儲在元數據中，供工具訪問
        metadata["image_base64"] = request.ImageBase64;
    }

    var response = await agent.RunAsync(new AgentRequest
    {
        SessionId = request.SessionId,
        UserMessage = userMessage,
        Metadata = metadata
    });

    // 構建回應：包含主要內容和工具呼叫結果
    var content = response.Content;
    
    // 如果有工具呼叫，將其結果附加到回應中
    if (response.ToolCalls?.Count > 0)
    {
        foreach (var toolCall in response.ToolCalls)
        {
            var toolResultText = toolCall.Result?.IsSuccess == true 
                ? toolCall.Result.Output 
                : $"工具 {toolCall.ToolName} 執行失敗";
            
            if (string.IsNullOrWhiteSpace(content))
            {
                content = $"[{toolCall.ToolName}] {toolResultText}";
            }
            else
            {
                content += $"\n[{toolCall.ToolName}] {toolResultText}";
            }
        }
    }

    return Results.Json(new 
    { 
        success = response.IsSuccess, 
        content = content,
        toolCalls = response.ToolCalls?.Select(tc => new
        {
            toolName = tc.ToolName,
            result = tc.Result?.Output,
            resultSuccess = tc.Result?.IsSuccess
        }).ToList(),
        error = response.ErrorMessage 
    });
});

app.Run();
