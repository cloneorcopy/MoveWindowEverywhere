using System.Runtime.InteropServices;

namespace MoveWindowEverywhere.Native;

/// <summary>Win32 结构体与委托声明。仅声明本项目实际使用到的成员。</summary>
internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WINDOWPLACEMENT
{
    public uint Length;
    public uint Flags;
    public uint ShowCmd;
    public POINT PtMinPosition;
    public POINT PtMaxPosition;
    public RECT RcNormalPosition;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct MONITORINFOEX
{
    public int CbSize;
    public RECT RcMonitor;
    public RECT RcWork;
    public uint DwFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string SzDevice;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct DISPLAY_DEVICE
{
    public int Cb;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;

    public uint StateFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceId;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CHANGEFILTERSTRUCT
{
    public uint CbSize;
    public uint ExtStatus;
}

/// <summary>CreateDIBSection 使用的位图信息头。仅声明 32 位 BGRA 自上而下所需字段。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BITMAPINFOHEADER
{
    public uint BiSize;
    public int BiWidth;
    public int BiHeight;
    public ushort BiPlanes;
    public ushort BiBitCount;
    public uint BiCompression;
    public uint BiSizeImage;
    public int BiXPelsPerMeter;
    public int BiYPelsPerMeter;
    public uint BiClrUsed;
    public uint BiClrImportant;
}

/// <summary>BITMAPINFO 只用到单色表，这里保留首项即可。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BITMAPINFO
{
    public BITMAPINFOHEADER Header;
    public uint Colors;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr Hwnd;
    public uint Message;
    public IntPtr WParam;
    public IntPtr LParam;
    public uint Time;
    public POINT Point;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct WNDCLASSEX
{
    public int CbSize;
    public uint Style;
    public WndProcDelegate LpfnWndProc;
    public int CbClsExtra;
    public int CbWndExtra;
    public IntPtr HInstance;
    public IntPtr HIcon;
    public IntPtr HCursor;
    public IntPtr HbrBackground;
    public string LpszMenuName;
    public string LpszClassName;
    public IntPtr HIconSm;
}

/// <summary>隐藏消息窗口的窗口过程委托。</summary>
internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
