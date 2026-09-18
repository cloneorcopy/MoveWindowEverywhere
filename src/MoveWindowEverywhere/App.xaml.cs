using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Native;
using MoveWindowEverywhere.Services;
using MoveWindowEverywhere.ViewModels;
using MoveWindowEverywhere.Views;
using WinForms = System.Windows.Forms;

namespace MoveWindowEverywhere;

/// <summary>
/// 应用入口：无主窗口，常驻系统托盘。
/// 单实例、全局快捷键、窗口枚举与移动均在此组装。
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\MoveWindowEverywhere.SingleInstance.v1";

    private Mutex? _singleInstanceMutex;
    private AppLogger _logger = null!;
    private SettingsService _settingsService = null!;
    private AppSettings _settings = AppSettings.CreateDefault();
    private MonitorService _monitorService = null!;
    private WindowEnumerator _windowEnumerator = null!;
    private WindowMover _windowMover = null!;
    private HiddenMessageWindow _messageWindow = null!;
    private HotkeyService _hotkeyService = null!;
    private TrayIconService _trayIcon = null!;
    private StartupRegistrar _startupRegistrar = null!;
    private WindowThumbnailService _thumbnailService = null!;
    private SelectorWindow? _selectorWindow;
    private bool _isShuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            NotifyExistingInstance();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        SessionEnding += OnSessionEnding;
        Exit += OnExit;

        _logger = new AppLogger();
        _logger.Info($"启动 Move Window Everywhere（PID {Environment.ProcessId}，.NET {Environment.Version}）");

        try
        {
            InitializeServices();
        }
        catch (Exception ex)
        {
            // 启动阶段失败必须让用户看见，否则程序会静默驻留而无任何入口
            _logger.Error("初始化失败，程序即将退出", ex);
            System.Windows.MessageBox.Show(
                $"Move Window Everywhere 初始化失败：{ex.Message}{Environment.NewLine}{Environment.NewLine}日志目录：{AppPaths.LogDirectory}",
                "Move Window Everywhere",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        RegisterHotkeyAtStartup();
        SynchronizeStartupRegistration();
    }

    /// <summary>
    /// 启动阶段注册快捷键。被占用且开启了自动换用时，会改用备用组合并把实际生效的组合写回配置，
    /// 这样下次启动直接使用可用的组合，不会再走一遍失败流程。
    /// </summary>
    private void RegisterHotkeyAtStartup()
    {
        HotkeyFallbackOutcome outcome = _hotkeyService.RegisterWithFallback(
            _settings.Hotkey,
            _settings.AutoFallbackHotkey);

        if (!outcome.Success)
        {
            string message = outcome.Message ?? "注册快捷键失败。";
            _logger.Error($"启动阶段注册快捷键失败：{message}");
            _trayIcon.ShowNotification("快捷键未生效", $"{message}可在托盘菜单中选择「设置」修改。", WinForms.ToolTipIcon.Warning);
            return;
        }

        if (!outcome.UsedFallback)
        {
            return;
        }

        _settings.Hotkey = outcome.Effective;
        SaveSettings();
        _trayIcon.UpdateHotkey(outcome.Effective);
        _trayIcon.ShowNotification("快捷键已自动调整", outcome.Message ?? string.Empty, WinForms.ToolTipIcon.Info);
    }

    /// <summary>
    /// 启动时把注册表里的开机自启状态对齐到配置。
    /// 配置为开启时会确保登记项存在且指向当前 exe，程序被移动过位置也能自动修复；
    /// 配置为关闭时不主动删除任何登记项，删除只由用户在设置里显式关闭时执行。
    /// </summary>
    private void SynchronizeStartupRegistration()
    {
        try
        {
            if (!_settings.StartWithWindows)
            {
                return;
            }

            StartupRegistrationResult result = _startupRegistrar.EnsureConsistent(desired: true);
            if (result.Success)
            {
                _logger.Info($"开机自启已就绪：{_startupRegistrar.ExecutablePath}");
            }
            else
            {
                _logger.Warn($"开机自启未能生效：{result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            // 开机自启属于附加功能，失败不应影响程序启动
            _logger.Warn("同步开机自启状态失败", ex);
        }
    }

    private void InitializeServices()
    {
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        _monitorService = new MonitorService(_logger);
        _windowEnumerator = new WindowEnumerator(_logger);
        _windowMover = new WindowMover(_logger);
        _thumbnailService = new WindowThumbnailService(_logger);
        _startupRegistrar = new StartupRegistrar();

        _messageWindow = new HiddenMessageWindow();
        _messageWindow.ShowSelectorRequested += (_, _) => OnTriggered("第二实例唤醒");

        _hotkeyService = new HotkeyService(_messageWindow, _settings.Hotkey, _logger);
        _hotkeyService.HotKeyPressed += (_, _) => OnTriggered("全局快捷键");

        _trayIcon = new TrayIconService(_settings.Hotkey, _logger);
        _trayIcon.OpenSelectorRequested += (_, _) => OnTriggered("托盘菜单或双击图标");
        _trayIcon.SettingsRequested += (_, _) => OpenSettings();
        _trayIcon.OpenDataFolderRequested += (_, _) => OpenDataFolder();
        _trayIcon.AboutRequested += (_, _) => ShowAbout();
        _trayIcon.ExitRequested += (_, _) => ShutdownApplication();
    }

    private static void NotifyExistingInstance()
    {
        IntPtr existingWindow = HiddenMessageWindow.FindExisting();
        if (existingWindow != IntPtr.Zero)
        {
            // 先用带超时的同步消息确认对方确实响应；失败则退回异步投递（消息进入对方队列后由消息循环处理）
            IntPtr acknowledged = Win32.SendMessageTimeout(
                existingWindow,
                Win32.MSG_APP_SHOW_SELECTOR,
                IntPtr.Zero,
                IntPtr.Zero,
                Win32.SMTO_NORMAL | Win32.SMTO_ABORTIFHUNG,
                1500,
                out IntPtr _);

            if (acknowledged != IntPtr.Zero)
            {
                return;
            }

            Debug.WriteLine($"SendMessageTimeout 未得到响应（Win32 错误码 {Marshal.GetLastWin32Error()}），改用 PostMessage 投递");

            if (Win32.PostMessage(existingWindow, Win32.MSG_APP_SHOW_SELECTOR, IntPtr.Zero, IntPtr.Zero))
            {
                return;
            }

            Debug.WriteLine($"PostMessage 失败，Win32 错误码 {Marshal.GetLastWin32Error()}");
        }

        System.Windows.MessageBox.Show(
            "Move Window Everywhere 已经在运行，请查看系统托盘中的图标。",
            "Move Window Everywhere",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>快捷键或第二实例唤醒的统一入口：先记录触发来源，再打开选择器。</summary>
    private void OnTriggered(string source)
    {
        if (_isShuttingDown)
        {
            return;
        }

        _logger?.Info($"触发来源：{source}");
        OpenSelector();
    }

    private void OpenSelector()
    {
        if (_isShuttingDown)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            if (_selectorWindow is { } existing)
            {
                if (existing.IsVisible)
                {
                    existing.Activate();
                    return;
                }

                // 残留的隐藏窗口必须先关掉。隐藏的窗口调用 Activate 不会让它重新出现，
                // 表现为按快捷键完全没有反应。
                _logger.Warn("发现未正常关闭的选择器窗口，将其关闭后重建");
                try
                {
                    existing.Close();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"关闭残留选择器失败：{ex.Message}");
                }

                _selectorWindow = null;
            }

            try
            {
                // 关键：在触发瞬间捕获鼠标所在显示器，后续点击不再重新计算
                MonitorInfo? targetMonitor = _monitorService.CaptureCursorMonitor() ?? _monitorService.GetPrimaryMonitor();
                if (targetMonitor is null)
                {
                    Notify("Move Window Everywhere", "未能捕获到目标显示器，请重试。");
                    return;
                }

                var options = new WindowFilterOptions
                {
                    IncludeMinimizedWindows = _settings.IncludeMinimizedWindows,
                };

                // 枚举耗时单独记录：若某次明显偏长，说明遇到了无响应的窗口
                var stopwatch = Stopwatch.StartNew();
                IReadOnlyList<WindowInfo> windows = new WindowFilterPolicy(options).Apply(_windowEnumerator.Enumerate());
                stopwatch.Stop();

                // 标注每个窗口所在显示器，便于在选择器里分辨哪些窗口在别的屏上
                IReadOnlyList<MonitorInfo> monitors = _monitorService.GetMonitors();
                WindowMonitorAnnotator.Annotate(windows, monitors, targetMonitor.Handle);

                _logger.Info($"已打开窗口选择器：目标显示器 {targetMonitor.ShortDescription}，候选窗口 {windows.Count} 个，显示器 {monitors.Count} 台，枚举耗时 {stopwatch.ElapsedMilliseconds} ms");

                var viewModel = new SelectorViewModel(targetMonitor);
                viewModel.SetWindows(windows);

                var selector = new SelectorWindow(
                    viewModel,
                    _windowMover,
                    targetMonitor,
                    Notify,
                    _settings.CloseSelectorOnFocusLost,
                    _settings.ShowThumbnails,
                    _thumbnailService);

                selector.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_selectorWindow, selector))
                    {
                        _selectorWindow = null;
                    }
                };

                _selectorWindow = selector;
                selector.Show();
                selector.Activate();
            }
            catch (Exception ex)
            {
                // UI 异常不能导致托盘程序退出
                _logger.Error("打开窗口选择器失败", ex);
                Notify("Move Window Everywhere", "打开窗口选择器失败，详情见日志。");
            }
        });
    }

    /// <summary>
    /// 打开设置窗口（全局快捷键 + 行为开关）。
    /// </summary>
    /// <remarks>
    /// 快捷键必须在窗口内即时注册校验，所以用户在窗口里预览过的组合会真实生效；
    /// 一旦点取消，这里负责把原来的组合注册回去，保证取消不留下副作用。
    /// 其余开关只在点了保存之后才落盘并生效。
    /// </remarks>
    private void OpenSettings()
    {
        _logger.Info("打开设置窗口");
        Dispatcher.Invoke(() =>
        {
            try
            {
                var dialog = new SettingsWindow(
                    _settings.Hotkey,
                    _settings,
                    RegisterHotkey,
                    _startupRegistrar.CanRegister);

                bool? dialogResult = dialog.ShowDialog();

                if (dialogResult == true && dialog.Result is { } change)
                {
                    ApplySettings(change);
                }
                else
                {
                    // 用户在预览期间触发过注册，取消时需要恢复到原快捷键
                    HotkeyRegistrationResult revert = _hotkeyService.TryRegister(_settings.Hotkey);
                    if (!revert.Success)
                    {
                        _logger.Error($"恢复原有快捷键失败：{revert.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("修改设置失败", ex);
                Notify("Move Window Everywhere", "修改设置失败，详情见日志。");
            }
        });
    }

    /// <summary>把设置窗口的结果落盘并生效。任何一项失败都要让配置与真实状态保持一致。</summary>
    private void ApplySettings(SettingsChange change)
    {
        _settings.Hotkey = change.Hotkey;
        _settings.IncludeMinimizedWindows = change.IncludeMinimizedWindows;
        _settings.CloseSelectorOnFocusLost = change.CloseSelectorOnFocusLost;
        _settings.AutoFallbackHotkey = change.AutoFallbackHotkey;
        _settings.ShowThumbnails = change.ShowThumbnails;

        _trayIcon.UpdateHotkey(change.Hotkey);
        _logger.Info($"设置已更新：快捷键 {change.Hotkey.DisplayText}，包含最小化窗口 {change.IncludeMinimizedWindows}，失焦关闭 {change.CloseSelectorOnFocusLost}，自动换用备用组合 {change.AutoFallbackHotkey}，显示缩略图 {change.ShowThumbnails}");

        ApplyStartupRegistration(change.StartWithWindows);
        SaveSettings();
    }

    /// <summary>
    /// 应用开机自启开关。写入失败时把配置回滚到注册表的真实状态，
    /// 避免配置显示已开启而实际并未生效。
    /// </summary>
    private void ApplyStartupRegistration(bool enabled)
    {
        if (enabled == _settings.StartWithWindows && _startupRegistrar.IsEnabled() == enabled)
        {
            return;
        }

        StartupRegistrationResult result = _startupRegistrar.Apply(enabled);
        if (result.Success)
        {
            _settings.StartWithWindows = result.Enabled;
            _logger.Info(enabled ? $"已设置开机自启：{_startupRegistrar.ExecutablePath}" : "已取消开机自启");
            return;
        }

        // 失败时以注册表的真实状态为准，不让配置说谎
        _settings.StartWithWindows = _startupRegistrar.IsEnabled();
        _logger.Error($"设置开机自启失败：{result.ErrorMessage}");
        Notify("开机自启设置失败", result.ErrorMessage ?? "无法写入启动项，详情见日志。");
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            _logger.Error("保存配置失败", ex);
        }
    }

    private HotkeyRegistrationResult RegisterHotkey(HotkeySettings candidate)
    {
        HotkeyRegistrationResult result = _hotkeyService.TryRegister(candidate);
        if (!result.Success && result.ErrorMessage is { } message)
        {
            _logger.Warn($"快捷键 {candidate.DisplayText} 注册失败：{message}");
        }

        return result;
    }

    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.AppDataDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"打开配置目录失败：{AppPaths.AppDataDirectory}", ex);
            Notify("Move Window Everywhere", "打开配置目录失败。");
        }
    }

    private void ShowAbout()
    {
        string startupState = _settings.StartWithWindows
            ? _startupRegistrar.DescribeState()
            : "未开启";

        System.Windows.MessageBox.Show(
            $"Move Window Everywhere v1.0.0{Environment.NewLine}{Environment.NewLine}"
            + $"当前快捷键：{_settings.Hotkey.DisplayText}{Environment.NewLine}"
            + $"开机自启：{startupState}{Environment.NewLine}"
            + $"配置文件：{_settingsService.SettingsPath}{Environment.NewLine}"
            + $"日志目录：{AppPaths.LogDirectory}{Environment.NewLine}{Environment.NewLine}"
            + "按下快捷键后选择窗口，即可把它移动到鼠标当时所在的显示器。",
            "关于 Move Window Everywhere",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Notify(string title, string message)
    {
        _logger.Warn($"{title}：{message}");
        try
        {
            _trayIcon.ShowNotification(title, message, WinForms.ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"托盘提示失败：{ex.Message}");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("UI 线程未处理异常", e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger?.Error("未处理异常", exception);
        }
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        ShutdownApplication();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        ReleaseResources();
    }

    private void ShutdownApplication()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _logger?.Info("正在退出 Move Window Everywhere");
        Shutdown();
    }

    private void ReleaseResources()
    {
        SaveSettings();

        try
        {
            _hotkeyService?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注销快捷键失败：{ex.Message}");
        }

        try
        {
            _trayIcon?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放托盘图标失败：{ex.Message}");
        }

        try
        {
            _messageWindow?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放隐藏窗口失败：{ex.Message}");
        }

        try
        {
            _logger?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放日志失败：{ex.Message}");
        }

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放单实例互斥量失败：{ex.Message}");
        }

        _singleInstanceMutex = null;
    }
}
