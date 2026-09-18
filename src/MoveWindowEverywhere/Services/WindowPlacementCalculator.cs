using System.Windows;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 窗口位置计算（纯逻辑，单位为物理像素）。
/// 支持负坐标显示器：所有计算都基于显示器工作区的绝对坐标，不做主屏假设。
/// </summary>
public static class WindowPlacementCalculator
{
    /// <summary>
    /// 计算把窗口放到工作区中央时的目标矩形。
    /// 窗口大于工作区时，宽高会被限制到工作区大小；位置对齐到工作区左上角。
    /// </summary>
    public static Int32Rect ComputeCenteredRect(Rect workArea, double width, double height)
    {
        int workLeft = (int)Math.Round(workArea.Left);
        int workTop = (int)Math.Round(workArea.Top);
        int workWidth = (int)Math.Round(workArea.Width);
        int workHeight = (int)Math.Round(workArea.Height);

        if (workWidth <= 0 || workHeight <= 0)
        {
            return new Int32Rect(workLeft, workTop, 0, 0);
        }

        int targetWidth = width <= 0 ? workWidth : (int)Math.Round(width);
        int targetHeight = height <= 0 ? workHeight : (int)Math.Round(height);

        if (targetWidth > workWidth)
        {
            targetWidth = workWidth;
        }

        if (targetHeight > workHeight)
        {
            targetHeight = workHeight;
        }

        int left = workLeft + (int)Math.Round((workWidth - targetWidth) / 2.0);
        int top = workTop + (int)Math.Round((workHeight - targetHeight) / 2.0);

        return new Int32Rect(left, top, targetWidth, targetHeight);
    }

    /// <summary>
    /// 把已有窗口矩形约束到工作区内：先限制尺寸，再平移位置，保持窗口完全可见。
    /// </summary>
    public static Int32Rect ClampToWorkArea(Int32Rect windowRect, Rect workArea)
    {
        int workLeft = (int)Math.Round(workArea.Left);
        int workTop = (int)Math.Round(workArea.Top);
        int workWidth = (int)Math.Round(workArea.Width);
        int workHeight = (int)Math.Round(workArea.Height);

        if (workWidth <= 0 || workHeight <= 0)
        {
            return new Int32Rect(workLeft, workTop, 0, 0);
        }

        int width = Math.Min(windowRect.Width, workWidth);
        int height = Math.Min(windowRect.Height, workHeight);

        int left = windowRect.X;
        if (left < workLeft)
        {
            left = workLeft;
        }
        else if (left + width > workLeft + workWidth)
        {
            left = workLeft + workWidth - width;
        }

        int top = windowRect.Y;
        if (top < workTop)
        {
            top = workTop;
        }
        else if (top + height > workTop + workHeight)
        {
            top = workTop + workHeight - height;
        }

        return new Int32Rect(left, top, width, height);
    }
}
