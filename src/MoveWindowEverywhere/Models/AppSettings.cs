using System.Text.Json.Serialization;

namespace MoveWindowEverywhere.Models;

/// <summary>
/// 应用配置。持久化位置：%LOCALAPPDATA%\MoveWindowEverywhere\settings.json
/// </summary>
public sealed class AppSettings
{
    /// <summary>配置文件格式版本，便于后续升级时迁移。</summary>
    public int Version { get; set; } = 1;

    /// <summary>唤醒窗口选择器的全局快捷键。</summary>
    public HotkeySettings Hotkey { get; set; } = HotkeySettings.CreateDefault();

    /// <summary>
    /// 是否随 Windows 启动。开启后会向
    /// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> 写入一条指向本程序的值；
    /// 关闭时会删除该值。程序不会在启动时主动关闭此开关，也不会在关闭状态下留下启动项。
    /// </summary>
    public bool StartWithWindows { get; set; }

    /// <summary>窗口列表是否包含最小化窗口。默认保留，便于把最小化的应用恢复到目标屏幕。</summary>
    public bool IncludeMinimizedWindows { get; set; } = true;

    /// <summary>窗口列表在失去焦点时是否自动关闭。</summary>
    public bool CloseSelectorOnFocusLost { get; set; } = true;

    /// <summary>
    /// 快捷键被其他程序占用时，是否自动尝试备用组合。
    /// 自动换用的是与原组合最接近的候选（先保按键换修饰键，再保修饰键换按键），
    /// 换用结果会写入配置并通过托盘气泡告知。
    /// </summary>
    public bool AutoFallbackHotkey { get; set; } = true;

    /// <summary>窗口选择器列表是否显示窗口缩略图。关闭后只显示图标，可减少打开列表时的开销。</summary>
    public bool ShowThumbnails { get; set; } = true;

    [JsonIgnore]
    public bool HasValidationIssue => !Hotkey.IsValid;

    public static AppSettings CreateDefault() => new();
}
