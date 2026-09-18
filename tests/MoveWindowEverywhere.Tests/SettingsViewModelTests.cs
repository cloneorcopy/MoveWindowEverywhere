using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.ViewModels;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 设置窗口视图模型测试。
/// 这些开关决定了快捷键、开机自启、最小化窗口与缩略图的实际行为，
/// 一旦 ToChange 漏掉某一项，界面上的改动就会被静默丢弃。
/// </summary>
public sealed class SettingsViewModelTests
{
    private static SettingsViewModel CreateViewModel(bool canRegisterStartup = true) => new(
        HotkeySettings.CreateDefault(),
        startWithWindows: false,
        includeMinimizedWindows: true,
        closeSelectorOnFocusLost: true,
        autoFallbackHotkey: true,
        showThumbnails: true,
        canRegisterStartup);

    [Fact]
    public void 初始状态应反映传入的设置()
    {
        var viewModel = new SettingsViewModel(
            new HotkeySettings { Modifiers = HotkeySettings.ModControl | HotkeySettings.ModAlt, VirtualKey = 0x4B },
            startWithWindows: true,
            includeMinimizedWindows: false,
            closeSelectorOnFocusLost: false,
            autoFallbackHotkey: false,
            showThumbnails: false);

        Assert.Equal("Ctrl + Alt + K", viewModel.PendingHotkeyText);
        Assert.True(viewModel.StartWithWindows);
        Assert.False(viewModel.IncludeMinimizedWindows);
        Assert.False(viewModel.CloseSelectorOnFocusLost);
        Assert.False(viewModel.AutoFallbackHotkey);
        Assert.False(viewModel.ShowThumbnails);
    }

    [Fact]
    public void 打包结果应包含全部开关()
    {
        SettingsViewModel viewModel = CreateViewModel();

        viewModel.StartWithWindows = true;
        viewModel.IncludeMinimizedWindows = false;
        viewModel.CloseSelectorOnFocusLost = false;
        viewModel.AutoFallbackHotkey = false;
        viewModel.ShowThumbnails = false;

        SettingsChange change = viewModel.ToChange();

        Assert.True(change.StartWithWindows);
        Assert.False(change.IncludeMinimizedWindows);
        Assert.False(change.CloseSelectorOnFocusLost);
        Assert.False(change.AutoFallbackHotkey);
        Assert.False(change.ShowThumbnails);
        Assert.Equal(viewModel.Pending.DisplayText, change.Hotkey.DisplayText);
    }

    [Fact]
    public void 打包结果的快捷键应是副本()
    {
        SettingsViewModel viewModel = CreateViewModel();

        SettingsChange change = viewModel.ToChange();
        viewModel.Pending = new HotkeySettings { Modifiers = HotkeySettings.ModShift | HotkeySettings.ModAlt, VirtualKey = 0x58 };

        Assert.NotEqual(viewModel.Pending.DisplayText, change.Hotkey.DisplayText);
    }

    [Fact]
    public void 无法登记开机自启时该开关应保持关闭()
    {
        var viewModel = new SettingsViewModel(
            HotkeySettings.CreateDefault(),
            startWithWindows: true,
            includeMinimizedWindows: true,
            closeSelectorOnFocusLost: true,
            autoFallbackHotkey: true,
            showThumbnails: true,
            canRegisterStartup: false);

        Assert.False(viewModel.CanRegisterStartup);
        Assert.False(viewModel.StartWithWindows);
        Assert.Contains("无法设置开机自启", viewModel.StartupHint);

        // 用户把开关打开也不应生效，避免写入无意义的启动项
        viewModel.StartWithWindows = true;
        Assert.False(viewModel.StartWithWindows);
        Assert.False(viewModel.ToChange().StartWithWindows);
    }

    [Fact]
    public void 可以登记开机自启时应给出正常说明()
    {
        SettingsViewModel viewModel = CreateViewModel();

        Assert.True(viewModel.CanRegisterStartup);
        Assert.Contains("注册表", viewModel.StartupHint);
    }

    [Fact]
    public void 修改开关应触发属性变更通知()
    {
        SettingsViewModel viewModel = CreateViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.AutoFallbackHotkey = false;
        viewModel.ShowThumbnails = false;
        viewModel.IncludeMinimizedWindows = false;
        viewModel.CloseSelectorOnFocusLost = false;
        viewModel.StartWithWindows = true;

        Assert.Contains(nameof(SettingsViewModel.AutoFallbackHotkey), changed);
        Assert.Contains(nameof(SettingsViewModel.ShowThumbnails), changed);
        Assert.Contains(nameof(SettingsViewModel.IncludeMinimizedWindows), changed);
        Assert.Contains(nameof(SettingsViewModel.CloseSelectorOnFocusLost), changed);
        Assert.Contains(nameof(SettingsViewModel.StartWithWindows), changed);
    }

    [Fact]
    public void 值未变化时不应重复触发通知()
    {
        SettingsViewModel viewModel = CreateViewModel();
        int notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.AutoFallbackHotkey = true; // 与初始值相同
        viewModel.ShowThumbnails = true;

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void 修改快捷键应同时通知显示文本()
    {
        SettingsViewModel viewModel = CreateViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Pending = new HotkeySettings { Modifiers = HotkeySettings.ModShift | HotkeySettings.ModAlt, VirtualKey = 0x58 };

        Assert.Contains(nameof(SettingsViewModel.Pending), changed);
        Assert.Contains(nameof(SettingsViewModel.PendingHotkeyText), changed);
        Assert.Equal("Alt + Shift + X", viewModel.PendingHotkeyText);
    }

    [Fact]
    public void 错误信息应可读写并触发通知()
    {
        SettingsViewModel viewModel = CreateViewModel();
        string? lastChanged = null;
        viewModel.PropertyChanged += (_, e) => lastChanged = e.PropertyName;

        viewModel.ErrorMessage = "快捷键已被占用";

        Assert.Equal("快捷键已被占用", viewModel.ErrorMessage);
        Assert.Equal(nameof(SettingsViewModel.ErrorMessage), lastChanged);
    }

    [Fact]
    public void 打包结果应为记录值类型且字段可比较()
    {
        SettingsViewModel viewModel = CreateViewModel();

        Assert.Equal(viewModel.ToChange(), viewModel.ToChange());
    }
}
