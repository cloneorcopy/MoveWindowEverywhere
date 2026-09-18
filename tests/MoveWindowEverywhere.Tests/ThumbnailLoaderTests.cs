using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

/// <summary>
/// 后台缩略图加载测试。
/// 这里注入假的捕获函数，完全不触碰 Win32，因此可以在任何环境稳定运行；
/// 要验证的是「哪些窗口会被截」「失败了会不会拖垮整轮」「取消后会不会继续截」这三件事。
/// </summary>
public sealed class ThumbnailLoaderTests
{
    private static WindowInfo CreateWindow(int handleValue, WindowStateKind state = WindowStateKind.Normal) => new()
    {
        Handle = new IntPtr(handleValue),
        Title = $"窗口 {handleValue}",
        ProcessName = "test",
        IsVisible = true,
        State = state,
    };

    private static BitmapSource CreateBitmap()
    {
        BitmapSource bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return condition();
    }

    [Fact]
    public void 初始化参数不能为空()
    {
        using var loader = new ThumbnailLoader(_ => null);

        Assert.Throws<ArgumentNullException>(() => loader.Start(new List<WindowInfo>(), null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => loader.Start(new List<WindowInfo>(), (_, _) => { }, null!));
    }

    [Fact]
    public void 没有候选窗口时不应启动任何工作()
    {
        int captures = 0;
        using var loader = new ThumbnailLoader(_ =>
        {
            Interlocked.Increment(ref captures);
            return CreateBitmap();
        });

        loader.Start(new List<WindowInfo>(), (_, _) => { }, action => action());

        Assert.False(loader.IsRunning);
        Assert.Equal(0, captures);
    }

    [Fact]
    public async Task 只截取合格窗口并逐张回填()
    {
        var windows = new List<WindowInfo>
        {
            CreateWindow(1),
            CreateWindow(2, WindowStateKind.Minimized),
            CreateWindow(3),
        };

        var captured = new List<int>();
        var applied = new List<int>();
        using var loader = new ThumbnailLoader(handle =>
        {
            lock (captured)
            {
                captured.Add(handle.ToInt32());
            }

            return CreateBitmap();
        });

        await loader.RunCoreAsync(
            ThumbnailCapturePolicy.SelectCandidates(windows),
            (window, _) => applied.Add(window.Handle.ToInt32()),
            action => action());

        Assert.Equal(new[] { 1, 3 }, captured);
        Assert.Equal(new[] { 1, 3 }, applied);
    }

    [Fact]
    public async Task 捕获失败时不应回填()
    {
        var windows = new List<WindowInfo> { CreateWindow(1), CreateWindow(2) };

        var applied = new List<int>();
        using var loader = new ThumbnailLoader(handle => handle.ToInt32() == 1 ? CreateBitmap() : null);

        await loader.RunCoreAsync(
            ThumbnailCapturePolicy.SelectCandidates(windows),
            (window, _) => applied.Add(window.Handle.ToInt32()),
            action => action());

        Assert.Equal(new[] { 1 }, applied);
    }

    [Fact]
    public async Task 单个窗口捕获抛异常不应中断整轮()
    {
        var windows = new List<WindowInfo> { CreateWindow(1), CreateWindow(2), CreateWindow(3) };

        var applied = new List<int>();
        using var loader = new ThumbnailLoader(handle =>
        {
            if (handle.ToInt32() == 2)
            {
                throw new InvalidOperationException("模拟捕获失败");
            }

            return CreateBitmap();
        });

        await loader.RunCoreAsync(
            ThumbnailCapturePolicy.SelectCandidates(windows),
            (window, _) => applied.Add(window.Handle.ToInt32()),
            action => action());

        Assert.Equal(new[] { 1, 3 }, applied);
    }

    [Fact]
    public async Task 回填过程抛异常不应中断整轮()
    {
        var windows = new List<WindowInfo> { CreateWindow(1), CreateWindow(2), CreateWindow(3) };

        var attempted = new List<int>();
        using var loader = new ThumbnailLoader(_ => CreateBitmap());

        await loader.RunCoreAsync(
            ThumbnailCapturePolicy.SelectCandidates(windows),
            (window, _) =>
            {
                attempted.Add(window.Handle.ToInt32());
                throw new InvalidOperationException("模拟界面已关闭");
            },
            action => action());

        Assert.Equal(new[] { 1, 2, 3 }, attempted);
    }

    [Fact]
    public void 运行期间重复启动应被忽略()
    {
        var windows = new List<WindowInfo> { CreateWindow(1), CreateWindow(2), CreateWindow(3) };

        int captures = 0;
        using var gate = new ManualResetEventSlim(false);
        using var loader = new ThumbnailLoader(_ =>
        {
            Interlocked.Increment(ref captures);
            gate.Wait(5000);
            return CreateBitmap();
        });

        Action<WindowInfo, BitmapSource> apply = (_, _) => { };
        Action<Action> dispatch = action => action();

        loader.Start(windows, apply, dispatch);
        Assert.True(WaitUntil(() => Volatile.Read(ref captures) >= 1), "第一个窗口的截取未在预期时间内开始");

        // 此时仍在运行，第二次启动必须被忽略，否则同一批窗口会被截两遍
        loader.Start(windows, apply, dispatch);

        gate.Set();
        Assert.True(WaitUntil(() => !loader.IsRunning), "截取未在预期时间内结束");
        Assert.Equal(3, captures);
    }

    [Fact]
    public void 释放后不应再启动新的截取()
    {
        int captures = 0;
        var loader = new ThumbnailLoader(_ =>
        {
            Interlocked.Increment(ref captures);
            return CreateBitmap();
        });

        loader.Dispose();
        loader.Start(new List<WindowInfo> { CreateWindow(1) }, (_, _) => { }, action => action());

        Assert.False(loader.IsRunning);
        Assert.Equal(0, captures);
    }

    [Fact]
    public void 运行中释放应尽快停下且不再回填()
    {
        var windows = new List<WindowInfo> { CreateWindow(1), CreateWindow(2), CreateWindow(3) };

        int captures = 0;
        int applied = 0;
        using var gate = new ManualResetEventSlim(false);
        var loader = new ThumbnailLoader(_ =>
        {
            Interlocked.Increment(ref captures);
            gate.Wait(5000);
            return CreateBitmap();
        });

        loader.Start(
            windows,
            (_, _) => Interlocked.Increment(ref applied),
            action => action());

        Assert.True(WaitUntil(() => Volatile.Read(ref captures) >= 1), "第一个窗口的截取未在预期时间内开始");

        loader.Dispose();
        gate.Set();

        Assert.True(WaitUntil(() => !loader.IsRunning), "截取未在释放后及时停止");
        Assert.Equal(1, captures);
        Assert.Equal(0, applied);
    }
}
