using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Native;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 窗口移动错误路径与真实窗口移动测试。真实窗口使用测试进程自己创建的隐藏 WinForms 窗体，
/// 不影响用户正在使用的任何窗口。
/// </summary>
public sealed class WindowMoverTests
{
    private static WindowMover CreateMover() => new(new AppLogger());

    private static MonitorInfo CreateMonitor() => new()
    {
        Handle = new IntPtr(0x00010001),
        Index = 1,
        DeviceName = @"\\.\DISPLAY1",
        FriendlyName = "测试显示器",
        IsPrimary = true,
        MonitorRect = new System.Windows.Rect(0, 0, 1920, 1080),
        WorkRect = new System.Windows.Rect(0, 0, 1920, 1040),
        DpiX = 96,
        DpiY = 96,
    };

    [Fact]
    public void 空句柄应返回明确失败()
    {
        WindowMoveResult result = CreateMover().MoveToMonitor(IntPtr.Zero, CreateMonitor());

        Assert.False(result.Success);
        Assert.Contains("句柄无效", result.Message);
    }

    [Fact]
    public void 已失效句柄应提示窗口已关闭()
    {
        var invalidHandle = new IntPtr(0x00ABCDEF);

        WindowMoveResult result = CreateMover().MoveToMonitor(invalidHandle, CreateMonitor());

        Assert.False(result.Success);
        Assert.False(result.RequiresElevation);
        Assert.Contains("已关闭或句柄失效", result.Message);
    }

    [Fact]
    public void 未捕获到目标显示器时应提示重试()
    {
        RunOnStaThread(() =>
        {
            using var form = new Form
            {
                Text = "MoveWindowEverywhere 空目标测试窗口",
                StartPosition = FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(0, 0, 500, 400),
                ShowInTaskbar = false,
            };
            _ = form.Handle;

            WindowMoveResult result = CreateMover().MoveToMonitor(form.Handle, null);

            Assert.False(result.Success);
            Assert.Contains("目标显示器", result.Message);
        });
    }

    [Fact]
    public void 真实窗口应支持多级恢复并还原每一步的位置与尺寸()
    {
        var monitorService = new MonitorService(new AppLogger());
        MonitorInfo? monitor = monitorService.GetPrimaryMonitor();
        Assert.NotNull(monitor);

        RunOnStaThread(() =>
        {
            using var form = new Form
            {
                Text = "MoveWindowEverywhere 多级恢复测试窗口",
                StartPosition = FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(37, 53, 640, 480),
                ShowInTaskbar = false,
                WindowState = FormWindowState.Normal,
            };
            _ = form.Handle;

            var mover = CreateMover();
            WINDOWPLACEMENT firstOrigin = GetPlacement(form.Handle);

            WindowMoveResult firstMove = mover.MoveToMonitor(form.Handle, monitor);
            Assert.True(firstMove.Success, firstMove.Message);
            Assert.Equal(1, mover.GetRestoreCount(form.Handle));
            Assert.Contains(form.Handle, mover.GetRestorableHandles());

            // 用户在第一次移动之后主动调整位置和尺寸；第二次 Alt+Z 应把这个新状态作为下一层历史。
            form.Bounds = new System.Drawing.Rectangle(211, 187, 520, 390);
            WINDOWPLACEMENT secondOrigin = GetPlacement(form.Handle);

            WindowMoveResult secondMove = mover.MoveToMonitor(form.Handle, monitor);
            Assert.True(secondMove.Success, secondMove.Message);
            Assert.Equal(2, mover.GetRestoreCount(form.Handle));

            // 再手工改大，恢复必须覆盖这些后续调整，回到第二次移动前的完整位置与尺寸。
            form.Bounds = new System.Drawing.Rectangle(15, 25, 900, 700);

            WindowMoveResult firstRestore = mover.RestorePrevious(form.Handle);
            Assert.True(firstRestore.Success, firstRestore.Message);
            AssertSamePlacement(secondOrigin, GetPlacement(form.Handle));
            Assert.Equal(1, mover.GetRestoreCount(form.Handle));

            WindowMoveResult secondRestore = mover.RestorePrevious(form.Handle);
            Assert.True(secondRestore.Success, secondRestore.Message);
            AssertSamePlacement(firstOrigin, GetPlacement(form.Handle));
            Assert.Equal(0, mover.GetRestoreCount(form.Handle));
            Assert.DoesNotContain(form.Handle, mover.GetRestorableHandles());
        });
    }

    [Fact]
    public void 最小化窗口移动后恢复应还原最小化状态与原始恢复矩形()
    {
        var monitorService = new MonitorService(new AppLogger());
        MonitorInfo? monitor = monitorService.GetPrimaryMonitor();
        Assert.NotNull(monitor);

        RunOnStaThread(() =>
        {
            using var form = new Form
            {
                Text = "MoveWindowEverywhere 最小化测试窗口",
                StartPosition = FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(123, 91, 500, 400),
                ShowInTaskbar = false,
            };
            _ = form.Handle;
            // 先显示测试窗口再最小化，确保原生 HWND 的 WINDOWPLACEMENT 确实记录为最小化状态。
            form.Show();
            form.WindowState = FormWindowState.Minimized;
            WINDOWPLACEMENT original = GetPlacement(form.Handle);

            var mover = CreateMover();
            WindowMoveResult move = mover.MoveToMonitor(form.Handle, monitor);

            Assert.True(move.Success, move.Message);
            Assert.Equal(FormWindowState.Normal, form.WindowState);

            WindowMoveResult restore = mover.RestorePrevious(form.Handle);
            Assert.True(restore.Success, restore.Message);
            Assert.Equal(FormWindowState.Minimized, form.WindowState);
            AssertSamePlacement(original, GetPlacement(form.Handle));
        });
    }

    [Fact]
    public void 最大化窗口跨屏移动后应恢复原显示器和原始普通尺寸()
    {
        var monitorService = new MonitorService(new AppLogger());
        IReadOnlyList<MonitorInfo> monitors = monitorService.GetMonitors();
        MonitorInfo? primary = monitorService.GetPrimaryMonitor();
        Assert.NotNull(primary);

        RunOnStaThread(() =>
        {
            using var form = new Form
            {
                Text = "MoveWindowEverywhere 最大化恢复测试窗口",
                StartPosition = FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(
                    (int)primary.WorkRect.Left + 65, (int)primary.WorkRect.Top + 75, 620, 430),
                ShowInTaskbar = false,
            };
            form.Show();
            form.WindowState = FormWindowState.Maximized;
            Assert.True(Win32.IsZoomed(form.Handle));

            WINDOWPLACEMENT original = GetPlacement(form.Handle);
            IntPtr originalMonitor = Win32.MonitorFromWindow(
                form.Handle, Win32.MONITOR_DEFAULTTONEAREST);
            MonitorInfo destination = monitors.FirstOrDefault(m => m.Handle != originalMonitor) ?? primary;
            var mover = CreateMover();

            WindowMoveResult move = mover.MoveToMonitor(form.Handle, destination);
            Assert.True(move.Success, move.Message);

            WindowMoveResult restore = mover.RestorePrevious(form.Handle);
            Assert.True(restore.Success, restore.Message);
            Assert.True(Win32.IsZoomed(form.Handle));
            Assert.Equal(originalMonitor, Win32.MonitorFromWindow(
                form.Handle, Win32.MONITOR_DEFAULTTONEAREST));
            AssertSamePlacement(original, GetPlacement(form.Handle));
        });
    }

    [Fact]
    public void 无边框全屏矩形应识别为覆盖完整显示器()
    {
        var monitor = new RECT { Left = -1920, Top = 0, Right = 0, Bottom = 1080 };
        var full = new RECT { Left = -1920, Top = 0, Right = 0, Bottom = 1080 };
        var partial = new RECT { Left = -1920, Top = 0, Right = 0, Bottom = 1040 };

        Assert.True(WindowMover.CoversMonitor(full, monitor));
        Assert.False(WindowMover.CoversMonitor(partial, monitor));
    }

    [Fact]
    public void 无边框全屏窗口移动和恢复时应保留完整显示器尺寸()
    {
        var monitorService = new MonitorService(new AppLogger());
        MonitorInfo? monitor = monitorService.GetPrimaryMonitor();
        Assert.NotNull(monitor);

        RunOnStaThread(() =>
        {
            using var form = new Form
            {
                Text = "MoveWindowEverywhere 无边框全屏测试窗口",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(
                    (int)monitor.MonitorRect.Left, (int)monitor.MonitorRect.Top,
                    (int)monitor.MonitorRect.Width, (int)monitor.MonitorRect.Height),
                ShowInTaskbar = false,
            };
            _ = form.Handle;
            // Form initialization may clamp a nominally fullscreen window to the
            // taskbar-adjusted work area. Set the HWND rectangle explicitly.
            Assert.True(Win32.SetWindowPos(form.Handle, IntPtr.Zero,
                (int)monitor.MonitorRect.Left, (int)monitor.MonitorRect.Top,
                (int)monitor.MonitorRect.Width, (int)monitor.MonitorRect.Height,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED));
            Assert.True(Win32.GetWindowRect(form.Handle, out RECT originalBounds));
            Assert.True(WindowMover.CoversMonitor(originalBounds, new RECT
            {
                Left = (int)monitor.MonitorRect.Left,
                Top = (int)monitor.MonitorRect.Top,
                Right = (int)monitor.MonitorRect.Right,
                Bottom = (int)monitor.MonitorRect.Bottom,
            }));

            var mover = CreateMover();
            WindowMoveResult move = mover.MoveToMonitor(form.Handle, monitor);
            Assert.True(move.Success, move.Message);
            Assert.True(Win32.GetWindowRect(form.Handle, out RECT movedBounds));
            Assert.Equal((int)monitor.MonitorRect.Height, movedBounds.Height);

            form.Bounds = new System.Drawing.Rectangle(
                (int)monitor.WorkRect.Left + 60, (int)monitor.WorkRect.Top + 80, 700, 500);
            WindowMoveResult restore = mover.RestorePrevious(form.Handle);
            Assert.True(restore.Success, restore.Message);
            Assert.True(Win32.GetWindowRect(form.Handle, out RECT restoredBounds));
            Assert.Equal(originalBounds.Left, restoredBounds.Left);
            Assert.Equal(originalBounds.Top, restoredBounds.Top);
            Assert.Equal(originalBounds.Width, restoredBounds.Width);
            Assert.Equal(originalBounds.Height, restoredBounds.Height);
        });
    }

    private static WINDOWPLACEMENT GetPlacement(IntPtr handle)
    {
        var placement = new WINDOWPLACEMENT
        {
            Length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>(),
        };

        Assert.True(Win32.GetWindowPlacement(handle, ref placement));
        return placement;
    }

    private static void AssertSamePlacement(WINDOWPLACEMENT expected, WINDOWPLACEMENT actual)
    {
        Assert.Equal(expected.ShowCmd, actual.ShowCmd);
        Assert.Equal(expected.RcNormalPosition.Left, actual.RcNormalPosition.Left);
        Assert.Equal(expected.RcNormalPosition.Top, actual.RcNormalPosition.Top);
        Assert.Equal(expected.RcNormalPosition.Right, actual.RcNormalPosition.Right);
        Assert.Equal(expected.RcNormalPosition.Bottom, actual.RcNormalPosition.Bottom);
    }

    /// <summary>WinForms 窗体必须在 STA 线程上创建。</summary>
    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw failure;
        }
    }
}
