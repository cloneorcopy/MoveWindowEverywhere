using System.Drawing;
using System.Drawing.Drawing2D;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 运行时生成托盘图标，避免引入二进制资源文件。
/// 图标主体为显示器轮廓加一个向右的箭头，表达「把窗口移到另一个屏幕」。
/// </summary>
public static class IconFactory
{
    public static Icon CreateAppIcon(int size = 64)
    {
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(System.Drawing.Color.Transparent);

            float unit = size / 16f;

            // 显示器外框与屏幕
            var monitor = new RectangleF(1.2f * unit, 2.6f * unit, 13.6f * unit, 9.4f * unit);
            using var bodyBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 46, 111, 243));
            using var screenBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 236, 243, 255));
            graphics.FillRoundedRectangle(bodyBrush, monitor, 1.4f * unit);
            graphics.FillRoundedRectangle(screenBrush, new RectangleF(monitor.Left + unit, monitor.Top + unit, monitor.Width - 2 * unit, monitor.Height - 2 * unit), 0.9f * unit);

            // 支架
            using var standBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 78, 170));
            graphics.FillRectangle(standBrush, new RectangleF(6.8f * unit, 12f * unit, 2.4f * unit, 1.6f * unit));
            graphics.FillRectangle(standBrush, new RectangleF(5.4f * unit, 13.4f * unit, 5.2f * unit, 0.9f * unit));

            // 屏幕内的箭头
            using var arrowBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 46, 111, 243));
            using var arrowPen = new Pen(arrowBrush, Math.Max(1.2f, 0.9f * unit));
            arrowPen.StartCap = LineCap.Round;
            arrowPen.EndCap = LineCap.Round;
            float centerX = monitor.Left + monitor.Width / 2f;
            float centerY = monitor.Top + monitor.Height / 2f;
            graphics.DrawLine(arrowPen, centerX - 2.6f * unit, centerY, centerX + 2.4f * unit, centerY);
            PointF[] arrowHead =
            {
                new PointF(centerX + 3.4f * unit, centerY),
                new PointF(centerX + 1.4f * unit, centerY - 1.5f * unit),
                new PointF(centerX + 1.4f * unit, centerY + 1.5f * unit),
            };
            graphics.FillPolygon(arrowBrush, arrowHead);
        }

        IntPtr iconHandle = bitmap.GetHicon();
        return Icon.FromHandle(iconHandle);
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
