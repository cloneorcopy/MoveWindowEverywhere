using System.Drawing;
using System.Windows.Forms;
using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 系统托盘图标与右键菜单。使用 System.Windows.Forms.NotifyIcon，不引入第三方依赖。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _icon;
    private readonly AppLogger _logger;
    private readonly ToolStripMenuItem _openSelectorItem;
    private bool _disposed;

    public event EventHandler? OpenSelectorRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? OpenDataFolderRequested;

    public event EventHandler? AboutRequested;

    public event EventHandler? ExitRequested;

    public TrayIconService(HotkeySettings hotkey, AppLogger logger)
    {
        _logger = logger;
        _icon = IconFactory.CreateAppIcon();

        _openSelectorItem = new ToolStripMenuItem($"打开窗口选择器（{hotkey.DisplayText}）", null, (_, _) => OpenSelectorRequested?.Invoke(this, EventArgs.Empty));
        var settingsItem = new ToolStripMenuItem("设置…", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        var folderItem = new ToolStripMenuItem("打开配置目录", null, (_, _) => OpenDataFolderRequested?.Invoke(this, EventArgs.Empty));
        var aboutItem = new ToolStripMenuItem("关于", null, (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty));
        var exitItem = new ToolStripMenuItem("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_openSelectorItem);
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(folderItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(aboutItem);
        _menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = $"Move Window Everywhere — {hotkey.DisplayText}",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSelectorRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateHotkey(HotkeySettings hotkey)
    {
        _openSelectorItem.Text = $"打开窗口选择器（{hotkey.DisplayText}）";
        _notifyIcon.Text = $"Move Window Everywhere — {hotkey.DisplayText}";
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            _notifyIcon.Visible = true;
            _notifyIcon.ShowBalloonTip(5000, title, message, icon);
        }
        catch (Exception ex)
        {
            _logger.Warn($"托盘气泡提示失败：{ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}
