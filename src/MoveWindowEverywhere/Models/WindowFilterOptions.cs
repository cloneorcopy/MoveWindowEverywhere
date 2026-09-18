namespace MoveWindowEverywhere.Models;

/// <summary>
/// 窗口过滤选项。过滤规则集中在此，避免散落在 UI 代码中。
/// </summary>
public sealed class WindowFilterOptions
{
    /// <summary>排除本进程自身的窗口（含选择器、隐藏窗口）。</summary>
    public bool ExcludeOwnProcess { get; init; } = true;

    /// <summary>排除具有 owner 的从属窗口，避免弹窗和内部窗口污染列表。</summary>
    public bool ExcludeOwnedWindows { get; init; } = true;

    /// <summary>排除 WS_EX_TOOLWINDOW 工具窗口。</summary>
    public bool ExcludeToolWindows { get; init; } = true;

    /// <summary>排除 WS_EX_NOACTIVATE 窗口（通知类浮动窗口）。</summary>
    public bool ExcludeNoActivateWindows { get; init; } = true;

    /// <summary>排除被 DWM cloaking 的窗口（虚拟桌面未激活桌面、UWP 挂起等）。</summary>
    public bool ExcludeCloakedWindows { get; init; } = true;

    /// <summary>是否保留最小化窗口。默认保留，便于把最小化的应用恢复到目标屏幕。</summary>
    public bool IncludeMinimizedWindows { get; init; } = true;

    public static WindowFilterOptions Default => new();
}
