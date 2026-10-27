using System.Drawing.Imaging;
using SkrinshotUZ.Core.Capture;

namespace SkrinshotUZ.Capture;

/// <summary>Ekran rasmini olish va kesish.</summary>
internal static class ScreenCapture
{
    /// <summary>
    /// Barcha monitorlarni bitta rasmga oladi (jismoniy piksellarda, masshtablashsiz).
    /// CAPTUREBLT — shaffof/qatlamli oynalar (masalan, menyular, tooltip'lar) ham tushadi.
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
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt);
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
