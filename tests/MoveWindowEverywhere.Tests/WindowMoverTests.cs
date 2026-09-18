using System.Threading;
using System.Windows.Forms;
using MoveWindowEverywhere.Models;
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
        // 该句柄值不属于任何现存窗口，IsWindow 必然返回 false
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
    public void 真实窗口应被移动到目标显示器工作区中央并保留尺寸()
    {
        var monitorService = new MonitorService(new AppLogger());
        MonitorInfo? monitor = monitorService.GetPrimaryMonitor();
        Assert.NotNull(monitor);

        RunOnStaThread(() =>
        {
            using var form = new Form
            {
                Text = "MoveWindowEverywhere 单元测试窗口",
                StartPosition = FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(0, 0, 640, 480),
                ShowInTaskbar = false,
                WindowState = FormWindowState.Normal,
            };

            // 强制创建窗口句柄，但不显示窗口
            _ = form.Handle;

            var mover = CreateMover();
            WindowMoveResult result = mover.MoveToMonitor(form.Handle, monitor);

            Assert.True(result.Success, result.Message);
            Assert.Equal(640, form.Width);
            Assert.Equal(480, form.Height);

            double workWidth = monitor.WorkRect.Width;
            double workHeight = monitor.WorkRect.Height;
            int expectedLeft = (int)Math.Round(monitor.WorkRect.Left + (workWidth - form.Width) / 2.0);
            int expectedTop = (int)Math.Round(monitor.WorkRect.Top + (workHeight - form.Height) / 2.0);

            Assert.InRange(form.Left, expectedLeft - 40, expectedLeft + 40);
            Assert.InRange(form.Top, expectedTop - 40, expectedTop + 40);
        });
    }

    [Fact]
    public void 最小化窗口应恢复为可见普通窗口()
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
                Bounds = new System.Drawing.Rectangle(0, 0, 500, 400),
                ShowInTaskbar = false,
            };
            _ = form.Handle;
            form.WindowState = FormWindowState.Minimized;

            WindowMoveResult result = CreateMover().MoveToMonitor(form.Handle, monitor);

            Assert.True(result.Success, result.Message);
            Assert.Equal(FormWindowState.Normal, form.WindowState);
            Assert.Equal(500, form.Width);
            Assert.Equal(400, form.Height);
        });
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
