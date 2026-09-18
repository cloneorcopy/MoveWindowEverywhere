using System.Windows;

namespace MoveWindowEverywhere.Models;

/// <summary>
/// 显示器信息。所有矩形均为物理像素，坐标系为 Windows 虚拟屏幕坐标系，
/// 因此支持位于主屏左侧或上方导致的负坐标。
/// </summary>
public sealed class MonitorInfo
{
    public IntPtr Handle { get; init; }

    /// <summary>应用内部编号，按左上角位置从左到右、从上到下排序后得到。</summary>
    public int Index { get; init; }

    public string DeviceName { get; init; } = string.Empty;

    public string FriendlyName { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }

    /// <summary>显示器完整矩形（物理像素）。</summary>
    public Rect MonitorRect { get; init; }

    /// <summary>显示器工作区矩形，已排除任务栏（物理像素）。</summary>
    public Rect WorkRect { get; init; }

    public uint DpiX { get; init; }

    public uint DpiY { get; init; }

    public int Width => (int)Math.Round(MonitorRect.Width);

    public int Height => (int)Math.Round(MonitorRect.Height);

    /// <summary>用于选择器底部提示的短描述。</summary>
    public string ShortDescription => $"显示器 {Index}：{FriendlyName}（{Width} × {Height}）";

    public override string ToString() => ShortDescription;
}
