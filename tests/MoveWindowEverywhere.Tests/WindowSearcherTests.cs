using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>搜索筛选规则测试。</summary>
public sealed class WindowSearcherTests
{
    private static List<WindowInfo> CreateSample()
    {
        return new List<WindowInfo>
        {
            new() { Handle = new IntPtr(1), Title = "文档.txt - 记事本", ProcessName = "notepad" },
            new() { Handle = new IntPtr(2), Title = "WorkBuddy", ProcessName = "WorkBuddy" },
            new() { Handle = new IntPtr(3), Title = "Windows PowerShell", ProcessName = "powershell" },
            new() { Handle = new IntPtr(4), Title = "资源管理器", ProcessName = "explorer" },
        };
    }

    [Fact]
    public void 空查询应保留全部并维持原顺序()
    {
        List<WindowInfo> source = CreateSample();

        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(source, null);

        Assert.Equal(4, result.Count);
        Assert.Equal("文档.txt - 记事本", result[0].Title);
        Assert.Equal("资源管理器", result[3].Title);
    }

    [Fact]
    public void 空白查询应保留全部()
    {
        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(CreateSample(), "   ");

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void 应按窗口标题筛选()
    {
        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(CreateSample(), "资源");

        Assert.Single(result);
        Assert.Equal("资源管理器", result[0].Title);
    }

    [Fact]
    public void 应按进程名筛选()
    {
        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(CreateSample(), "notepad");

        Assert.Single(result);
        Assert.Equal("notepad", result[0].ProcessName);
    }

    [Fact]
    public void 筛选应忽略大小写()
    {
        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(CreateSample(), "POWERSHELL");

        Assert.Single(result);
        Assert.Equal("powershell", result[0].ProcessName);
    }

    [Fact]
    public void 多个关键词需全部命中()
    {
        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(CreateSample(), "记事本 文档");

        Assert.Single(result);
        Assert.Equal("文档.txt - 记事本", result[0].Title);
    }

    [Fact]
    public void 无匹配时返回空列表()
    {
        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(CreateSample(), "不存在的窗口");

        Assert.Empty(result);
    }

    [Fact]
    public void 标题前缀匹配应排在标题包含匹配之前()
    {
        var source = new List<WindowInfo>
        {
            new() { Handle = new IntPtr(1), Title = "报告 - 资源管理器", ProcessName = "explorer" },
            new() { Handle = new IntPtr(2), Title = "资源管理器", ProcessName = "explorer" },
        };

        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(source, "资源");

        Assert.Equal(2, result.Count);
        Assert.Equal("资源管理器", result[0].Title);
        Assert.Equal("报告 - 资源管理器", result[1].Title);
    }

    [Fact]
    public void 进程名前缀匹配应排在进程名包含匹配之前()
    {
        var source = new List<WindowInfo>
        {
            new() { Handle = new IntPtr(1), Title = "一号窗口", ProcessName = "myexplorer" },
            new() { Handle = new IntPtr(2), Title = "二号窗口", ProcessName = "explorer" },
        };

        IReadOnlyList<WindowInfo> result = WindowSearcher.Search(source, "explorer");

        Assert.Equal(2, result.Count);
        Assert.Equal("explorer", result[0].ProcessName);
        Assert.Equal("myexplorer", result[1].ProcessName);
    }
}
