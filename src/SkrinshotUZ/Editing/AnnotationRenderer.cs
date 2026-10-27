using System.Drawing.Drawing2D;
using SkrinshotUZ.Core.Editing;

namespace SkrinshotUZ.Editing;

/// <summary>
/// Chizmalarni Graphics'ga chizadi. Bir xil kod ham ekranda (overlay), ham natija rasmida ishlatiladi —
/// ekranda ko'rgan narsangiz aynan buferga/faylga tushadi.
/// </summary>
internal static class AnnotationRenderer
{
    /// <summary>Matn uchun shrift.</summary>
    public const string FontFamily = "Segoe UI";

    /// <summary>Matn chizish/o'lchash bayroqlari (o'lchash va chizish bir xil bo'lishi shart).</summary>
    public const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix;

    /// <summary>Matn shrifti (chaqiruvchi dispose qiladi).</summary>
    public static Font CreateTextFont(float pixels) => new(FontFamily, pixels, FontStyle.Bold, GraphicsUnit.Pixel);

    /// <summary>Chizmani chizadi.</summary>
    /// <param name="g">Graphics.</param>
    /// <param name="annotation">Chizma.</param>
    /// <param name="offset">Kanvas koordinatasidan ayiriladigan siljish (natija rasmida — soha chap-yuqori burchagi).</param>
    public static void Draw(Graphics g, Annotation annotation, Point offset)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(annotation);

        var oldSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            switch (annotation)
            {
                case StrokeAnnotation s:
                    DrawStroke(g, s, offset);
                    break;
                case LineAnnotation l:
                    DrawLine(g, l, offset);
                    break;
                case RectangleAnnotation r:
                    DrawRectangle(g, r, offset);
                    break;
                case TextAnnotation t:
                    DrawText(g, t, offset);
                    break;
            }
        }
        finally
        {
            g.SmoothingMode = oldSmoothing;
        }
    }

    private static PointF Shift(Point p, Point offset) => new(p.X - offset.X, p.Y - offset.Y);

    private static PointF Shift(PointF p, Point offset) => new(p.X - offset.X, p.Y - offset.Y);

    private static Pen RoundPen(Color color, float width) => new(color, width)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round,
    };

    private static void DrawStroke(Graphics g, StrokeAnnotation s, Point offset)
    {
        if (s.Points.Count == 0)
        {
            return;
        }

        var color = s.IsMarker ? Color.FromArgb(StrokeAnnotation.MarkerAlpha, s.Color) : s.Color;
        var points = s.Points.Select(p => Shift(p, offset)).ToArray();
        if (points.Distinct().Count() == 1)
        {
            // Bitta bosish — doira (nuqta).
            using var brush = new SolidBrush(color);
            float r = s.Width / 2;
            g.FillEllipse(brush, points[0].X - r, points[0].Y - r, s.Width, s.Width);
            return;
        }

        // Bitta yo'l sifatida chiziladi — marker o'z ustiga tushganda qorayib ketmaydi.
        using var pen = RoundPen(color, s.Width);
        g.DrawLines(pen, points);
    }

    private static void DrawLine(Graphics g, LineAnnotation l, Point offset)
    {
        using var pen = RoundPen(l.Color, l.Width);
        if (!l.HasArrowHead)
        {
            g.DrawLine(pen, Shift(l.Start, offset), Shift(l.End, offset));
            return;
        }

        var head = ShapeGeometry.ArrowHeadFor(l.Start, l.End, l.Width);

        // Chiziq uch asosida tugaydi — qalin chiziq uchli qismdan chiqib turmaydi.
        pen.EndCap = LineCap.Flat;
        g.DrawLine(pen, Shift(l.Start, offset), Shift(head.Base, offset));
        using var brush = new SolidBrush(l.Color);
        g.FillPolygon(brush, new[] { Shift(head.Tip, offset), Shift(head.Left, offset), Shift(head.Right, offset) });
    }

    private static void DrawRectangle(Graphics g, RectangleAnnotation r, Point offset)
    {
        using var pen = new Pen(r.Color, r.Width) { LineJoin = LineJoin.Miter };
        g.DrawRectangle(pen, r.Rect.X - offset.X, r.Rect.Y - offset.Y, r.Rect.Width, r.Rect.Height);
    }

    private static void DrawText(Graphics g, TextAnnotation t, Point offset)
    {
        using var font = CreateTextFont(t.Width);
        var location = new Point(t.Location.X - offset.X, t.Location.Y - offset.Y);
        TextRenderer.DrawText(g, t.Text.Replace("\n", Environment.NewLine, StringComparison.Ordinal), font, location, t.Color, TextFlags);
    }
}

/// <summary>Natija rasmi: kesilgan soha + barcha chizmalar.</summary>
internal static class ImageComposer
{
    /// <summary>Soha rasmini chizmalar bilan birga yaratadi.</summary>
    /// <param name="screen">Ekran rasmi (kanvas).</param>
    /// <param name="selection">Soha (kanvas).</param>
    /// <param name="annotations">Chizmalar (kanvas koordinatalarida).</param>
    /// <returns>Yangi rasm (chaqiruvchi dispose qiladi).</returns>
    public static Bitmap Compose(Bitmap screen, Rectangle selection, IEnumerable<Annotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var result = Capture.ScreenCapture.Crop(screen, selection);
        try
        {
            using var g = Graphics.FromImage(result);
            foreach (var a in annotations)
            {
                AnnotationRenderer.Draw(g, a, selection.Location);
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }
}
