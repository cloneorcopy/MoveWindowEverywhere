using MoveWindowEverywhere.Models;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>快捷键模型与显示文本测试。</summary>
public sealed class HotkeySettingsTests
{
    [Fact]
    public void 默认快捷键应为AltZ()
    {
        HotkeySettings hotkey = HotkeySettings.CreateDefault();

        Assert.Equal("Alt + Z", hotkey.DisplayText);
        Assert.Equal(0x5Au, hotkey.VirtualKey);
        Assert.True(hotkey.IsValid);
    }

    [Fact]
    public void 缺少修饰键时应判定为无效()
    {
        var hotkey = new HotkeySettings { Modifiers = 0, VirtualKey = 0x4D };

        Assert.False(hotkey.IsValid);
    }

    [Fact]
    public void 缺少按键时应判定为无效()
    {
        var hotkey = new HotkeySettings { Modifiers = HotkeySettings.ModControl, VirtualKey = 0 };

        Assert.False(hotkey.IsValid);
    }

    [Fact]
    public void 显示文本应按CtrlAltShiftWin顺序输出()
    {
        var hotkey = new HotkeySettings
        {
            Modifiers = HotkeySettings.ModWin | HotkeySettings.ModShift | HotkeySettings.ModAlt | HotkeySettings.ModControl,
            VirtualKey = 0x31, // '1'
        };

        Assert.Equal("Ctrl + Alt + Shift + Win", hotkey.ModifierText);
        Assert.Equal("1", hotkey.KeyText);
    }

    [Fact]
    public void 注册掩码应保留Norepeat位()
    {
        HotkeySettings hotkey = HotkeySettings.CreateDefault();

        Assert.Equal(
            HotkeySettings.ModAlt | HotkeySettings.ModNoRepeat,
            hotkey.RegisterModifiers);
    }

    [Fact]
    public void 克隆应产生值相等但相互独立的对象()
    {
        HotkeySettings original = HotkeySettings.CreateDefault();
        HotkeySettings clone = original.Copy();

        Assert.Equal(original, clone);
        Assert.NotSame(original, clone);

        clone.VirtualKey = 0x4E;
        Assert.NotEqual(original, clone);
    }

    [Fact]
    public void 未设置按键时显示未设置()
    {
        var hotkey = new HotkeySettings { Modifiers = 0, VirtualKey = 0 };

        Assert.Equal("未设置", hotkey.DisplayText);
    }
}
