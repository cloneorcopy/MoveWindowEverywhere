using System.Runtime.InteropServices;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Native;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 顶层窗口枚举。只负责采集原始数据，过滤规则统一交给 <see cref="WindowFilterPolicy"/>。
/// </summary>
/// <remarks>
/// 采集分两段，原因是系统里的顶层窗口远多于列表里能出现的窗口：一台日常使用的机器上
/// <c>EnumWindows</c> 会返回一千多个句柄，而通过过滤的常常不到二十个。
/// 探测段只做进程内查询（可见性、样式、类名、标题、DWM cloak），逐个句柄做完整一遍也只有几十毫秒；
/// 补全段读的却是两个贵字段——进程名要打开进程句柄，图标要向目标窗口发消息——
/// 把它们摊到一千多个句柄上就是半秒起步，遇到忙而未挂的窗口还能放大到数秒。
/// 所以顺序是先探测、先过滤，最后只给活下来的窗口补全。
/// </remarks>
public sealed class WindowEnumerator
{
    private readonly AppLogger _logger;
    private readonly ProcessNameResolver _processNameResolver;

    public WindowEnumerator(AppLogger logger)
    {
        _logger = logger;
        _processNameResolver = new ProcessNameResolver(logger);
    }

    /// <summary>枚举全部顶层窗口，包括被过滤掉的窗口，因此每个窗口的昂贵字段都会补齐。慢，仅供对照与兜底。</summary>
    public IReadOnlyList<WindowInfo> Enumerate() => EnumerateCore(filter: null);

    /// <summary>
    /// 枚举通过 <paramref name="filter"/> 的候选窗口。
    /// 先用零成本探测淘汰，再只为留下的窗口补全进程名与图标，是应用实际走的那条路。
    /// </summary>
    public IReadOnlyList<WindowInfo> Enumerate(WindowFilterPolicy filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return EnumerateCore(filter);
    }

    private IReadOnlyList<WindowInfo> EnumerateCore(WindowFilterPolicy? filter)
    {
        var kept = new List<WindowInfo>();

        foreach (IntPtr handle in CollectHandles())
        {
            try
            {
                WindowInfo probe = Probe(handle);
                if (filter is null || filter.IsIncluded(probe))
                {
                    kept.Add(probe);
                }
            }
            catch (Exception ex)
            {
                // 单个窗口采集失败不应中断整个枚举
                _logger.Warn($"探测窗口 0x{handle.ToInt64():X} 信息失败", ex);
            }
        }

        foreach (WindowInfo window in kept)
        {
            try
            {
                Complete(window);
            }
            catch (Exception ex)
            {
                _logger.Warn($"补全窗口 0x{window.Handle.ToInt64():X} 信息失败", ex);
            }
        }

        return kept;
    }

    /// <summary>收集全部顶层句柄。这里不做任何判断，判断留在后面按窗口逐个做。</summary>
    private IReadOnlyList<IntPtr> CollectHandles()
    {
        var seen = new List<IntPtr>();

        EnumWindowsProc callback = (hwnd, lParam) =>
        {
            seen.Add(hwnd);
            return true;
        };

        try
        {
            if (!Win32.EnumWindows(callback, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                _logger.Error($"EnumWindows 失败，Win32 错误码 {error}");
            }
        }
        finally
        {
            GC.KeepAlive(callback);
        }

        return seen;
    }

    /// <summary>读取单个窗口的完整快照，包含进程名与图标。句柄无效时返回 null。</summary>
    public WindowInfo? TryCreateWindowInfo(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !Win32.IsWindow(handle))
        {
            return null;
        }

        WindowInfo probe = Probe(handle);
        Complete(probe);
        return probe;
    }

    /// <summary>只读进程内可得的字段。这些查询不向目标窗口发消息，摊到一千多个句柄上也不过几十毫秒。</summary>
    private WindowInfo Probe(IntPtr handle)
    {
        _ = Win32.GetWindowThreadProcessId(handle, out uint processId);

        WindowStateKind state = WindowStateKind.Normal;
        if (Win32.IsIconic(handle))
        {
            state = WindowStateKind.Minimized;
        }
        else if (Win32.IsZoomed(handle))
        {
            state = WindowStateKind.Maximized;
        }

        return new WindowInfo
        {
            Handle = handle,
            Title = Win32.GetWindowTitle(handle).Trim(),
            ProcessId = processId,
            ClassName = Win32.GetWindowClassName(handle),
            State = state,
            MonitorHandle = Win32.MonitorFromWindow(handle, Win32.MONITOR_DEFAULTTONEAREST),
            IsVisible = Win32.IsWindowVisible(handle),
            IsCloaked = Win32.IsWindowCloaked(handle),
            HasOwner = Win32.GetWindow(handle, Win32.GW_OWNER) != IntPtr.Zero,
            IsOwnProcess = processId == (uint)Environment.ProcessId,
            Style = Win32.GetWindowLongValue(handle, Win32.GWL_STYLE),
            ExtendedStyle = Win32.GetWindowLongValue(handle, Win32.GWL_EXSTYLE),
        };
    }

    /// <summary>补上两个贵字段：进程名要打开进程句柄，图标要向目标窗口发消息。</summary>
    private void Complete(WindowInfo window)
    {
        window.ProcessName = _processNameResolver.Resolve(window.ProcessId);
        window.IconHandle = GetIconHandle(window.Handle);
    }

    private static IntPtr GetIconHandle(IntPtr handle)
    {
        // 关键：绝不能在这里用同步的 SendMessage 向目标窗口索取图标。
        // 窗口列表里只要出现一个无响应的窗口（后台挂起的程序、被冻结的 UWP 应用），
        // SendMessage 就会一直等下去，而枚举运行在 UI 线程上，
        // 结果是整个程序界面冻结：选择器出不来、托盘菜单也点不动。
        //
        // 先用 IsHungAppWindow 做一次零成本判断：窗口已无响应就直接跳过消息查询，
        // 省掉逐个等待超时的开销，然后统一回退到窗口类图标。
        if (!Win32.IsHungAppWindow(handle))
        {
            IntPtr queried = Win32.SendMessageQuery(handle, Win32.WM_GETICON, (IntPtr)Win32.ICON_BIG, IntPtr.Zero);
            if (queried == IntPtr.Zero)
            {
                queried = Win32.SendMessageQuery(handle, Win32.WM_GETICON, (IntPtr)Win32.ICON_SMALL2, IntPtr.Zero);
            }

            if (queried == IntPtr.Zero)
            {
                queried = Win32.SendMessageQuery(handle, Win32.WM_GETICON, (IntPtr)Win32.ICON_SMALL, IntPtr.Zero);
            }

            if (queried != IntPtr.Zero)
            {
                return queried;
            }
        }

        // 类图标是纯查询，不会阻塞
        IntPtr icon = Win32.GetClassLongPtrValue(handle, Win32.GCLP_HICON);
        if (icon == IntPtr.Zero)
        {
            icon = Win32.GetClassLongPtrValue(handle, Win32.GCLP_HICONSM);
        }

        return icon;
    }
}
