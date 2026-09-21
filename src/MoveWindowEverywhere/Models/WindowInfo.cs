namespace MoveWindowEverywhere.Models;

/// <summary>窗口状态。</summary>
public enum WindowStateKind
{
    Normal = 0,
    Minimized = 1,
    Maximized = 2,
}

/// <summary>
/// 窗口信息快照。由 <c>WindowEnumerator</c> 通过 Win32 采集，
/// 所有字段均为纯数据，便于在过滤策略与单元测试中直接使用。
/// </summary>
public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }

    public string Title { get; init; } = string.Empty;

    public uint ProcessId { get; init; }

    /// <summary>
    /// 进程名。读取要打开进程句柄，属于昂贵字段，由 <c>WindowEnumerator</c> 在过滤之后
    /// 只为进入列表的窗口补全；在此之前保持默认值。
    /// </summary>
    public string ProcessName { get; internal set; } = "未知进程";

    public string ClassName { get; init; } = string.Empty;

    public WindowStateKind State { get; init; }

    public IntPtr MonitorHandle { get; init; }

    public bool IsVisible { get; init; }

    public bool IsCloaked { get; init; }

    public bool HasOwner { get; init; }

    public bool IsOwnProcess { get; init; }

    public long Style { get; init; }

    public long ExtendedStyle { get; init; }

    /// <summary>
    /// 窗口图标句柄，UI 层按需转换为 <c>ImageSource</c>。
    /// 取它要向目标窗口发消息，属于昂贵字段，与 <see cref="ProcessName"/> 同样在过滤之后补全。
    /// </summary>
    public IntPtr IconHandle { get; internal set; }

    /// <summary>
    /// 窗口当前所在显示器的编号，从 1 开始。0 表示未标注。
    /// 这不是枚举阶段采集的原始数据，而是宿主在枚举完成后按显示器列表填充的展示信息；
    /// 只有一台显示器时会刻意保持为 0，避免每一行都重复同一个编号。
    /// </summary>
    public int MonitorIndex { get; set; }

    /// <summary>窗口是否已经位于本次操作的目标显示器上。与 <see cref="MonitorIndex"/> 一同由宿主填充。</summary>
    public bool IsOnTargetMonitor { get; set; }

    /// <summary>列表展示用的显示器标签，未标注时为空串。</summary>
    public string MonitorLabel => MonitorIndex > 0 ? $"屏 {MonitorIndex}" : string.Empty;

    public bool IsToolWindow => (ExtendedStyle & 0x00000080L) != 0;

    public bool IsNoActivateWindow => (ExtendedStyle & 0x08000000L) != 0;

    public bool HasMinimizeBox => (Style & 0x00020000L) != 0;

    public override string ToString() => $"{ProcessName} | {Title} | PID {ProcessId}";
}
