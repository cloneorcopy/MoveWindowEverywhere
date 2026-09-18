using System.ComponentModel;
using System.Runtime.CompilerServices;
using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.ViewModels;

/// <summary>设置窗口返回给宿主的完整改动。</summary>
public sealed record SettingsChange(
    HotkeySettings Hotkey,
    bool StartWithWindows,
    bool IncludeMinimizedWindows,
    bool CloseSelectorOnFocusLost,
    bool AutoFallbackHotkey,
    bool ShowThumbnails);

/// <summary>设置窗口的视图模型：全局快捷键与全部开关。</summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private HotkeySettings _pending;
    private string _errorMessage = string.Empty;
    private bool _startWithWindows;
    private bool _includeMinimizedWindows;
    private bool _closeSelectorOnFocusLost;
    private bool _autoFallbackHotkey;
    private bool _showThumbnails;

    public SettingsViewModel(
        HotkeySettings pending,
        bool startWithWindows,
        bool includeMinimizedWindows,
        bool closeSelectorOnFocusLost,
        bool autoFallbackHotkey,
        bool showThumbnails,
        bool canRegisterStartup = true)
    {
        _pending = pending;
        _startWithWindows = startWithWindows;
        _includeMinimizedWindows = includeMinimizedWindows;
        _closeSelectorOnFocusLost = closeSelectorOnFocusLost;
        _autoFallbackHotkey = autoFallbackHotkey;
        _showThumbnails = showThumbnails;
        CanRegisterStartup = canRegisterStartup;
    }

    public HotkeySettings Pending
    {
        get => _pending;
        set
        {
            _pending = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PendingHotkeyText));
        }
    }

    public string PendingHotkeyText => _pending.DisplayText;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>是否随 Windows 启动。写注册表 HKCU 的 Run 项，不需要管理员权限。</summary>
    public bool StartWithWindows
    {
        get => _startWithWindows && CanRegisterStartup;
        set
        {
            if (_startWithWindows == value)
            {
                return;
            }

            _startWithWindows = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前进程是否可以登记开机自启。通过 <c>dotnet run</c> 启动时进程路径指向 dotnet.exe，
    /// 写进启动项没有意义，此时该开关整体禁用并在界面上说明原因。
    /// </summary>
    public bool CanRegisterStartup { get; }

    public string StartupHint => CanRegisterStartup
        ? "在注册表的启动项中登记本程序，不需要管理员权限。"
        : "当前不是直接运行发布后的 exe，无法设置开机自启。";

    /// <summary>窗口列表是否包含最小化窗口。</summary>
    public bool IncludeMinimizedWindows
    {
        get => _includeMinimizedWindows;
        set
        {
            if (_includeMinimizedWindows == value)
            {
                return;
            }

            _includeMinimizedWindows = value;
            OnPropertyChanged();
        }
    }

    /// <summary>选择器失去焦点时是否自动关闭。</summary>
    public bool CloseSelectorOnFocusLost
    {
        get => _closeSelectorOnFocusLost;
        set
        {
            if (_closeSelectorOnFocusLost == value)
            {
                return;
            }

            _closeSelectorOnFocusLost = value;
            OnPropertyChanged();
        }
    }

    /// <summary>快捷键被占用时是否自动改用备用组合。</summary>
    public bool AutoFallbackHotkey
    {
        get => _autoFallbackHotkey;
        set
        {
            if (_autoFallbackHotkey == value)
            {
                return;
            }

            _autoFallbackHotkey = value;
            OnPropertyChanged();
        }
    }

    /// <summary>窗口列表是否显示缩略图。</summary>
    public bool ShowThumbnails
    {
        get => _showThumbnails;
        set
        {
            if (_showThumbnails == value)
            {
                return;
            }

            _showThumbnails = value;
            OnPropertyChanged();
        }
    }

    /// <summary>打包当前选择，交给宿主写回配置。</summary>
    public SettingsChange ToChange() => new(
        _pending.Copy(),
        StartWithWindows,
        _includeMinimizedWindows,
        _closeSelectorOnFocusLost,
        _autoFallbackHotkey,
        _showThumbnails);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
