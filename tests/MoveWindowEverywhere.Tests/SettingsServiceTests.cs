using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>配置持久化测试（使用临时目录，不写入真实用户配置）。</summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsPath;

    public SettingsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"MWE-Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _settingsPath = Path.Combine(_tempDirectory, "settings.json");
    }

    [Fact]
    public void 配置不存在时应返回默认值并创建文件()
    {
        var service = new SettingsService(_settingsPath);

        AppSettings settings = service.Load();

        Assert.Equal("Alt + Z", settings.Hotkey.DisplayText);
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void 保存后应能完整读回()
    {
        var service = new SettingsService(_settingsPath);
        var settings = new AppSettings
        {
            Hotkey = new HotkeySettings
            {
                Modifiers = HotkeySettings.ModControl | HotkeySettings.ModShift,
                VirtualKey = 0x4E,
            },
            IncludeMinimizedWindows = false,
            CloseSelectorOnFocusLost = false,
            StartWithWindows = false,
        };

        service.Save(settings);
        AppSettings loaded = service.Load();

        Assert.Equal("Ctrl + Shift", loaded.Hotkey.ModifierText);
        Assert.Equal(0x4Eu, loaded.Hotkey.VirtualKey);
        Assert.False(loaded.IncludeMinimizedWindows);
        Assert.False(loaded.CloseSelectorOnFocusLost);
    }

    [Fact]
    public void 新增开关应能持久化并保留默认值()
    {
        var service = new SettingsService(_settingsPath);

        AppSettings defaults = service.Load();
        Assert.True(defaults.IncludeMinimizedWindows);
        Assert.True(defaults.CloseSelectorOnFocusLost);
        Assert.True(defaults.AutoFallbackHotkey);
        Assert.True(defaults.ShowThumbnails);
        Assert.False(defaults.StartWithWindows);

        defaults.StartWithWindows = true;
        defaults.IncludeMinimizedWindows = false;
        defaults.CloseSelectorOnFocusLost = false;
        defaults.AutoFallbackHotkey = false;
        defaults.ShowThumbnails = false;
        service.Save(defaults);

        AppSettings reloaded = service.Load();
        Assert.True(reloaded.StartWithWindows);
        Assert.False(reloaded.IncludeMinimizedWindows);
        Assert.False(reloaded.CloseSelectorOnFocusLost);
        Assert.False(reloaded.AutoFallbackHotkey);
        Assert.False(reloaded.ShowThumbnails);
    }

    [Fact]
    public void 配置文件损坏时应回退为默认快捷键()
    {
        File.WriteAllText(_settingsPath, "{ 这不是合法 JSON ");
        var service = new SettingsService(_settingsPath);

        AppSettings settings = service.Load();

        Assert.Equal("Alt + Z", settings.Hotkey.DisplayText);
    }

    [Fact]
    public void 无效的快捷键配置应回退为默认值()
    {
        File.WriteAllText(_settingsPath, "{\"Version\":1,\"Hotkey\":{\"Modifiers\":0,\"VirtualKey\":0}}");
        var service = new SettingsService(_settingsPath);

        AppSettings settings = service.Load();

        Assert.True(settings.Hotkey.IsValid);
        Assert.Equal("Alt + Z", settings.Hotkey.DisplayText);
    }

    [Fact]
    public void 默认配置目录应位于LocalApplicationData()
    {
        var service = new SettingsService();

        Assert.StartsWith(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoveWindowEverywhere"),
            service.SettingsPath);
    }

    [Fact]
    public void 保存的配置只应包含可持久化字段()
    {
        var service = new SettingsService(_settingsPath);

        service.Save(AppSettings.CreateDefault());
        string json = File.ReadAllText(_settingsPath);

        // 只读的计算属性不应进入配置文件，否则配置文件会被冗余字段淹没
        Assert.Contains("\"Modifiers\"", json);
        Assert.Contains("\"VirtualKey\"", json);
        Assert.DoesNotContain("DisplayText", json);
        Assert.DoesNotContain("ModifierText", json);
        Assert.DoesNotContain("RegisterModifiers", json);
        Assert.DoesNotContain("\"KeyText\"", json);
        Assert.DoesNotContain("\"IsValid\"", json);
    }

    [Fact]
    public void 含冗余计算属性的旧版配置文件仍应被正确读取()
    {
        File.WriteAllText(
            _settingsPath,
            """
            {
              "Version": 1,
              "Hotkey": {
                "Modifiers": 16387,
                "VirtualKey": 77,
                "IsValid": true,
                "RegisterModifiers": 16387,
                "ModifierText": "Ctrl + Alt",
                "KeyText": "M",
                "DisplayText": "Ctrl + Alt + M"
              },
              "StartWithWindows": false,
              "IncludeMinimizedWindows": true,
              "CloseSelectorOnFocusLost": true
            }
            """);

        var service = new SettingsService(_settingsPath);
        AppSettings settings = service.Load();

        Assert.Equal(0x4Du, settings.Hotkey.VirtualKey);
        Assert.Equal(HotkeySettings.ModControl | HotkeySettings.ModAlt | HotkeySettings.ModNoRepeat, settings.Hotkey.Modifiers);
        Assert.Equal("Ctrl + Alt + M", settings.Hotkey.DisplayText);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // 清理临时目录失败不影响测试结果
        }
    }
}
