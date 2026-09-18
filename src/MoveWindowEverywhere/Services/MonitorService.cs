using System.Runtime.InteropServices;
using System.Windows;
using MoveWindowEverywhere.Models;
using MoveWindowEverywhere.Native;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 显示器服务：枚举显示器、按点/句柄查询显示器、捕获鼠标所在显示器。
/// 所有返回值中的矩形均为物理像素坐标，支持负坐标（显示器位于主屏左侧或上方）。
/// </summary>
public sealed class MonitorService
{
    private readonly AppLogger _logger;

    public MonitorService(AppLogger logger)
    {
        _logger = logger;
    }

    /// <summary>枚举全部显示器，并按左上角位置从左到右、从上到下编号（编号从 1 开始）。</summary>
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var handles = new List<IntPtr>();

        MonitorEnumProc callback = (hMonitor, hdc, lprc, data) =>
        {
            handles.Add(hMonitor);
            return true;
        };

        try
        {
            if (!Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                _logger.Error($"EnumDisplayMonitors 失败，Win32 错误码 {error}");
            }
        }
        finally
        {
            GC.KeepAlive(callback);
        }

        var monitors = new List<MonitorInfo>();
        foreach (IntPtr handle in handles)
        {
            MonitorInfo? info = BuildMonitorInfo(handle, -1);
            if (info is not null)
            {
                monitors.Add(info);
            }
        }

        monitors.Sort(static (a, b) =>
        {
            int compareLeft = a.MonitorRect.Left.CompareTo(b.MonitorRect.Left);
            return compareLeft != 0 ? compareLeft : a.MonitorRect.Top.CompareTo(b.MonitorRect.Top);
        });

        for (int i = 0; i < monitors.Count; i++)
        {
            monitors[i] = new MonitorInfo
            {
                Handle = monitors[i].Handle,
                Index = i + 1,
                DeviceName = monitors[i].DeviceName,
                FriendlyName = monitors[i].FriendlyName,
                IsPrimary = monitors[i].IsPrimary,
                MonitorRect = monitors[i].MonitorRect,
                WorkRect = monitors[i].WorkRect,
                DpiX = monitors[i].DpiX,
                DpiY = monitors[i].DpiY,
            };
        }

        return monitors;
    }

    /// <summary>获取鼠标当前所在显示器。坐标为物理像素。</summary>
    public MonitorInfo? GetMonitorFromPoint(int x, int y)
    {
        var point = new POINT { X = x, Y = y };
        IntPtr handle = Win32.MonitorFromPoint(point, Win32.MONITOR_DEFAULTTONEAREST);
        if (handle == IntPtr.Zero)
        {
            _logger.Warn($"MonitorFromPoint 未能定位显示器（坐标 {x},{y}）");
            return null;
        }

        return GetMonitorByHandle(handle);
    }

    /// <summary>捕获快捷键触发瞬间鼠标所在的显示器。</summary>
    public MonitorInfo? CaptureCursorMonitor()
    {
        if (!Win32.GetCursorPos(out POINT point))
        {
            int error = Marshal.GetLastWin32Error();
            _logger.Error($"GetCursorPos 失败，Win32 错误码 {error}");
            return null;
        }

        return GetMonitorFromPoint(point.X, point.Y);
    }

    /// <summary>按显示器句柄查询，句柄不在当前枚举结果中时直接从系统读取。</summary>
    public MonitorInfo? GetMonitorByHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        foreach (MonitorInfo monitor in GetMonitors())
        {
            if (monitor.Handle == handle)
            {
                return monitor;
            }
        }

        return BuildMonitorInfo(handle, 0);
    }

    public MonitorInfo? GetPrimaryMonitor()
    {
        IReadOnlyList<MonitorInfo> monitors = GetMonitors();
        foreach (MonitorInfo monitor in monitors)
        {
            if (monitor.IsPrimary)
            {
                return monitor;
            }
        }

        return monitors.Count > 0 ? monitors[0] : null;
    }

    private MonitorInfo? BuildMonitorInfo(IntPtr handle, int index)
    {
        var info = new MONITORINFOEX
        {
            CbSize = Marshal.SizeOf<MONITORINFOEX>(),
            SzDevice = string.Empty,
        };

        if (!Win32.GetMonitorInfo(handle, ref info))
        {
            int error = Marshal.GetLastWin32Error();
            _logger.Error($"GetMonitorInfo 失败，Win32 错误码 {error}");
            return null;
        }

        uint dpiX = 96;
        uint dpiY = 96;
        int hr = Win32.GetDpiForMonitor(handle, Win32.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
        if (hr != 0)
        {
            // 取不到 DPI 时按 96 处理，不改变坐标计算正确性（坐标以像素为准）
            _logger.Warn($"GetDpiForMonitor 返回 0x{hr:X8}，按 96 DPI 处理显示器 {info.SzDevice}");
            dpiX = 96;
            dpiY = 96;
        }

        string deviceName = string.IsNullOrWhiteSpace(info.SzDevice) ? "UNKNOWN" : info.SzDevice;
        string friendlyName = LookupFriendlyName(deviceName);

        return new MonitorInfo
        {
            Handle = handle,
            Index = index,
            DeviceName = deviceName,
            FriendlyName = friendlyName,
            IsPrimary = (info.DwFlags & Win32.MONITORINFOF_PRIMARY) != 0,
            MonitorRect = new Rect(info.RcMonitor.Left, info.RcMonitor.Top, info.RcMonitor.Width, info.RcMonitor.Height),
            WorkRect = new Rect(info.RcWork.Left, info.RcWork.Top, info.RcWork.Width, info.RcWork.Height),
            DpiX = dpiX,
            DpiY = dpiY,
        };
    }

    private static string LookupFriendlyName(string deviceName)
    {
        try
        {
            uint index = 0;
            // 设置上限：万一枚举接口异常地持续返回成功，也不至于在此死循环
            while (index < 32)
            {
                var device = new DISPLAY_DEVICE
                {
                    Cb = Marshal.SizeOf<DISPLAY_DEVICE>(),
                    DeviceName = string.Empty,
                    DeviceString = string.Empty,
                    DeviceId = string.Empty,
                    DeviceKey = string.Empty,
                };

                if (!Win32.EnumDisplayDevices(null, index, ref device, 0))
                {
                    break;
                }

                if (string.Equals(device.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(device.DeviceString) ? deviceName : device.DeviceString.Trim();
                }

                index++;
            }
        }
        catch (Exception)
        {
            return deviceName;
        }

        return deviceName;
    }
}
