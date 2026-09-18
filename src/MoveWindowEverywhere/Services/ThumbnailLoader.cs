using System.Windows.Media.Imaging;
using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 在后台线程逐张截取窗口缩略图，并回填到界面。
/// </summary>
/// <remarks>
/// 截取必须离开 UI 线程：<c>PrintWindow</c> 会等待目标窗口渲染，遇到迟钝的窗口可能要
/// 几十到上百毫秒。放在 UI 线程上串行执行，列表会出现肉眼可见的卡顿。
/// 这里把截取放到后台任务里逐张进行，每张完成后通过调用方给定的
/// <c>dispatch</c> 回到 UI 线程写回属性，界面保持可交互，缩略图逐张浮现。
/// 捕获函数由构造参数注入，便于在测试里替换成不触碰 Win32 的假实现。
/// </remarks>
public sealed class ThumbnailLoader : IDisposable
{
    private readonly Func<IntPtr, BitmapSource?> _capture;
    private readonly AppLogger? _logger;
    private volatile bool _cancelled;
    private int _running;

    public ThumbnailLoader(Func<IntPtr, BitmapSource?> capture, AppLogger? logger = null)
    {
        _capture = capture;
        _logger = logger;
    }

    /// <summary>是否有一轮截取正在进行。</summary>
    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>
    /// 启动一轮截取。同一实例同时只允许一轮，重复调用会被直接忽略。
    /// 没有任何候选窗口时不做任何事，也不占用线程。
    /// </summary>
    public void Start(
        IEnumerable<WindowInfo>? windows,
        Action<WindowInfo, BitmapSource> apply,
        Action<Action> dispatch)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(dispatch);

        IReadOnlyList<WindowInfo> candidates = ThumbnailCapturePolicy.SelectCandidates(windows);
        if (candidates.Count == 0 || _cancelled)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() => RunCoreAsync(candidates, apply, dispatch));
    }

    /// <summary>截取主循环。独立出来是为了让测试可以直接等待它结束。</summary>
    internal async Task RunCoreAsync(
        IReadOnlyList<WindowInfo> candidates,
        Action<WindowInfo, BitmapSource> apply,
        Action<Action> dispatch)
    {
        try
        {
            foreach (WindowInfo window in candidates)
            {
                if (_cancelled)
                {
                    return;
                }

                BitmapSource? bitmap = TryCaptureSafely(window);
                if (bitmap is null || _cancelled)
                {
                    continue;
                }

                BitmapSource captured = bitmap;
                try
                {
                    dispatch(() => apply(window, captured));
                }
                catch (Exception ex)
                {
                    // 界面已经关闭时回填会失败，这属于正常情况，记一条就够了
                    _logger?.Warn($"回填窗口缩略图失败：{ex.Message}");
                }

                // 每张之间让出一次，避免长时间占满线程池
                await Task.Yield();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private BitmapSource? TryCaptureSafely(WindowInfo window)
    {
        try
        {
            return _capture(window.Handle);
        }
        catch (Exception ex)
        {
            _logger?.Warn($"截取窗口 0x{window.Handle.ToInt64():X} 缩略图失败：{ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _cancelled = true;
        Interlocked.Exchange(ref _running, 0);
    }
}
