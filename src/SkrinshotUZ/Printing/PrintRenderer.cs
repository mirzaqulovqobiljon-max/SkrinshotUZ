using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Printing;

/// <summary>
/// Bitta varaqni istalgan <see cref="Graphics"/>'ga millimetrlarda chizadi: printerga, oldindan ko'rishga va
/// testdagi 300 DPI Bitmap'ga — hammasi bir xil kod. Joylashuvni o'zi hisoblamaydi (<see cref="PrintPlanner"/>).
/// </summary>
internal static class PrintRenderer
{
    /// <summary>Yozuv shrifti balandligi, mm (~10 pt).</summary>
    public const float CaptionFontMm = 3.5f;

    /// <summary>Kesish chizig'i qalinligi, mm.</summary>
    public const float CutLineWidthMm = 0.3f;

    private static readonly ColorMatrix GrayMatrix = new(new[]
    {
        // ITU-R BT.601 yorqinlik: rangli rasm oq-qora printerda ham tiniq chiqadi.
        new[] { 0.299f, 0.299f, 0.299f, 0, 0 },
        new[] { 0.587f, 0.587f, 0.587f, 0, 0 },
        new[] { 0.114f, 0.114f, 0.114f, 0, 0 },
        new[] { 0f, 0, 0, 1, 0 },
        new[] { 0f, 0, 0, 0, 1 },
    });

    /// <summary>Varaqni chizadi.</summary>
    /// <param name="g">Graphics (printer, ko'rish yoki Bitmap). Uning (0,0) nuqtasi varaqning (<paramref name="originMm"/>) nuqtasida turadi deb hisoblanadi.</param>
    /// <param name="page">Varaq.</param>
    /// <param name="images">Rasmlar (<see cref="PlacedImage.ImageIndex"/> bo'yicha).</param>
    /// <param name="originMm">Graphics boshlanish nuqtasining varaqdagi joyi, mm: printerda — bosilmaydigan chet (HardMarginX/Y), ko'rishda — (0,0).</param>
    /// <param name="grayscale">Oq-qora.</param>
    /// <param name="showMargins">Maydonlar chegarasini ko'rsatish (faqat oldindan ko'rishda).</param>
    public static void DrawPage(Graphics g, PrintPage page, IReadOnlyList<Image> images, PointF originMm, bool grayscale, bool showMargins)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(images);

        var state = g.Save();
        try
        {
            g.PageUnit = GraphicsUnit.Millimeter;
            g.PageScale = 1f;
            g.TranslateTransform(-originMm.X, -originMm.Y);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (showMargins)
            {
                DrawMarginGuides(g, page);
            }

            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY); // rasm chetida oq "halo" bo'lmasin (o'lcham aniq chiqadi)
            if (grayscale)
            {
                attributes.SetColorMatrix(GrayMatrix);
            }

            foreach (var placed in page.Images)
            {
                var image = images[placed.ImageIndex];
                var dest = ToRectF(placed.Dest);
                var src = placed.Source;
                g.DrawImage(image, ToPoints(dest), new RectangleF(src.X, src.Y, src.Width, src.Height), GraphicsUnit.Pixel, attributes);
            }

            if (page.CutLineYMm is double y)
            {
                DrawCutLine(g, page, (float)y);
            }

            if (page.Captions.Count > 0)
            {
                DrawCaptions(g, page.Captions);
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static PointF[] ToPoints(RectangleF r) => new[] { r.Location, new PointF(r.Right, r.Top), new PointF(r.Left, r.Bottom) };

    private static RectangleF ToRectF(RectMm r) => new((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height);

    private static void DrawCutLine(Graphics g, PrintPage page, float y)
    {
        using var pen = new Pen(Color.FromArgb(120, 120, 120), CutLineWidthMm) { DashPattern = new[] { 6f, 4f } };
        float start = 9f; // qaychi belgisi uchun joy
        g.DrawLine(pen, start, y, (float)page.Size.Width, y);

        // Qaychi (✂) — shrift bo'lmasa ham chiziq baribir bor.
        using var font = new Font("Segoe UI Symbol", 5f, GraphicsUnit.Millimeter);
        using var brush = new SolidBrush(Color.FromArgb(120, 120, 120));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("✂", font, brush, new RectangleF(1f, y - 4f, 8f, 8f), format);
    }

    private static void DrawCaptions(Graphics g, IReadOnlyList<PlacedCaption> captions)
    {
        using var font = new Font("Segoe UI", CaptionFontMm, GraphicsUnit.Millimeter);
        using var brush = new SolidBrush(Color.FromArgb(40, 40, 40));
        using var format = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        foreach (var c in captions)
        {
            g.DrawString(c.Text, font, brush, ToRectF(c.Rect), format);
        }
    }

    private static void DrawMarginGuides(Graphics g, PrintPage page)
    {
        using var pen = new Pen(Color.FromArgb(150, 180, 230), 0.3f) { DashPattern = new[] { 4f, 3f } };
        foreach (var area in page.Images.Select(i => i.Area).Distinct())
        {
            var r = ToRectF(area);
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        }
    }
}
