using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SkrinshotUZ.Core.Editing;

namespace SkrinshotUZ.Editing;

/// <summary>Panel tugmalari uchun ikonkalar.</summary>
internal enum ToolbarIcon
{
    Pen,
    Line,
    Arrow,
    Rectangle,
    Marker,
    Text,
    Undo,
    Redo,
    Copy,
    Save,
    Print,
    Close,
}

/// <summary>
/// Ikonkalarni kod bilan, kerakli o'lchamda chizadi (vektor) — har qanday DPI'da tiniq, alohida rasm fayllari kerak emas.
/// </summary>
internal static class IconPainter
{
    private static readonly Color Ink = Color.FromArgb(45, 45, 45);

    /// <summary>Asbob ikonkasi.</summary>
    public static ToolbarIcon ForTool(EditorTool tool) => tool switch
    {
        EditorTool.Pen => ToolbarIcon.Pen,
        EditorTool.Line => ToolbarIcon.Line,
        EditorTool.Arrow => ToolbarIcon.Arrow,
        EditorTool.Rectangle => ToolbarIcon.Rectangle,
        EditorTool.Marker => ToolbarIcon.Marker,
        EditorTool.Text => ToolbarIcon.Text,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
    };

    /// <summary>Ikonka (chaqiruvchi dispose qiladi).</summary>
    public static Bitmap Draw(ToolbarIcon icon, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float s = size;
        float w = Math.Max(1.5f, s / 12f);
        using var pen = new Pen(Ink, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(Ink);

        PointF P(float x, float y) => new(x * s, y * s);

        switch (icon)
        {
            case ToolbarIcon.Pen:
                // Qalam: qiya tana + uchi.
                g.DrawPolygon(pen, new[] { P(0.68f, 0.14f), P(0.86f, 0.32f), P(0.36f, 0.82f), P(0.18f, 0.64f) });
                g.FillPolygon(brush, new[] { P(0.18f, 0.64f), P(0.36f, 0.82f), P(0.12f, 0.88f) });
                break;
            case ToolbarIcon.Line:
                g.DrawLine(pen, P(0.18f, 0.82f), P(0.82f, 0.18f));
                break;
            case ToolbarIcon.Arrow:
                g.DrawLine(pen, P(0.18f, 0.82f), P(0.72f, 0.28f));
                g.FillPolygon(brush, new[] { P(0.86f, 0.14f), P(0.80f, 0.46f), P(0.54f, 0.20f) });
                break;
            case ToolbarIcon.Rectangle:
                g.DrawRectangle(pen, 0.16f * s, 0.26f * s, 0.68f * s, 0.48f * s);
                break;
            case ToolbarIcon.Marker:
                using (var marker = new SolidBrush(Color.FromArgb(255, 210, 0)))
                {
                    g.FillRectangle(marker, 0.10f * s, 0.56f * s, 0.80f * s, 0.22f * s);
                }

                g.DrawPolygon(pen, new[] { P(0.62f, 0.12f), P(0.82f, 0.30f), P(0.50f, 0.62f), P(0.30f, 0.44f) });
                break;
            case ToolbarIcon.Text:
                // Shaffof fonda GDI (TextRenderer) alfa kanalini yozmaydi — shuning uchun GDI+ DrawString.
                using (var font = new Font("Segoe UI", s * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.DrawString("A", font, brush, new RectangleF(0, 0, s, s), format);
                }

                break;
            case ToolbarIcon.Undo:
            case ToolbarIcon.Redo:
                var state = g.Save();
                if (icon == ToolbarIcon.Redo)
                {
                    g.TranslateTransform(s, 0);
                    g.ScaleTransform(-1, 1);
                }

                g.DrawArc(pen, 0.22f * s, 0.30f * s, 0.58f * s, 0.46f * s, 180, 200);
                g.FillPolygon(brush, new[] { P(0.08f, 0.50f), P(0.38f, 0.50f), P(0.22f, 0.26f) });
                g.Restore(state);
                break;
            case ToolbarIcon.Copy:
                g.DrawRectangle(pen, 0.30f * s, 0.30f * s, 0.52f * s, 0.56f * s);
                g.DrawLines(pen, new[] { P(0.18f, 0.70f), P(0.18f, 0.14f), P(0.62f, 0.14f) });
                break;
            case ToolbarIcon.Save:
                g.DrawRectangle(pen, 0.16f * s, 0.16f * s, 0.68f * s, 0.68f * s);
                g.DrawRectangle(pen, 0.32f * s, 0.16f * s, 0.36f * s, 0.22f * s);
                g.DrawRectangle(pen, 0.28f * s, 0.54f * s, 0.44f * s, 0.30f * s);
                break;
            case ToolbarIcon.Print:
                g.DrawRectangle(pen, 0.30f * s, 0.12f * s, 0.40f * s, 0.24f * s);
                g.DrawRectangle(pen, 0.12f * s, 0.36f * s, 0.76f * s, 0.32f * s);
                g.DrawRectangle(pen, 0.30f * s, 0.58f * s, 0.40f * s, 0.30f * s);
                break;
            case ToolbarIcon.Close:
                g.DrawLine(pen, P(0.24f, 0.24f), P(0.76f, 0.76f));
                g.DrawLine(pen, P(0.76f, 0.24f), P(0.24f, 0.76f));
                break;
        }

        return bmp;
    }

    /// <summary>Rang namunasi (doira).</summary>
    public static Bitmap Swatch(Color color, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        float m = size * 0.14f;
        using var brush = new SolidBrush(color);
        using var border = new Pen(Color.FromArgb(120, 0, 0, 0), Math.Max(1f, size / 20f));
        g.FillEllipse(brush, m, m, size - (2 * m), size - (2 * m));
        g.DrawEllipse(border, m, m, size - (2 * m), size - (2 * m));
        return bmp;
    }

    /// <summary>Qalinlik namunasi (gorizontal chiziq).</summary>
    public static Bitmap ThicknessSample(float lineWidth, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var pen = new Pen(Ink, Math.Min(lineWidth, height * 0.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, height * 0.3f, height / 2f, width - (height * 0.3f), height / 2f);
        return bmp;
    }
}
