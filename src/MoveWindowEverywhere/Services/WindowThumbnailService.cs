using System.Windows.Media;
using System.Windows.Media.Imaging;
using MoveWindowEverywhere.Native;

namespace MoveWindowEverywhere.Services;

/// <summary>
/// 窗口缩略图截取。
/// </summary>
/// <remarks>
/// 用 <c>PrintWindow</c> 请目标窗口把自己渲染到一块内存位图上，再等比缩放到缩略图尺寸。
/// 这里有三点必须小心，任何一点做错都会让程序整体卡死：
/// <list type="number">
/// <item>
/// <c>PrintWindow</c> 本质上是向目标窗口发消息并等它渲染完，与 <c>SendMessage</c> 一样会被
/// 无响应的窗口拖住。所以调用前先用 <c>IsHungAppWindow</c> 判断，并且**永远不要**在 UI 线程上调用本类。
/// </item>
/// <item>
/// 截图缓冲区必须限制大小。一个 4K 最大化窗口的 32 位位图接近 33 MB，
/// 几十个窗口叠加足以把内存吃光，因此宽度和高度都做了上限夹紧，超出部分直接裁掉。
/// </item>
/// <item>
/// 所有 GDI 对象都要在 finally 里释放。DC、位图、位图的选择状态漏掉任何一个，
/// 都会在反复截图后耗尽 GDI 句柄（进程上限通常只有一万个）。
/// </item>
/// </list>
/// 返回的 <see cref="BitmapSource"/> 已经 <c>Freeze()</c>，可以安全地从后台线程交给 UI 线程使用。
/// </remarks>
public sealed class WindowThumbnailService
{
    private readonly AppLogger? _logger;

    public WindowThumbnailService(AppLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 截取窗口缩略图。窗口不可截、无响应、渲染失败或整幅为纯色时返回 null，
    /// 由界面显示占位图而不是一块黑或是空白。
    /// </summary>
    public BitmapSource? TryCapture(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !Win32.IsWindow(windowHandle))
        {
            return null;
        }

        // 无响应的窗口会让 PrintWindow 长时间等待，直接放弃
        if (Win32.IsHungAppWindow(windowHandle))
        {
            _logger?.Warn($"窗口 0x{windowHandle.ToInt64():X} 无响应，跳过缩略图截取");
            return null;
        }

        if (!Win32.TryGetVisibleWindowBounds(windowHandle, out RECT bounds)
            || bounds.Width <= 0
            || bounds.Height <= 0)
        {
            return null;
        }

        int captureWidth = Math.Min(bounds.Width, ThumbnailCapturePolicy.MaxCaptureWidth);
        int captureHeight = Math.Min(bounds.Height, ThumbnailCapturePolicy.MaxCaptureHeight);
        (int targetWidth, int targetHeight) = ThumbnailCapturePolicy.ComputeThumbnailSize(bounds.Width, bounds.Height);

        return CaptureCore(windowHandle, captureWidth, captureHeight, targetWidth, targetHeight);
    }

    private BitmapSource? CaptureCore(
        IntPtr windowHandle,
        int captureWidth,
        int captureHeight,
        int targetWidth,
        int targetHeight)
    {
        IntPtr screenDc = IntPtr.Zero;
        IntPtr captureDc = IntPtr.Zero;
        IntPtr captureBitmap = IntPtr.Zero;
        IntPtr captureOldBitmap = IntPtr.Zero;
        IntPtr thumbDc = IntPtr.Zero;
        IntPtr thumbBitmap = IntPtr.Zero;
        IntPtr thumbOldBitmap = IntPtr.Zero;

        try
        {
            screenDc = Win32.GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                return null;
            }

            captureDc = Win32.CreateCompatibleDC(screenDc);
            if (captureDc == IntPtr.Zero)
            {
                return null;
            }

            captureBitmap = CreateTopDownBitmap(screenDc, captureWidth, captureHeight, out IntPtr captureBits);
            if (captureBitmap == IntPtr.Zero || captureBits == IntPtr.Zero)
            {
                return null;
            }

            captureOldBitmap = Win32.SelectObject(captureDc, captureBitmap);

            if (!Win32.PrintWindow(windowHandle, captureDc, Win32.PW_RENDERFULLCONTENT))
            {
                // 一部分老程序不认 PW_RENDERFULLCONTENT，退回最基础的调用方式再试一次
                if (!Win32.PrintWindow(windowHandle, captureDc, 0))
                {
                    return null;
                }
            }

            thumbDc = Win32.CreateCompatibleDC(screenDc);
            if (thumbDc == IntPtr.Zero)
            {
                return null;
            }

            thumbBitmap = CreateTopDownBitmap(screenDc, targetWidth, targetHeight, out IntPtr thumbBits);
            if (thumbBitmap == IntPtr.Zero || thumbBits == IntPtr.Zero)
            {
                return null;
            }

            thumbOldBitmap = Win32.SelectObject(thumbDc, thumbBitmap);

            Win32.SetStretchBltMode(thumbDc, Win32.HALFTONE);
            if (!Win32.StretchBlt(
                    thumbDc,
                    0,
                    0,
                    targetWidth,
                    targetHeight,
                    captureDc,
                    0,
                    0,
                    captureWidth,
                    captureHeight,
                    Win32.SRCCOPY))
            {
                return null;
            }

            // 纯色（几乎总是全黑）说明目标窗口没有真正把自己画出来，
            // 与其显示一块黑，不如让界面退回占位图
            if (IsBlank(thumbBits, targetWidth * targetHeight))
            {
                return null;
            }

            int stride = targetWidth * 4;
            BitmapSource source = BitmapSource.Create(
                targetWidth,
                targetHeight,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                thumbBits,
                stride * targetHeight,
                stride);

            // Freeze 之后才能安全地从后台线程转交给 UI 线程
            source.Freeze();
            return source;
        }
        catch (Exception ex)
        {
            _logger?.Warn($"截取窗口 0x{windowHandle.ToInt64():X} 缩略图失败：{ex.Message}");
            return null;
        }
        finally
        {
            if (thumbOldBitmap != IntPtr.Zero && thumbDc != IntPtr.Zero)
            {
                Win32.SelectObject(thumbDc, thumbOldBitmap);
            }

            if (thumbBitmap != IntPtr.Zero)
            {
                Win32.DeleteObject(thumbBitmap);
            }

            if (thumbDc != IntPtr.Zero)
            {
                Win32.DeleteDC(thumbDc);
            }

            if (captureOldBitmap != IntPtr.Zero && captureDc != IntPtr.Zero)
            {
                Win32.SelectObject(captureDc, captureOldBitmap);
            }

            if (captureBitmap != IntPtr.Zero)
            {
                Win32.DeleteObject(captureBitmap);
            }

            if (captureDc != IntPtr.Zero)
            {
                Win32.DeleteDC(captureDc);
            }

            if (screenDc != IntPtr.Zero)
            {
                Win32.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    /// <summary>
    /// 创建一张自上而下的 32 位 DIB。高度取负值是关键：这样第一行就是图像顶部，
    /// 与 WPF <c>BitmapSource</c> 的行序一致，否则缩略图会上下颠倒。
    /// </summary>
    private static IntPtr CreateTopDownBitmap(IntPtr referenceDc, int width, int height, out IntPtr bits)
    {
        var info = new BITMAPINFO
        {
            Header = new BITMAPINFOHEADER
            {
                BiSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
                BiWidth = width,
                BiHeight = -height,
                BiPlanes = 1,
                BiBitCount = 32,
                BiCompression = Win32.BI_RGB,
                BiSizeImage = (uint)(width * height * 4),
            },
        };

        return Win32.CreateDIBSection(referenceDc, ref info, Win32.DIB_RGB_COLORS, out bits, IntPtr.Zero, 0);
    }

    /// <summary>
    /// 判断位图是否为纯色。抽样检查即可：只需要在「有内容」和「一片黑」之间做区分，
    /// 逐像素比对没有意义。全透明与全黑都算空白。
    /// </summary>
    private static bool IsBlank(IntPtr bits, int pixelCount)
    {
        if (bits == IntPtr.Zero || pixelCount <= 0)
        {
            return true;
        }

        const int stride = 97; // 与像素总数互质的步长，抽样分布更均匀

        int first = System.Runtime.InteropServices.Marshal.ReadInt32(bits, 0);
        bool firstIsBlank = IsBlankPixel(first);

        for (int index = 0; index < pixelCount; index += stride)
        {
            int pixel = System.Runtime.InteropServices.Marshal.ReadInt32(bits, index * 4);
            if (!firstIsBlank)
            {
                return false;
            }

            if (pixel != first)
            {
                return false;
            }
        }

        return firstIsBlank;

        static bool IsBlankPixel(int pixel)
        {
            // 取低 24 位判断 RGB 是否为全 0，透明黑与纯黑都算空白
            return (pixel & 0x00FFFFFF) == 0;
        }
    }
}
