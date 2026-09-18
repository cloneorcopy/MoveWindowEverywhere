using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MoveWindowEverywhere.Native;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 隐藏消息窗口：自建窗口类并创建一个不可见的顶层窗口，用于接收 WM_HOTKEY，
/// 以及接收第二实例发来的唤醒消息。
/// 之所以不使用 WinForms 的 NativeWindow，是因为自定义类名必须先注册，
/// 否则 CreateHandle 会抛出「窗口类名无效」。
/// </summary>
public sealed class HiddenMessageWindow : IDisposable
{
    /// <summary>固定窗口类名，用于单实例唤醒定位。</summary>
    public const string WindowClassName = "MoveWindowEverywhere.MessageWindow";

    private static readonly WndProcDelegate WindowProcDelegate = StaticWindowProc;

    private readonly string _className;

    private IntPtr _handle;
    private IntPtr _moduleHandle;
    private GCHandle _selfHandle;
    private bool _disposed;

    public event EventHandler<int>? HotKeyReceived;

    public event EventHandler? ShowSelectorRequested;

    /// <summary>
    /// 创建隐藏消息窗口。类名默认使用 <see cref="WindowClassName"/>；
    /// 测试可传入唯一类名以与真实运行中的实例相互隔离。
    /// </summary>
    public HiddenMessageWindow(string? windowClassName = null)
    {
        _className = string.IsNullOrWhiteSpace(windowClassName) ? WindowClassName : windowClassName;
        _moduleHandle = Win32.GetModuleHandle(null);

        var windowClass = new WNDCLASSEX
        {
            CbSize = Marshal.SizeOf<WNDCLASSEX>(),
            Style = 0,
            LpfnWndProc = WindowProcDelegate,
            CbClsExtra = 0,
            CbWndExtra = 0,
            HInstance = _moduleHandle,
            HIcon = IntPtr.Zero,
            HCursor = IntPtr.Zero,
            HbrBackground = IntPtr.Zero,
            LpszMenuName = string.Empty,
            LpszClassName = _className,
            HIconSm = IntPtr.Zero,
        };

        ushort atom = Win32.RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != Win32.ERROR_CLASS_ALREADY_EXISTS)
            {
                throw new Win32Exception(error, "注册隐藏消息窗口的窗口类失败");
            }
        }

        _handle = Win32.CreateWindowEx(
            0,
            _className,
            "MoveWindowEverywhere Message Window",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            _moduleHandle,
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "创建隐藏消息窗口失败");
        }

        _selfHandle = GCHandle.Alloc(this);
        Win32.SetWindowLongPtrValue(_handle, Win32.GWLP_USERDATA, GCHandle.ToIntPtr(_selfHandle));

        AllowMessageFromLowerIntegrity(Win32.MSG_APP_SHOW_SELECTOR);
    }

    public IntPtr Handle => _handle;

    public string ClassName => _className;

    public bool IsValid => _handle != IntPtr.Zero && Win32.IsWindow(_handle);

    /// <summary>
    /// 查找已存在实例的隐藏消息窗口。使用 EnumWindows 枚举而非 FindWindow，
    /// 因为不可见窗口通过 FindWindow 查找并不总是可靠。
    /// 类名默认使用 <see cref="WindowClassName"/>，测试可传入唯一类名。
    /// </summary>
    public static IntPtr FindExisting(string? windowClassName = null)
    {
        string target = string.IsNullOrWhiteSpace(windowClassName) ? WindowClassName : windowClassName;
        IntPtr found = IntPtr.Zero;

        EnumWindowsProc callback = (hwnd, lParam) =>
        {
            if (string.Equals(Win32.GetWindowClassName(hwnd), target, StringComparison.Ordinal))
            {
                found = hwnd;
                return false;
            }

            return true;
        };

        try
        {
            Win32.EnumWindows(callback, IntPtr.Zero);
        }
        finally
        {
            GC.KeepAlive(callback);
        }

        return found;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            Win32.DestroyWindow(_handle);
            _handle = IntPtr.Zero;
        }

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        if (_moduleHandle != IntPtr.Zero)
        {
            Win32.UnregisterClass(_className, _moduleHandle);
        }
    }

    private void HandleMessage(uint message, IntPtr wParam)
    {
        if (message == Win32.WM_HOTKEY)
        {
            HotKeyReceived?.Invoke(this, wParam.ToInt32());
            return;
        }

        if (message == Win32.MSG_APP_SHOW_SELECTOR)
        {
            ShowSelectorRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static IntPtr StaticWindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        IntPtr userData = (IntPtr)Win32.GetWindowLongValue(hWnd, Win32.GWLP_USERDATA);
        if (userData != IntPtr.Zero)
        {
            GCHandle handle = GCHandle.FromIntPtr(userData);
            if (handle.Target is HiddenMessageWindow window)
            {
                window.HandleMessage(message, wParam);

                if (message == Win32.WM_HOTKEY || message == Win32.MSG_APP_SHOW_SELECTOR)
                {
                    return IntPtr.Zero;
                }
            }
        }

        return Win32.DefWindowProc(hWnd, message, wParam, lParam);
    }

    /// <summary>允许完整性级别更低的进程（例如提升运行的第二实例）向本窗口发送消息。</summary>
    private void AllowMessageFromLowerIntegrity(uint message)
    {
        try
        {
            var filter = new CHANGEFILTERSTRUCT { CbSize = (uint)Marshal.SizeOf<CHANGEFILTERSTRUCT>() };
            if (!Win32.ChangeWindowMessageFilterEx(_handle, message, Win32.MSGFLT_ALLOW, ref filter))
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"ChangeWindowMessageFilterEx 失败，Win32 错误码 {error}");
            }
        }
        catch (Exception)
        {
            // 该优化失败不影响核心功能
        }
    }
}
