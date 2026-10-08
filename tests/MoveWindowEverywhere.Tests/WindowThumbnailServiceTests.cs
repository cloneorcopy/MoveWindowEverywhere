using System.Runtime.InteropServices;
using System.Windows.Media;
using MoveWindowEverywhere.Services;
using Xunit;

namespace MoveWindowEverywhere.Tests;

public sealed class WindowThumbnailServiceTests
{
    [Fact]
    public void Gdi32位截图的Alpha为零时缩略图仍应不透明()
    {
        // A 32-bit BI_RGB DIB stores BGR plus one unused byte, often zero.
        // WPF Bgra32 would interpret the last byte as alpha=0 and hide the image.
        byte[] gdiPixels = { 0x20, 0x60, 0xE0, 0x00, 0x90, 0x40, 0x10, 0x00 };
        IntPtr bits = Marshal.AllocHGlobal(gdiPixels.Length);
        try
        {
            Marshal.Copy(gdiPixels, 0, bits, gdiPixels.Length);
            var bitmap = WindowThumbnailService.CreateOpaqueThumbnail(bits, 2, 1);

            Assert.True(bitmap.IsFrozen);
            Assert.Equal(PixelFormats.Bgr32, bitmap.Format);
            var actual = new byte[8];
            bitmap.CopyPixels(actual, 8, 0);
            Assert.Equal(gdiPixels, actual);
        }
        finally
        {
            Marshal.FreeHGlobal(bits);
        }
    }
}
