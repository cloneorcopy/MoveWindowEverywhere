using System.Windows;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using MoveWindowEverywhere.ViewModels;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>选择器视图模型测试：列表装载、搜索联动、键盘导航。</summary>
public sealed class SelectorViewModelTests
{
    private static List<WindowInfo> CreateWindows()
    {
        return new List<WindowInfo>
        {
            new() { Handle = new IntPtr(1), Title = "项目计划.md", ProcessName = "Code", IsVisible = true },
            new() { Handle = new IntPtr(2), Title = "PowerShell", ProcessName = "powershell", IsVisible = true },
            new() { Handle = new IntPtr(3), Title = "资源管理器", ProcessName = "explorer", IsVisible = true },
        };
    }

    [Fact]
    public void 装载窗口后应默认选中第一项()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());

        viewModel.SetWindows(CreateWindows());

        Assert.Equal(3, viewModel.Windows.Count);
        Assert.False(viewModel.IsEmpty);
        Assert.NotNull(viewModel.SelectedWindow);
        Assert.Equal("项目计划.md", viewModel.SelectedWindow!.Title);
    }

    [Fact]
    public void 搜索文本变化应联动过滤列表()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());
        viewModel.SetWindows(CreateWindows());

        viewModel.SearchText = "power";

        Assert.Single(viewModel.Windows);
        Assert.Equal("powershell", viewModel.SelectedWindow!.ProcessName);
    }

    [Fact]
    public void 搜索无结果时应标记空列表()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());
        viewModel.SetWindows(CreateWindows());

        viewModel.SearchText = "zzz-不存在";

        Assert.Empty(viewModel.Windows);
        Assert.True(viewModel.IsEmpty);
        Assert.Null(viewModel.SelectedWindow);
    }

    [Fact]
    public void 清空搜索文本应恢复完整列表()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());
        viewModel.SetWindows(CreateWindows());
        viewModel.SearchText = "power";

        viewModel.SearchText = string.Empty;

        Assert.Equal(3, viewModel.Windows.Count);
        Assert.True(viewModel.IsSearchEmpty);
    }

    [Fact]
    public void 键盘向下导航应移动选中项且不越界()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());
        viewModel.SetWindows(CreateWindows());

        viewModel.MoveSelection(1);
        Assert.Equal("PowerShell", viewModel.SelectedWindow!.Title);

        viewModel.MoveSelection(1);
        Assert.Equal("资源管理器", viewModel.SelectedWindow!.Title);

        viewModel.MoveSelection(1);
        Assert.Equal("资源管理器", viewModel.SelectedWindow!.Title);
    }

    [Fact]
    public void 键盘向上导航应停在第一项()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());
        viewModel.SetWindows(CreateWindows());

        viewModel.MoveSelection(-1);
        Assert.Equal("项目计划.md", viewModel.SelectedWindow!.Title);
    }

    [Fact]
    public void 空列表导航不应抛异常()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());
        viewModel.SetWindows(Array.Empty<WindowInfo>());

        viewModel.MoveSelection(1);

        Assert.True(viewModel.IsEmpty);
        Assert.Null(viewModel.SelectedWindow);
    }

    [Fact]
    public void 目标显示器提示应包含显示器编号()
    {
        var viewModel = new SelectorViewModel(CreateMonitor());

        Assert.Contains("显示器 2", viewModel.TargetMonitorText);
    }

    [Fact]
    public void 未捕获显示器时应给出提示文本()
    {
        var viewModel = new SelectorViewModel(null);

        Assert.Equal("未捕获到目标显示器", viewModel.TargetMonitorText);
    }

    private static MonitorInfo CreateMonitor() => new()
    {
        Handle = new IntPtr(0x00010002),
        Index = 2,
        DeviceName = @"\\.\DISPLAY2",
        FriendlyName = "DELL P2419H",
        IsPrimary = false,
        MonitorRect = new Rect(0, 0, 1920, 1080),
        WorkRect = new Rect(0, 0, 1920, 1040),
        DpiX = 96,
        DpiY = 96,
    };
}
