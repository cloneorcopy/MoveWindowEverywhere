using System.Windows.Threading;
using MoveWindowEverywhere.Native;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 验证「WPF 调度器消息循环能把消息分发给自建隐藏窗口」。
/// 应用真实运行时由 WPF 的 Dispatcher 泵消息，本测试用同样的方式泵消息，
/// 因此覆盖了第二实例唤醒消息与 WM_HOTKEY 在真实消息循环下的分发路径。
/// 归入窗口类集合，避免与同样注册窗口类的测试并行。
/// </summary>
[Collection(WindowClassCollection.Name)]
public sealed class DispatcherMessageLoopTests
{
    [Fact]
    public void WPF调度器应能把唤醒消息分发给隐藏窗口()
    {
        bool raised = false;

        RunOnStaDispatcher(
            action: () =>
            {
                using var window = new HiddenMessageWindow();
                window.ShowSelectorRequested += (_, _) => raised = true;

                PostAsync(window.Handle, Win32.MSG_APP_SHOW_SELECTOR, IntPtr.Zero);
                PumpUntil(() => raised, TimeSpan.FromSeconds(3));
            });

        Assert.True(raised, "WPF 调度器未能把唤醒消息分发给隐藏窗口");
    }

    [Fact]
    public void WPF调度器应能把热键消息分发给隐藏窗口()
    {
        int receivedId = 0;

        RunOnStaDispatcher(
            action: () =>
            {
                using var window = new HiddenMessageWindow();
                window.HotKeyReceived += (_, id) => receivedId = id;

                PostAsync(window.Handle, Win32.WM_HOTKEY, new IntPtr(HotkeyService.HotkeyId));
                PumpUntil(() => receivedId != 0, TimeSpan.FromSeconds(3));
            });

        Assert.Equal(HotkeyService.HotkeyId, receivedId);
    }

    [Fact]
    public void WPF调度器应能分发隐藏窗口的自定义消息且不影响其他消息()
    {
        int hotkeyEvents = 0;
        int selectorEvents = 0;

        RunOnStaDispatcher(
            action: () =>
            {
                using var window = new HiddenMessageWindow();
                window.HotKeyReceived += (_, _) => Interlocked.Increment(ref hotkeyEvents);
                window.ShowSelectorRequested += (_, _) => Interlocked.Increment(ref selectorEvents);

                PostAsync(window.Handle, Win32.WM_HOTKEY, new IntPtr(HotkeyService.HotkeyId));
                PostAsync(window.Handle, Win32.MSG_APP_SHOW_SELECTOR, IntPtr.Zero);
                PostAsync(window.Handle, Win32.WM_HOTKEY, new IntPtr(HotkeyService.HotkeyId));

                PumpUntil(() => hotkeyEvents >= 2 && selectorEvents >= 1, TimeSpan.FromSeconds(3));
            });

        Assert.Equal(2, hotkeyEvents);
        Assert.Equal(1, selectorEvents);
    }

    private static void PostAsync(IntPtr handle, uint message, IntPtr wParam)
    {
        var thread = new Thread(() =>
        {
            Thread.Sleep(50);
            Win32.PostMessage(handle, message, wParam, IntPtr.Zero);
        })
        {
            IsBackground = true,
        };

        thread.Start();
    }

    /// <summary>在带 WPF 消息循环的 STA 线程上执行动作，动作完成后关闭调度器。</summary>
    private static void RunOnStaDispatcher(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(
                new Action(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        dispatcher.InvokeShutdown();
                    }
                }));

            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        bool finished = thread.Join(TimeSpan.FromSeconds(15));
        Assert.True(finished, "STA 消息循环未在预期时间内结束");

        if (failure is not null)
        {
            throw failure;
        }
    }

    /// <summary>以嵌套消息帧的方式泵消息，直到条件满足或超时。</summary>
    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };

        DateTime deadline = DateTime.UtcNow + timeout;
        timer.Tick += (_, _) =>
        {
            if (condition() || DateTime.UtcNow > deadline)
            {
                timer.Stop();
                frame.Continue = false;
            }
        };

        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
