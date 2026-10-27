using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Capture;
using SkrinshotUZ.Native;

namespace SkrinshotUZ.Capture;

/// <summary>Ekran rasmini olish va kesish.</summary>
internal static class ScreenCapture
{
    /// <summary>
    /// Barcha monitorlarni bitta rasmga oladi (jismoniy piksellarda, masshtablashsiz).
    /// SRCCOPY | CAPTUREBLT — shaffof/qatlamli oynalar (menyular, tooltip'lar) ham tushadi.
    /// (Graphics.CopyFromScreen bu bayroqlar birikmasini qabul qilmaydi — shuning uchun to'g'ridan-to'g'ri BitBlt.)
    /// Sichqoncha kursori rasmga tushmaydi.
    /// </summary>
    /// <param name="layout">Monitorlar joylashuvi.</param>
    /// <returns>Kanvas o'lchamidagi rasm (chaqiruvchi dispose qiladi).</returns>
    public static Bitmap CaptureVirtualScreen(ScreenLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var bounds = layout.VirtualBounds;
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        try
        {
            using var g = Graphics.FromImage(bitmap);
            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new Win32Exception("Ekran kontekstini (GetDC) olib bo'lmadi.");
            }

            IntPtr targetDc = g.GetHdc();
            try
            {
                if (!NativeMethods.BitBlt(targetDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.X, bounds.Y, NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                g.ReleaseHdc(targetDc);
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>Kanvasdan sohani kesib oladi (piksel-piksel, masshtablashsiz). Natija — shaffofliksiz 24-bit rasm.</summary>
    /// <param name="source">Manba rasm.</param>
    /// <param name="rect">Soha (kanvas koordinatalarida, rasm ichida bo'lishi shart).</param>
    /// <returns>Yangi rasm (chaqiruvchi dispose qiladi).</returns>
    public static Bitmap Crop(Bitmap source, Rectangle rect)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (rect.Width <= 0 || rect.Height <= 0 || !new Rectangle(Point.Empty, source.Size).Contains(rect))
        {
            throw new ArgumentException($"Soha rasm chegarasidan tashqarida yoki bo'sh: {rect}.", nameof(rect));
        }

        return source.Clone(rect, PixelFormat.Format24bppRgb);
    }
}
