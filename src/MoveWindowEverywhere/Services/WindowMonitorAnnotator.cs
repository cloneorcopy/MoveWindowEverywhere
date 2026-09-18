using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 为窗口列表标注各自所在的显示器，供选择器展示。
/// 纯逻辑，不调用 Win32，便于单元测试。
/// </summary>
public static class WindowMonitorAnnotator
{
    /// <summary>
    /// 逐个窗口匹配所在显示器，填写显示器编号并标记它是否已经在目标显示器上。
    /// 只有一台显示器时不标注：每一行都写同一个编号只会增加噪音。
    /// 匹配不到显示器的窗口保持未标注状态，不予猜测。
    /// </summary>
    /// <param name="windows">待标注的窗口列表，会被就地修改。</param>
    /// <param name="monitors">当前系统的显示器列表，编号取自 <see cref="MonitorInfo.Index"/>。</param>
    /// <param name="targetMonitorHandle">本次操作的目标显示器句柄。</param>
    public static void Annotate(
        IReadOnlyList<WindowInfo>? windows,
        IReadOnlyList<MonitorInfo>? monitors,
        IntPtr targetMonitorHandle)
    {
        if (windows is null || monitors is null || windows.Count == 0)
        {
            return;
        }

        if (monitors.Count <= 1)
        {
            return;
        }

        foreach (WindowInfo window in windows)
        {
            if (window is null || window.MonitorHandle == IntPtr.Zero)
            {
                continue;
            }

            foreach (MonitorInfo monitor in monitors)
            {
                if (monitor.Handle != window.MonitorHandle)
                {
                    continue;
                }

                window.MonitorIndex = monitor.Index;
                window.IsOnTargetMonitor = monitor.Handle == targetMonitorHandle;
                break;
            }
        }
    }
}
