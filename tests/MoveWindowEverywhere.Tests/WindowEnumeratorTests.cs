using System.Diagnostics;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 窗口枚举测试。
/// 枚举会遍历系统上所有顶层窗口，其中可能包含无响应的窗口（后台挂起的程序、
/// 被冻结的 UWP 应用）。这里重点守住「枚举必须在有限时间内返回」这条底线：
/// 一旦有人把带超时的查询改回同步 SendMessage，遇到挂起窗口就会永久阻塞，
/// 而枚举运行在 UI 线程上，卡住等于整个程序失去响应。
/// </summary>
public sealed class WindowEnumeratorTests
{
    [Fact]
    public void 枚举窗口应在有限时间内完成()
    {
        using var logger = new AppLogger();
        var enumerator = new WindowEnumerator(logger);

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<WindowInfo> windows = enumerator.Enumerate();
        stopwatch.Stop();

        Assert.NotNull(windows);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 10_000,
            $"窗口枚举耗时 {stopwatch.ElapsedMilliseconds} ms，存在阻塞风险");
    }

    [Fact]
    public void 对无效句柄读取窗口信息应返回空()
    {
        using var logger = new AppLogger();
        var enumerator = new WindowEnumerator(logger);

        Assert.Null(enumerator.TryCreateWindowInfo(IntPtr.Zero));
    }
}
