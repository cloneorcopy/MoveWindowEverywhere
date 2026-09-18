using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 窗口所在显示器标注测试。
/// 选择器列表里的「屏 N」标签完全由这段逻辑决定，标错会让用户把窗口移到错误的屏幕上。
/// </summary>
public sealed class WindowMonitorAnnotatorTests
{
    private static readonly IntPtr MonitorA = new(0x1001);
    private static readonly IntPtr MonitorB = new(0x1002);

    private static MonitorInfo CreateMonitor(IntPtr handle, int index) => new()
    {
        Handle = handle,
        Index = index,
        DeviceName = $@"\\.\DISPLAY{index}",
        FriendlyName = $"显示器 {index}",
        IsPrimary = index == 1,
        MonitorRect = new System.Windows.Rect(0, 0, 1920, 1080),
        WorkRect = new System.Windows.Rect(0, 0, 1920, 1040),
        DpiX = 96,
        DpiY = 96,
    };

    private static WindowInfo CreateWindow(IntPtr monitorHandle, string title = "测试窗口") => new()
    {
        Handle = new IntPtr(0x2000),
        Title = title,
        ProcessName = "test",
        MonitorHandle = monitorHandle,
    };

    [Fact]
    public void 多显示器时应标注所在屏与是否位于目标屏()
    {
        var windows = new List<WindowInfo>
        {
            CreateWindow(MonitorA, "在屏一"),
            CreateWindow(MonitorB, "在屏二"),
        };
        var monitors = new List<MonitorInfo> { CreateMonitor(MonitorA, 1), CreateMonitor(MonitorB, 2) };

        WindowMonitorAnnotator.Annotate(windows, monitors, MonitorB);

        Assert.Equal(1, windows[0].MonitorIndex);
        Assert.False(windows[0].IsOnTargetMonitor);
        Assert.Equal("屏 1", windows[0].MonitorLabel);

        Assert.Equal(2, windows[1].MonitorIndex);
        Assert.True(windows[1].IsOnTargetMonitor);
        Assert.Equal("屏 2", windows[1].MonitorLabel);
    }

    [Fact]
    public void 单显示器时不应标注()
    {
        var windows = new List<WindowInfo> { CreateWindow(MonitorA) };
        var monitors = new List<MonitorInfo> { CreateMonitor(MonitorA, 1) };

        WindowMonitorAnnotator.Annotate(windows, monitors, MonitorA);

        Assert.Equal(0, windows[0].MonitorIndex);
        Assert.Equal(string.Empty, windows[0].MonitorLabel);
    }

    [Fact]
    public void 未匹配到显示器时不应猜测()
    {
        var windows = new List<WindowInfo> { CreateWindow(new IntPtr(0x9999)) };
        var monitors = new List<MonitorInfo> { CreateMonitor(MonitorA, 1), CreateMonitor(MonitorB, 2) };

        WindowMonitorAnnotator.Annotate(windows, monitors, MonitorA);

        Assert.Equal(0, windows[0].MonitorIndex);
        Assert.False(windows[0].IsOnTargetMonitor);
        Assert.Equal(string.Empty, windows[0].MonitorLabel);
    }

    [Fact]
    public void 句柄为零的窗口应跳过()
    {
        var windows = new List<WindowInfo> { CreateWindow(IntPtr.Zero) };
        var monitors = new List<MonitorInfo> { CreateMonitor(MonitorA, 1), CreateMonitor(MonitorB, 2) };

        WindowMonitorAnnotator.Annotate(windows, monitors, MonitorA);

        Assert.Equal(0, windows[0].MonitorIndex);
    }

    [Fact]
    public void 空列表或空参数不应抛异常()
    {
        var monitors = new List<MonitorInfo> { CreateMonitor(MonitorA, 1), CreateMonitor(MonitorB, 2) };
        var oneWindow = new List<WindowInfo> { CreateWindow(MonitorA) };

        WindowMonitorAnnotator.Annotate(new List<WindowInfo>(), monitors, MonitorA);
        WindowMonitorAnnotator.Annotate(null, monitors, MonitorA);
        WindowMonitorAnnotator.Annotate(oneWindow, null, MonitorA);
        WindowMonitorAnnotator.Annotate(null, null, IntPtr.Zero);
    }

    [Fact]
    public void 未标注过的窗口标签应为空串()
    {
        WindowInfo window = CreateWindow(MonitorA, "未标注");

        Assert.Equal(0, window.MonitorIndex);
        Assert.False(window.IsOnTargetMonitor);
        Assert.Equal(string.Empty, window.MonitorLabel);
    }
}
