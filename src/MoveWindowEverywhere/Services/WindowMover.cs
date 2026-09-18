using System.Runtime.InteropServices;
using System.Windows;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Native;

namespace MoveWindowEverywhere.Services;

/// <summary>窗口移动结果。</summary>
public sealed record WindowMoveResult(bool Success, string Message, bool RequiresElevation)
{
    public static WindowMoveResult Ok(string message) => new(true, message, false);

    public static WindowMoveResult Fail(string message, bool requiresElevation = false) => new(false, message, requiresElevation);
}

/// <summary>
/// 把窗口移动到目标显示器：恢复最小化、解除最大化、居中、限制尺寸、恢复最大化、激活。
/// 目标显示器必须是快捷键触发瞬间捕获的显示器，而不是点击选择器时鼠标所在的显示器。
/// </summary>
public sealed class WindowMover
{
    private readonly AppLogger _logger;

    public WindowMover(AppLogger logger)
    {
        _logger = logger;
    }

    public WindowMoveResult MoveToMonitor(IntPtr handle, MonitorInfo? targetMonitor)
    {
        if (handle == IntPtr.Zero)
        {
            return WindowMoveResult.Fail("目标窗口句柄无效。");
        }

        if (!Win32.IsWindow(handle))
        {
            return WindowMoveResult.Fail("目标窗口已关闭或句柄失效，请重新选择。");
        }

        if (targetMonitor is null)
        {
            return WindowMoveResult.Fail("未捕获到目标显示器，请重新按一次快捷键。");
        }

        bool wasMinimized = Win32.IsIconic(handle);
        bool wasMaximized = Win32.IsZoomed(handle);

        if (wasMinimized || wasMaximized)
        {
            Win32.ShowWindow(handle, Win32.SW_RESTORE);
            WaitUntilRestored(handle);
        }

        if (!Win32.TryGetVisibleWindowBounds(handle, out RECT current))
        {
            return WindowMoveResult.Fail("无法读取窗口位置，窗口可能已关闭。");
        }

        Int32Rect desired = WindowPlacementCalculator.ComputeCenteredRect(
            targetMonitor.WorkRect,
            current.Width,
            current.Height);

        if (!SetPosition(handle, desired))
        {
            return WindowMoveResult.Fail(BuildSetWindowPosError(handle));
        }

        // 某些程序会拒绝或修正我们设置的位置，移动后再校验一次并夹紧到工作区
        if (Win32.TryGetVisibleWindowBounds(handle, out RECT after))
        {
            Int32Rect clamped = WindowPlacementCalculator.ClampToWorkArea(
                new Int32Rect(after.Left, after.Top, after.Width, after.Height),
                targetMonitor.WorkRect);

            if (clamped.X != after.Left || clamped.Y != after.Top || clamped.Width != after.Width || clamped.Height != after.Height)
            {
                _logger.Warn($"窗口未按预期位置落位，执行一次夹紧：({after.Left},{after.Top},{after.Width}×{after.Height}) -> ({clamped.X},{clamped.Y},{clamped.Width}×{clamped.Height})");
                if (!SetPosition(handle, clamped))
                {
                    return WindowMoveResult.Fail(BuildSetWindowPosError(handle));
                }
            }
        }

        if (wasMaximized)
        {
            Win32.ShowWindow(handle, Win32.SW_MAXIMIZE);
        }

        // 最小化的窗口按默认策略恢复为普通可见窗口（README 中已说明）
        bool activated = Activate(handle);
        if (!activated)
        {
            _logger.Warn($"窗口 0x{handle.ToInt64():X} 移动成功，但未能设置为前台窗口");
        }

        return WindowMoveResult.Ok($"已移动到 {targetMonitor.ShortDescription}");
    }

    private bool SetPosition(IntPtr handle, Int32Rect rect)
    {
        bool ok = Win32.SetWindowPos(
            handle,
            IntPtr.Zero,
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);

        if (!ok)
        {
            int error = Marshal.GetLastWin32Error();
            _logger.Error($"SetWindowPos 失败，句柄 0x{handle.ToInt64():X}，Win32 错误码 {error}");
            return false;
        }

        return true;
    }

    private string BuildSetWindowPosError(IntPtr handle)
    {
        int error = Marshal.GetLastWin32Error();
        if (error == Win32.ERROR_ACCESS_DENIED || error == Win32.ERROR_INVALID_WINDOW_HANDLE)
        {
            return "目标窗口权限高于本程序或句柄已失效，请尝试以管理员身份运行本工具。";
        }

        return $"移动窗口失败（Win32 错误码 {error}）。";
    }

    private static void WaitUntilRestored(IntPtr handle)
    {
        for (int i = 0; i < 20; i++)
        {
            if (!Win32.IsIconic(handle) && !Win32.IsZoomed(handle))
            {
                return;
            }

            Thread.Sleep(10);
        }

        // 少数程序的恢复是异步的，超时后继续尝试移动，后续校验会再夹紧一次
    }

    /// <summary>把窗口设置为前台窗口。失败时尝试线程附加方式再次激活。</summary>
    public bool Activate(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !Win32.IsWindow(handle))
        {
            return false;
        }

        // 允许其他进程抢占前台，降低 SetForegroundWindow 被系统拒绝的概率
        Win32.AllowSetForegroundWindow(uint.MaxValue);

        if (Win32.SetForegroundWindow(handle))
        {
            return true;
        }

        _logger.Warn($"SetForegroundWindow 失败，句柄 0x{handle.ToInt64():X}，Win32 错误码 {Marshal.GetLastWin32Error()}，尝试线程附加方式激活");

        uint targetThread = Win32.GetWindowThreadProcessId(handle, out _);
        uint currentThread = Win32.GetCurrentThreadId();
        bool attached = false;
        try
        {
            if (targetThread != 0 && targetThread != currentThread)
            {
                attached = Win32.AttachThreadInput(currentThread, targetThread, true);
            }

            Win32.BringWindowToTop(handle);
            return Win32.SetForegroundWindow(handle);
        }
        finally
        {
            if (attached)
            {
                Win32.AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }
}
