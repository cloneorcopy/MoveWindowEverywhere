using System.Runtime.InteropServices;
using MoveWindowEverywhere.Native;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 隐藏消息窗口测试：自建窗口类必须能创建成功、能被第二实例找到，
/// 并且唤醒消息与 WM_HOTKEY 都能被消息循环正确分发。
/// 归入窗口类集合，避免与同样注册窗口类的测试并行。
/// </summary>
[Collection(WindowClassCollection.Name)]
public sealed class HiddenMessageWindowTests
{
    [Fact]
    public void 应成功创建隐藏消息窗口()
    {
        using var window = new HiddenMessageWindow();

        Assert.NotEqual(IntPtr.Zero, window.Handle);
        Assert.True(window.IsValid);
    }

    [Fact]
    public void 第二实例应能找到已有实例的消息窗口()
    {
        // 使用唯一类名，避免与真实运行中的实例相互干扰（否则会先枚举到别人的窗口）
        string className = UniqueClassName();
        using var window = new HiddenMessageWindow(className);

        IntPtr found = HiddenMessageWindow.FindExisting(className);

        Assert.Equal(window.Handle, found);
    }

    [Fact]
    public void 默认类名应为生产用固定类名()
    {
        using var window = new HiddenMessageWindow();

        Assert.Equal(HiddenMessageWindow.WindowClassName, window.ClassName);
    }

    [Fact]
    public void 释放后不应再找到该窗口()
    {
        string className = UniqueClassName();
        IntPtr created;
        using (var window = new HiddenMessageWindow(className))
        {
            created = window.Handle;
            Assert.NotEqual(IntPtr.Zero, created);
        }

        Assert.NotEqual(created, HiddenMessageWindow.FindExisting(className));
    }

    [Fact]
    public void 收到唤醒消息时应触发显示选择器事件()
    {
        using var window = new HiddenMessageWindow();
        bool raised = false;
        window.ShowSelectorRequested += (_, _) => raised = true;

        PostFromBackgroundThread(window.Handle, Win32.MSG_APP_SHOW_SELECTOR, IntPtr.Zero);

        PumpUntil(() => raised);
        Assert.True(raised);
    }

    [Fact]
    public void 收到热键消息时应携带热键标识()
    {
        using var window = new HiddenMessageWindow();
        int receivedId = 0;
        window.HotKeyReceived += (_, id) => receivedId = id;

        PostFromBackgroundThread(window.Handle, Win32.WM_HOTKEY, new IntPtr(HotkeyService.HotkeyId));

        PumpUntil(() => receivedId != 0);
        Assert.Equal(HotkeyService.HotkeyId, receivedId);
    }

    /// <summary>生成唯一窗口类名，让测试与真实运行中的实例相互隔离。</summary>
    private static string UniqueClassName() => $"MoveWindowEverywhere.Test.{Guid.NewGuid():N}";

    private static void PostFromBackgroundThread(IntPtr handle, uint message, IntPtr wParam)
    {
        var thread = new Thread(() =>
        {
            Thread.Sleep(100);
            if (!Win32.PostMessage(handle, message, wParam, IntPtr.Zero))
            {
                throw new InvalidOperationException($"PostMessage 失败，Win32 错误码 {Marshal.GetLastWin32Error()}");
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
    }

    /// <summary>在当前线程泵消息直到条件满足或超时，用于验证消息处理链路。</summary>
    private static void PumpUntil(Func<bool> condition, int timeoutMilliseconds = 3000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline && !condition())
        {
            while (Win32.PeekMessage(out MSG message, IntPtr.Zero, 0, 0, Win32.PM_REMOVE))
            {
                Win32.TranslateMessage(ref message);
                Win32.DispatchMessage(ref message);
            }

            Thread.Sleep(10);
        }
    }
}
