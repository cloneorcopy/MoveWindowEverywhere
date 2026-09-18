using MoveWindowEverywhere.Models;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 决定哪些窗口值得去截图，以及一次最多截几个。
/// </summary>
/// <remarks>
/// 这一段是纯数据判断，不碰任何 Win32 调用，因此可以直接用单元测试覆盖。
/// 截图本身相对昂贵（要为目标窗口分配一块与窗口等大的位图），
/// 把不合格的窗口提前剔掉，既省时间也省内存。
/// </remarks>
public static class ThumbnailCapturePolicy
{
    /// <summary>一次最多截取的窗口数量。列表很长时只截前若干个，避免无谓开销。</summary>
    public const int MaxCapturesPerOpen = 24;

    /// <summary>缩略图的目标高度（物理像素）。宽度按窗口宽高比换算。</summary>
    public const int TargetHeight = 56;

    /// <summary>缩略图的目标最大宽度（物理像素），用于限制超宽窗口。</summary>
    public const int MaxTargetWidth = 96;

    /// <summary>截图缓冲区的最大宽高。窗口比这更大时只取左上角区域，用来限制内存占用。</summary>
    public const int MaxCaptureWidth = 1280;

    public const int MaxCaptureHeight = 800;

    /// <summary>
    /// 单个窗口是否适合截图。最小化窗口没有任何可渲染内容，截出来只会是黑块，
    /// 因此直接排除，交给界面显示占位图。
    /// </summary>
    public static bool ShouldCapture(WindowInfo? window)
    {
        if (window is null)
        {
            return false;
        }

        if (window.Handle == IntPtr.Zero || !window.IsVisible || window.IsCloaked)
        {
            return false;
        }

        return window.State != WindowStateKind.Minimized;
    }

    /// <summary>从候选窗口里挑出要截图的前若干个，保持原有顺序。</summary>
    public static IReadOnlyList<WindowInfo> SelectCandidates(
        IEnumerable<WindowInfo>? windows,
        int maxCaptures = MaxCapturesPerOpen)
    {
        var candidates = new List<WindowInfo>();
        if (windows is null || maxCaptures <= 0)
        {
            return candidates;
        }

        foreach (WindowInfo window in windows)
        {
            if (candidates.Count >= maxCaptures)
            {
                break;
            }

            if (ShouldCapture(window))
            {
                candidates.Add(window);
            }
        }

        return candidates;
    }

    /// <summary>按窗口宽高比算出缩略图尺寸，保证不超出目标框且至少 1 像素。</summary>
    public static (int Width, int Height) ComputeThumbnailSize(int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0)
        {
            return (MaxTargetWidth, TargetHeight);
        }

        double scale = Math.Min(
            MaxTargetWidth / (double)windowWidth,
            TargetHeight / (double)windowHeight);

        int width = Math.Max(1, (int)Math.Round(windowWidth * scale));
        int height = Math.Max(1, (int)Math.Round(windowHeight * scale));
        return (width, height);
    }
}
