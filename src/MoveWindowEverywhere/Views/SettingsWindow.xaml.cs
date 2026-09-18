using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Native;
using MoveWindowEverywhere.Services;
using MoveWindowEverywhere.ViewModels;

namespace MoveWindowEverywhere.Views;

/// <summary>
/// 设置窗口：全局快捷键与行为开关。
/// </summary>
/// <remarks>
/// 快捷键在窗口内即时校验（真正调用 RegisterHotKey），被占用时当场给出提示并保留原组合；
/// 用户在预览期间注册过别的组合又点了取消时，由宿主负责把原来的组合注册回去。
/// 其余开关只收集选择，等宿主收到 DialogResult 之后再落盘并生效，
/// 这样取消操作不会留下任何副作用。
/// </remarks>
public sealed partial class SettingsWindow : Window
{
    private readonly Func<HotkeySettings, HotkeyRegistrationResult> _validateHotkey;
    private readonly HotkeySettings _originalHotkey;
    private bool _capturing;

    public SettingsWindow(
        HotkeySettings currentHotkey,
        AppSettings current,
        Func<HotkeySettings, HotkeyRegistrationResult> validateHotkey,
        bool canRegisterStartup = true)
    {
        ArgumentNullException.ThrowIfNull(current);

        _originalHotkey = currentHotkey.Copy();
        _validateHotkey = validateHotkey;

        InitializeComponent();
        ViewModel = new SettingsViewModel(
            currentHotkey.Copy(),
            current.StartWithWindows,
            current.IncludeMinimizedWindows,
            current.CloseSelectorOnFocusLost,
            current.AutoFallbackHotkey,
            current.ShowThumbnails,
            canRegisterStartup);
        DataContext = ViewModel;

        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => PreviewKeyDown -= OnPreviewKeyDown;
    }

    public SettingsViewModel ViewModel { get; }

    /// <summary>保存后的完整设置；取消时为 null。</summary>
    public SettingsChange? Result { get; private set; }

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        _capturing = true;
        CaptureHintTextBlock.Visibility = Visibility.Visible;
        ViewModel.ErrorMessage = string.Empty;
        Focus();
    }

    private void OnResetHotkeyClick(object sender, RoutedEventArgs e)
    {
        _capturing = false;
        CaptureHintTextBlock.Visibility = Visibility.Collapsed;
        ViewModel.Pending = HotkeySettings.CreateDefault();
        ViewModel.ErrorMessage = string.Empty;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        HotkeySettings candidate = ViewModel.Pending.Copy();

        if (!candidate.IsValid)
        {
            ViewModel.ErrorMessage = "快捷键无效：至少需要包含一个修饰键（Ctrl、Alt、Shift 或 Win）和一个按键。";
            return;
        }

        // 快捷键没改动时不要再注册一次：既无必要，也会因为组合此刻恰好被占用
        // 而让一次「只想改开关」的保存失败
        bool hotkeyChanged = candidate.Modifiers != _originalHotkey.Modifiers
            || candidate.VirtualKey != _originalHotkey.VirtualKey;

        if (hotkeyChanged)
        {
            HotkeyRegistrationResult result = _validateHotkey(candidate);
            if (!result.Success)
            {
                ViewModel.ErrorMessage = result.ErrorMessage ?? "注册快捷键失败，请换一个组合。";
                return;
            }
        }

        Result = ViewModel.ToChange();
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing)
        {
            return;
        }

        if (IsModifierKey(e.Key))
        {
            return;
        }

        e.Handled = true;
        _capturing = false;
        CaptureHintTextBlock.Visibility = Visibility.Collapsed;

        uint modifiers = 0;
        ModifierKeys currentModifiers = Keyboard.Modifiers;
        if ((currentModifiers & ModifierKeys.Control) != 0)
        {
            modifiers |= HotkeySettings.ModControl;
        }

        if ((currentModifiers & ModifierKeys.Alt) != 0)
        {
            modifiers |= HotkeySettings.ModAlt;
        }

        if ((currentModifiers & ModifierKeys.Shift) != 0)
        {
            modifiers |= HotkeySettings.ModShift;
        }

        // WPF 的 Keyboard.Modifiers 不包含 Windows 键，需要单独查询按键状态
        if (IsKeyDown(Win32.VK_LWIN) || IsKeyDown(Win32.VK_RWIN))
        {
            modifiers |= HotkeySettings.ModWin;
        }

        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(e.Key);
        ViewModel.Pending = new HotkeySettings
        {
            Modifiers = modifiers | HotkeySettings.ModNoRepeat,
            VirtualKey = virtualKey,
        };

        if (!ViewModel.Pending.IsValid)
        {
            ViewModel.ErrorMessage = "快捷键无效：至少需要包含一个修饰键（Ctrl、Alt、Shift 或 Win）和一个按键。";
        }
    }

    private static bool IsKeyDown(int virtualKey) => (Win32.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl
        or Key.RightCtrl
        or Key.LeftAlt
        or Key.RightAlt
        or Key.LeftShift
        or Key.RightShift
        or Key.LWin
        or Key.RWin
        or Key.System
        or Key.None
        or Key.Capital
        or Key.CapsLock;
}
