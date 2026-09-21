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
    /// <summary>
    /// 应用实际走的那条路：带过滤策略的两段式枚举。
    /// 预算取 1500 ms：稳态实测在几十毫秒内，留出的余量全部让给「系统里出现一批
    /// 忙而未挂的窗口」这种最差情况；再往上就等于放行日志里出现过的 3～10 秒卡顿。
    /// </summary>
    [Fact]
    public void 带过滤策略的枚举应在有限时间内完成()
    {
        using var logger = new AppLogger();
        var enumerator = new WindowEnumerator(logger);

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<WindowInfo> windows = enumerator.Enumerate(new WindowFilterPolicy());
        stopwatch.Stop();

        Assert.NotEmpty(windows);
        Assert.All(windows, w => Assert.True(new WindowFilterPolicy().IsIncluded(w), $"窗口 {w.Title} 不该出现在列表里"));
        Assert.True(
            stopwatch.ElapsedMilliseconds < 1_500,
            $"带过滤策略的枚举耗时 {stopwatch.ElapsedMilliseconds} ms，存在阻塞风险");
    }

    /// <summary>
    /// 两段式枚举必须与「先全量采集、再交给策略过滤」给出同一批窗口。
    /// 守的是性能改动的行为等价性：先过滤再补全昂贵字段，不允许少给或多给窗口。
    /// 两次枚举之间窗口本身可能增减，因此只允许少量差异。
    /// </summary>
    [Fact]
    public void 两段式枚举结果应与先全量再过滤一致()
    {
        using var logger = new AppLogger();
        var enumerator = new WindowEnumerator(logger);
        var policy = new WindowFilterPolicy();

        IReadOnlyList<WindowInfo> fullPath = policy.Apply(enumerator.Enumerate());
        IReadOnlyList<WindowInfo> fastPath = enumerator.Enumerate(policy);

        HashSet<IntPtr> fullHandles = fullPath.Select(static w => w.Handle).ToHashSet();
        HashSet<IntPtr> fastHandles = fastPath.Select(static w => w.Handle).ToHashSet();

        int difference = fastHandles.Except(fullHandles).Count() + fullHandles.Except(fastHandles).Count();

        Assert.True(fullHandles.Count > 0, "全量路径未产出任何窗口，测试环境不成立");
        Assert.True(
            difference <= 5,
            $"两段式与全量结果差异 {difference} 个窗口：{string.Join(", ", fastHandles.Except(fullHandles).Concat(fullHandles.Except(fastHandles)).Select(h => $"0x{h:X}"))}");
    }

    /// <summary>
    /// 全量枚举（不过滤，每个窗口都补齐全部字段）仍然可用，用于兜底和对照。
    /// 它天然比两段式慢，这里只守住「不会永久阻塞」。
    /// </summary>
    [Fact]
    public void 全量枚举窗口应在有限时间内完成()
    {
        using var logger = new AppLogger();
        var enumerator = new WindowEnumerator(logger);

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<WindowInfo> windows = enumerator.Enumerate();
        stopwatch.Stop();

        Assert.NotNull(windows);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 10_000,
            $"全量窗口枚举耗时 {stopwatch.ElapsedMilliseconds} ms，存在阻塞风险");
    }

    [Fact]
    public void 对无效句柄读取窗口信息应返回空()
    {
        using var logger = new AppLogger();
        var enumerator = new WindowEnumerator(logger);

        Assert.Null(enumerator.TryCreateWindowInfo(IntPtr.Zero));
    }
}
