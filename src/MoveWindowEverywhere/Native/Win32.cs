using System.Runtime.InteropServices;
using System.Text;

namespace MoveWindowEverywhere.Native;

/// <summary>
/// 本项目使用的全部 Win32 API 声明与常量。所有涉及 SetLastError 的调用均通过
/// <see cref="Marshal.GetLastWin32Error"/> 读取错误码，便于诊断。
/// </summary>
internal static partial class Win32
{
    // ---------------- 窗口消息 ----------------
    public const int WM_CLOSE = 0x0010;
    public const int WM_SYSCOMMAND = 0x0112;
    public const int WM_GETICON = 0x007F;
    public const int WM_HOTKEY = 0x0312;

    /// <summary>第二实例用于唤醒已有实例打开选择器的自定义消息（WM_APP + 0x21）。</summary>
    public const int MSG_APP_SHOW_SELECTOR = 0x8000 + 0x21;

    public const int SC_RESTORE = 0xF120;
    public const int SC_MAXIMIZE = 0xF030;

    // ---------------- 窗口查询常量 ----------------
    public const uint GW_OWNER = 4;

    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    public const long WS_MINIMIZE = 0x20000000L;
    public const long WS_MAXIMIZE = 0x01000000L;
    public const long WS_VISIBLE = 0x10000000L;
    public const long WS_CHILD = 0x40000000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
    public const long WS_EX_APPWINDOW = 0x00040000L;
    public const long WS_EX_NOACTIVATE = 0x08000000L;

    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;

    // ---------------- ShowWindow / SetWindowPos ----------------
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_MAXIMIZE = 3;
    public const int SW_RESTORE = 9;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // ---------------- 显示器 ----------------
    public const uint MONITOR_DEFAULTTONULL = 0;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITORINFOF_PRIMARY = 0x00000001;

    public const int MDT_EFFECTIVE_DPI = 0;
    public const int EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;

    // ---------------- DWM ----------------
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;

    // ---------------- UIPI ----------------
    public const uint MSGFLT_ALLOW = 1;
    public const uint MSGFLT_DISALLOW = 2;

    // ---------------- 虚拟键 ----------------
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    // ---------------- 错误码 ----------------
    public const int ERROR_ACCESS_DENIED = 5;
    public const int ERROR_INVALID_WINDOW_HANDLE = 1400;
    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    // ---------------- 热键修饰符 ----------------
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // ---------------- user32 ----------------
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    public static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
    public static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLong")]
    public static extern uint GetClassLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    // -------- 消息循环（单元测试中用于泵消息） --------
    public const uint PM_REMOVE = 0x0001;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr DispatchMessage(ref MSG lpmsg);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public const uint SMTO_NORMAL = 0x0000;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    /// <summary>
    /// 带超时地向窗口索取信息并返回结果，超时或失败时返回 <see cref="IntPtr.Zero"/>。
    /// 向其他进程的窗口发消息时必须用这个而不是 SendMessage：SendMessage 是同步的，
    /// 只要目标窗口所属线程无响应就会永久阻塞，而调用方通常在 UI 线程上，
    /// 会导致整个程序界面冻结（选择器出不来、托盘菜单点不动）。
    /// SMTO_ABORTIFHUNG 会让系统对已标记为未响应的窗口直接返回，不再等待。
    /// </summary>
    public static IntPtr SendMessageQuery(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint timeoutMilliseconds = 120)
    {
        SendMessageTimeout(
            hWnd,
            msg,
            wParam,
            lParam,
            SMTO_ABORTIFHUNG | SMTO_NORMAL,
            timeoutMilliseconds,
            out IntPtr result);

        return result;
    }

    /// <summary>
    /// 判断窗口所属线程是否已停止响应（被挂起或正在处理长任务）。
    /// 这是纯查询，立即返回，可用来决定要不要向该窗口发消息，
    /// 避免把时间浪费在逐个等待超时上。
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool IsHungAppWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, ref CHANGEFILTERSTRUCT pChangeFilterStruct);

    // -------- 窗口类与隐藏消息窗口 --------
    public const int GWLP_USERDATA = -21;
    public const int ERROR_CLASS_ALREADY_EXISTS = 1410;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    public static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static IntPtr SetWindowLongPtrValue(IntPtr hWnd, int nIndex, IntPtr value) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, value) : SetWindowLong32(hWnd, nIndex, value);

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    // ---------------- dwmapi ----------------
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    public static extern int DwmGetWindowAttributeRect(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    public static extern int DwmGetWindowAttributeInt(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    // ---------------- 窗口截图（缩略图） ----------------
    /// <summary>PrintWindow 标志：请求窗口完整渲染，包括 DirectComposition 与硬件加速内容。</summary>
    public const uint PW_RENDERFULLCONTENT = 0x00000002;

    public const uint SRCCOPY = 0x00CC0020;
    public const int HALFTONE = 4;
    public const uint BI_RGB = 0;
    public const uint DIB_RGB_COLORS = 0;

    /// <summary>
    /// 请求窗口把自己渲染到给定的设备上下文。窗口必须响应 WM_PRINT 才会输出内容，
    /// 因此调用前应先确认窗口未处于无响应状态，且调用不得放在 UI 线程上长时间等待。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    // ---------------- gdi32 ----------------
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool StretchBlt(
        IntPtr hdcDest,
        int xDest,
        int yDest,
        int wDest,
        int hDest,
        IntPtr hdcSrc,
        int xSrc,
        int ySrc,
        int wSrc,
        int hSrc,
        uint rop);

    [DllImport("gdi32.dll")]
    public static extern int SetStretchBltMode(IntPtr hdc, int mode);

    // ---------------- shcore ----------------
    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>获取窗口样式（GWL_STYLE）。</summary>
    public static long GetWindowLongValue(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex).ToInt64() : GetWindowLong32(hWnd, nIndex);

    /// <summary>获取窗口类样式图标句柄（GCLP_HICON / GCLP_HICONSM）。</summary>
    public static IntPtr GetClassLongPtrValue(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetClassLongPtr64(hWnd, nIndex) : new IntPtr(GetClassLong32(hWnd, nIndex));
}

/// <summary>
/// 针对 Win32 调用的辅助封装：读取标题、类名，以及带错误信息的调用结果。
/// </summary>
internal static partial class Win32
{
    public static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        int copied = GetWindowText(hWnd, builder, builder.Capacity);
        return copied > 0 ? builder.ToString() : string.Empty;
    }

    public static string GetWindowClassName(IntPtr hWnd)
    {
        var builder = new StringBuilder(256);
        int copied = GetClassName(hWnd, builder, builder.Capacity);
        return copied > 0 ? builder.ToString() : string.Empty;
    }

    public static bool IsWindowCloaked(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        int hr = DwmGetWindowAttributeInt(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    /// <summary>读取窗口真实边框（不含 DWM 阴影等不可见区域）。失败时回退到 GetWindowRect。</summary>
    public static bool TryGetVisibleWindowBounds(IntPtr hWnd, out RECT rect)
    {
        int hr = DwmGetWindowAttributeRect(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT extended, Marshal.SizeOf<RECT>());
        if (hr == 0 && extended.Right > extended.Left && extended.Bottom > extended.Top)
        {
            rect = extended;
            return true;
        }

        if (GetWindowRect(hWnd, out RECT fallback) && fallback.Right > fallback.Left && fallback.Bottom > fallback.Top)
        {
            rect = fallback;
            return true;
        }

        rect = default;
        return false;
    }
}
