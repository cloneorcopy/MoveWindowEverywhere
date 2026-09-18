using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 快捷键备用组合规划测试。
/// 规划顺序决定了「快捷键被占用之后程序会自己去抢哪个组合」，
/// 顺序错了可能抢占其他程序的 Alt + 字母菜单助记符，或者撞上系统保留组合。
/// </summary>
public sealed class HotkeyFallbackPlannerTests
{
    private const uint VkZ = 0x5A;
    private const uint VkF4 = 0x73;
    private const uint VkTab = 0x09;

    [Fact]
    public void 候选项的第一项应当是用户原本设定的组合()
    {
        HotkeySettings desired = HotkeySettings.CreateDefault();

        IReadOnlyList<HotkeySettings> plan = HotkeyFallbackPlanner.Plan(desired);

        Assert.NotEmpty(plan);
        Assert.Equal(desired.Modifiers, plan[0].Modifiers);
        Assert.Equal(desired.VirtualKey, plan[0].VirtualKey);
        Assert.Equal(desired.DisplayText, plan[0].DisplayText);
    }

    [Fact]
    public void 无效快捷键不应产生任何候选()
    {
        Assert.Empty(HotkeyFallbackPlanner.Plan(null));
        Assert.Empty(HotkeyFallbackPlanner.Plan(new HotkeySettings { Modifiers = HotkeySettings.ModAlt, VirtualKey = 0 }));
        Assert.Empty(HotkeyFallbackPlanner.Plan(new HotkeySettings { Modifiers = 0, VirtualKey = VkZ }));
    }

    [Fact]
    public void 候选数量不应超过上限()
    {
        IReadOnlyList<HotkeySettings> plan = HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault(), maxCandidates: 3);

        Assert.Equal(3, plan.Count);
    }

    [Fact]
    public void 候选不应重复()
    {
        IReadOnlyList<HotkeySettings> plan = HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault());

        var seen = new HashSet<(uint, uint)>();
        foreach (HotkeySettings candidate in plan)
        {
            Assert.True(
                seen.Add((candidate.RegisterModifiers, candidate.VirtualKey)),
                $"候选 {candidate.DisplayText} 重复出现");
        }
    }

    [Fact]
    public void 优先级靠前的候选应当保留按键只改修饰键()
    {
        HotkeySettings desired = HotkeySettings.CreateDefault();
        IReadOnlyList<HotkeySettings> plan = HotkeyFallbackPlanner.Plan(desired);

        // 第二项起应先尝试保留按键、只替换修饰键，这样用户的肌肉记忆仍然管用
        HotkeySettings second = plan[1];
        Assert.Equal(desired.VirtualKey, second.VirtualKey);
        Assert.NotEqual(desired.Modifiers, second.Modifiers);
    }

    [Fact]
    public void 全部候选都应是有效的快捷键()
    {
        foreach (HotkeySettings candidate in HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault()))
        {
            Assert.True(candidate.IsValid, $"候选 {candidate.DisplayText} 不是有效快捷键");
            Assert.NotEqual(0u, candidate.VirtualKey);
            Assert.NotEqual(0u, candidate.RegisterModifiers & (HotkeySettings.ModAlt | HotkeySettings.ModControl | HotkeySettings.ModShift));
        }
    }

    [Fact]
    public void 候选应保留不重复触发的修饰位()
    {
        foreach (HotkeySettings candidate in HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault()))
        {
            Assert.True((candidate.RegisterModifiers & HotkeySettings.ModNoRepeat) != 0);
        }
    }

    [Fact]
    public void 候选不应使用Win键组合()
    {
        // Win 组合被系统外壳大量占用，抢注会让用户失去系统快捷键，注册成功率也低
        foreach (HotkeySettings candidate in HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault()))
        {
            Assert.Equal(0u, candidate.RegisterModifiers & HotkeySettings.ModWin);
        }
    }

    [Fact]
    public void 候选不应包含F4与Tab这类系统保留按键()
    {
        // Alt + F4 关窗口、Alt + Tab 切任务，这些组合被系统占用，抢注有害无益
        foreach (HotkeySettings candidate in HotkeyFallbackPlanner.Plan(new HotkeySettings
        {
            Modifiers = HotkeySettings.ModAlt | HotkeySettings.ModNoRepeat,
            VirtualKey = VkZ,
        }))
        {
            Assert.NotEqual(VkF4, candidate.VirtualKey);
            Assert.NotEqual(VkTab, candidate.VirtualKey);
        }
    }

    [Fact]
    public void 用户改为其他按键时规划结果应跟随该按键()
    {
        var desired = new HotkeySettings
        {
            Modifiers = HotkeySettings.ModControl | HotkeySettings.ModAlt | HotkeySettings.ModNoRepeat,
            VirtualKey = 0x4B, // K
        };

        IReadOnlyList<HotkeySettings> plan = HotkeyFallbackPlanner.Plan(desired);

        Assert.Equal(0x4Bu, plan[0].VirtualKey);
        Assert.Equal(desired.Modifiers, plan[0].Modifiers);

        // 第二项仍然保留用户按键
        Assert.Equal(0x4Bu, plan[1].VirtualKey);

        // 后续会出现保留用户修饰键、改成首选备用按键的候选
        Assert.Contains(
            plan,
            candidate => candidate.VirtualKey == 0x58
                && (candidate.RegisterModifiers & (HotkeySettings.ModControl | HotkeySettings.ModAlt)) != 0);
    }

    [Fact]
    public void 零上限时不应产生候选()
    {
        Assert.Empty(HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault(), maxCandidates: 0));
        Assert.Empty(HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault(), maxCandidates: -5));
    }

    [Fact]
    public void 默认上限应能覆盖按键不变的全部修饰键组合()
    {
        IReadOnlyList<HotkeySettings> plan = HotkeyFallbackPlanner.Plan(HotkeySettings.CreateDefault());

        // 用户原本的组合 + 4 组备用修饰键组合
        int sameKeyCount = plan.Count(candidate => candidate.VirtualKey == VkZ);
        Assert.Equal(5, sameKeyCount);
    }
}
