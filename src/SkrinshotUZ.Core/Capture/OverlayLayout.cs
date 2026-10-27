using System.Drawing;

namespace SkrinshotUZ.Core.Capture;

/// <summary>Overlay ustidagi yozuvlar joylashuvi (o'lcham belgisi, maslahat matni) — sof geometriya.</summary>
public static class OverlayLayout
{
    /// <summary>
    /// "1280 × 720" belgisining joyi: soha ustida, chap tomonda. Yuqorida joy bo'lmasa — soha ichida.
    /// Belgi doim monitor ichida qoladi (ikki monitor chegarasida ham kesilmaydi).
    /// </summary>
    /// <param name="selection">Soha (kanvas).</param>
    /// <param name="label">Belgi o'lchami, px.</param>
    /// <param name="monitor">Soha chap-yuqori burchagi joylashgan monitor (kanvas).</param>
    /// <param name="gap">Soha va belgi orasidagi masofa, px.</param>
    /// <returns>Belgining chap-yuqori nuqtasi.</returns>
    public static Point PlaceSizeLabel(Rectangle selection, Size label, Rectangle monitor, int gap)
    {
        int x = selection.Left;
        int y = selection.Top - gap - label.Height;
        if (y < monitor.Top)
        {
            y = selection.Top + gap;
            x = selection.Left + gap;
        }

        x = Math.Clamp(x, monitor.Left, Math.Max(monitor.Left, monitor.Right - label.Width));
        y = Math.Clamp(y, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - label.Height));
        return new Point(x, y);
    }

    /// <summary>Maslahat matni: monitorning yuqori markazida.</summary>
    /// <param name="hint">Matn bloki o'lchami.</param>
    /// <param name="monitor">Monitor (kanvas).</param>
    /// <param name="marginTop">Yuqoridan masofa, px.</param>
    /// <returns>Chap-yuqori nuqta.</returns>
    public static Point PlaceHint(Size hint, Rectangle monitor, int marginTop) =>
        new(monitor.Left + Math.Max(0, (monitor.Width - hint.Width) / 2), monitor.Top + marginTop);

    /// <summary>"1280 × 720" ko'rinishidagi matn (doim bir xil, madaniyatga bog'liq emas).</summary>
    /// <param name="size">O'lcham.</param>
    /// <returns>Matn.</returns>
    public static string SizeText(Size size) =>
        size.Width.ToString(System.Globalization.CultureInfo.InvariantCulture) + " × " +
        size.Height.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
