using System.Text.Json;
using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 配置读写。默认路径 %LOCALAPPDATA%\MoveWindowEverywhere\settings.json，
/// 便于测试时注入临时路径。
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _settingsPath;

    public SettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? AppPaths.SettingsFilePath;
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                AppSettings fresh = AppSettings.CreateDefault();
                Save(fresh);
                return fresh;
            }

            string json = File.ReadAllText(_settingsPath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            settings ??= AppSettings.CreateDefault();
            settings.Hotkey ??= HotkeySettings.CreateDefault();

            if (!settings.Hotkey.IsValid)
            {
                // 配置文件被手工改坏时回退到默认值，避免程序启动后没有可用快捷键
                settings.Hotkey = HotkeySettings.CreateDefault();
            }

            return settings;
        }
        catch (Exception)
        {
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
