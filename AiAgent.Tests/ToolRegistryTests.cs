using AiAgent.Core.Registry;
using AiAgent.Infrastructure.Tools;
using Xunit;

namespace AiAgent.Tests;

/// <summary>
/// 單元測試：驗證 <see cref="ToolRegistry"/> 的工具註冊、查詢、覆蓋及列舉功能。
/// </summary>
public class ToolRegistryTests
{
    /// <summary>
    /// 驗證註冊工具後，能以相同名稱查詢到相同的工具實例。
    /// </summary>
    [Fact]
    public void Register_And_TryGet_ReturnsRegisteredTool()
    {
        var registry = new ToolRegistry();
        var tool = new EchoTool();
        registry.Register(tool);

        var found = registry.TryGet("echo", out var retrieved);

        Assert.True(found);
        Assert.Same(tool, retrieved);
    }

    /// <summary>
    /// 驗證查詢不存在的工具名稱時，回傳 <see langword="false"/> 且輸出參數為 <see langword="null"/>。
    /// </summary>
    [Fact]
    public void TryGet_UnknownTool_ReturnsFalse()
    {
        var registry = new ToolRegistry();
        var found = registry.TryGet("nonexistent", out var tool);
        Assert.False(found);
        Assert.Null(tool);
    }

    /// <summary>
    /// 驗證 <see cref="ToolRegistry.GetAll"/> 能回傳所有已註冊的工具。
    /// </summary>
    [Fact]
    public void GetAll_ReturnsAllRegisteredTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new EchoTool());
        registry.Register(new CurrentTimeTool());

        var all = registry.GetAll();
        Assert.Equal(2, all.Count);
    }

    /// <summary>
    /// 驗證以相同名稱重複註冊工具時，後者會覆蓋前者，且登錄中只保留一個實例。
    /// </summary>
    [Fact]
    public void Register_SameNameTwice_OverwritesPrevious()
    {
        var registry = new ToolRegistry();
        var tool1 = new EchoTool();
        var tool2 = new EchoTool();
        registry.Register(tool1);
        registry.Register(tool2);

        registry.TryGet("echo", out var retrieved);
        Assert.Same(tool2, retrieved);
    }
}
