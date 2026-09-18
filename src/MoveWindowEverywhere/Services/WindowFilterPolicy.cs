using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>过滤判定结果：是否保留以及被排除的原因（用于日志与后续排错）。</summary>
public sealed record WindowFilterDecision(bool IsIncluded, string Reason)
{
    public static WindowFilterDecision Include() => new(true, string.Empty);

    public static WindowFilterDecision Exclude(string reason) => new(false, reason);
}

/// <summary>
/// 窗口过滤策略。全部过滤规则集中在此类中，UI 代码不参与判断。
/// 判断对象为 <see cref="WindowInfo"/> 纯数据，因此可被单元测试直接覆盖。
/// </summary>
public sealed class WindowFilterPolicy
{
    /// <summary>系统级窗口类名：任务栏、桌面、开始菜单、通知溢出区等。</summary>
    private static readonly HashSet<string> SystemWindowClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Shell_InputSwitchTopLevelWindow",
        "Shell_CharmWindow",
        "Progman",
        "WorkerW",
        "DV2ControlHost",
        "NotifyIconOverflowWindow",
        "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow",
        "ApplicationFrameInputSink",
        "ForegroundStaging",
        "ApplicationManager_DesktopShellWindow",
        "Microsoft.Windows.Shell.RunDialog",
        "MozillaWindowClassMessageWindow",
        "EdgeUiInputTopLevelClassWindow",
    };

    private readonly WindowFilterOptions _options;

    public WindowFilterPolicy(WindowFilterOptions? options = null)
    {
        _options = options ?? WindowFilterOptions.Default;
    }

    public WindowFilterOptions Options => _options;

    public bool IsIncluded(WindowInfo window) => Evaluate(window).IsIncluded;

    public IReadOnlyList<WindowInfo> Apply(IEnumerable<WindowInfo> windows)
    {
        var kept = new List<WindowInfo>();
        foreach (WindowInfo window in windows)
        {
            if (IsIncluded(window))
            {
                kept.Add(window);
            }
        }

        return kept;
    }

    public WindowFilterDecision Evaluate(WindowInfo window)
    {
        if (window is null)
        {
            return WindowFilterDecision.Exclude("窗口对象为空");
        }

        if (window.Handle == IntPtr.Zero)
        {
            return WindowFilterDecision.Exclude("句柄无效");
        }

        if (!window.IsVisible)
        {
            return WindowFilterDecision.Exclude("窗口不可见");
        }

        if (string.IsNullOrWhiteSpace(window.Title))
        {
            return WindowFilterDecision.Exclude("窗口没有有效标题");
        }

        if (_options.ExcludeOwnProcess && window.IsOwnProcess)
        {
            return WindowFilterDecision.Exclude("属于本程序自身");
        }

        if (_options.ExcludeCloakedWindows && window.IsCloaked)
        {
            return WindowFilterDecision.Exclude("被系统 cloaking 隐藏");
        }

        if (_options.ExcludeToolWindows && window.IsToolWindow)
        {
            return WindowFilterDecision.Exclude("WS_EX_TOOLWINDOW 工具窗口");
        }

        if (_options.ExcludeNoActivateWindows && window.IsNoActivateWindow)
        {
            return WindowFilterDecision.Exclude("WS_EX_NOACTIVATE 不可激活窗口");
        }

        if (_options.ExcludeOwnedWindows && window.HasOwner)
        {
            return WindowFilterDecision.Exclude("从属于其他窗口");
        }

        if (!_options.IncludeMinimizedWindows && window.State == WindowStateKind.Minimized)
        {
            return WindowFilterDecision.Exclude("最小化窗口已被配置排除");
        }

        if (SystemWindowClassNames.Contains(window.ClassName))
        {
            return WindowFilterDecision.Exclude($"系统窗口（{window.ClassName}）");
        }

        return WindowFilterDecision.Include();
    }
}
