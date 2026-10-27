using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
/// Ikonkalar: imkon qadar Windows'ning o'z ikonka shrifti — Segoe MDL2 Assets (Windows 10/11 dagi ikonkalar bilan
/// bir xil uslub, har qanday DPI'da tiniq). Shriftda yo'q belgilar (chiziq, qiya strelka) — shu uslubdagi ingichka
/// vektor chizma. Shrift o'rnatilmagan bo'lsa — hammasi vektor bilan chiziladi.
/// Glif kodlari: https://learn.microsoft.com/windows/apps/design/style/segoe-ui-symbol-font
/// </summary>
internal static class IconPainter
{
    /// <summary>Ikonka shrifti nomi.</summary>
    public const string GlyphFontName = "Segoe MDL2 Assets";

    private static readonly Color Ink = Color.FromArgb(50, 50, 50);
    private static readonly Lazy<bool> GlyphFontInstalled = new(() =>
    {
        using var fonts = new InstalledFontCollection();
        return fonts.Families.Any(f => string.Equals(f.Name, GlyphFontName, StringComparison.OrdinalIgnoreCase));
    });

    /// <summary>Ikonka shrifti mavjudmi.</summary>
    public static bool HasGlyphFont => GlyphFontInstalled.Value;

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

    /// <summary>Segoe MDL2 Assets glifi (yo'q bo'lsa — <c>null</c>, vektor chiziladi).</summary>
    public static char? GlyphFor(ToolbarIcon icon) => icon switch
    {
        ToolbarIcon.Pen => '',        // Edit
        ToolbarIcon.Rectangle => '',  // Checkbox (bo'sh kvadrat)
        ToolbarIcon.Marker => '',     // Highlight
        ToolbarIcon.Text => '',       // Font
        ToolbarIcon.Undo => '',
        ToolbarIcon.Redo => '',
        ToolbarIcon.Copy => '',
        ToolbarIcon.Save => '',
        ToolbarIcon.Print => '',
        ToolbarIcon.Close => '',      // Cancel
        _ => null,                          // Line, Arrow — shriftda yo'q
    };

    /// <summary>Ikonka (chaqiruvchi dispose qiladi).</summary>
    public static Bitmap Draw(ToolbarIcon icon, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        if (HasGlyphFont && GlyphFor(icon) is char glyph)
        {
            DrawGlyph(g, glyph, size);
        }
        else
        {
            DrawVector(g, icon, size);
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
        float m = size * 0.16f;
        using var brush = new SolidBrush(color);
        using var border = new Pen(Color.FromArgb(110, 0, 0, 0), Math.Max(1f, size / 18f));
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
        using var pen = new Pen(Ink, Math.Min(lineWidth, height * 0.7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, height * 0.25f, height / 2f, width - (height * 0.25f), height / 2f);
        return bmp;
    }

    /// <summary>Glifni yo'l (path) sifatida chizadi va aniq markazga qo'yadi — shriftning ichki chegaralari halaqit bermaydi.</summary>
    private static void DrawGlyph(Graphics g, char glyph, int size)
    {
        using var family = new FontFamily(GlyphFontName);
        using var path = new GraphicsPath();
        path.AddString(glyph.ToString(), family, (int)FontStyle.Regular, size * 0.82f, PointF.Empty, StringFormat.GenericTypographic);
        var b = path.GetBounds();
        using (var m = new Matrix())
        {
            m.Translate(((size - b.Width) / 2f) - b.X, ((size - b.Height) / 2f) - b.Y);
            path.Transform(m);
        }

        using var brush = new SolidBrush(Ink);
        g.FillPath(brush, path);
    }

    /// <summary>Ingichka chiziqli vektor ikonkalar (MDL2 uslubiga yaqin).</summary>
    private static void DrawVector(Graphics g, ToolbarIcon icon, int size)
    {
        float s = size;
        float w = Math.Max(1.2f, s / 13f);
        using var pen = new Pen(Ink, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        PointF P(float x, float y) => new(x * s, y * s);

        switch (icon)
        {
            case ToolbarIcon.Line:
                g.DrawLine(pen, P(0.2f, 0.8f), P(0.8f, 0.2f));
                break;
            case ToolbarIcon.Arrow:
                g.DrawLine(pen, P(0.2f, 0.8f), P(0.8f, 0.2f));
                g.DrawLines(pen, new[] { P(0.46f, 0.2f), P(0.8f, 0.2f), P(0.8f, 0.54f) });
                break;
            case ToolbarIcon.Pen:
                g.DrawPolygon(pen, new[] { P(0.66f, 0.16f), P(0.84f, 0.34f), P(0.36f, 0.82f), P(0.16f, 0.84f), P(0.18f, 0.64f) });
                break;
            case ToolbarIcon.Rectangle:
                g.DrawRectangle(pen, 0.2f * s, 0.2f * s, 0.6f * s, 0.6f * s);
                break;
            case ToolbarIcon.Marker:
                g.DrawPolygon(pen, new[] { P(0.6f, 0.14f), P(0.8f, 0.32f), P(0.48f, 0.64f), P(0.28f, 0.46f) });
                g.DrawLine(pen, P(0.16f, 0.84f), P(0.84f, 0.84f));
                break;
            case ToolbarIcon.Text:
                g.DrawLines(pen, new[] { P(0.22f, 0.82f), P(0.5f, 0.18f), P(0.78f, 0.82f) });
                g.DrawLine(pen, P(0.33f, 0.58f), P(0.67f, 0.58f));
                break;
            case ToolbarIcon.Undo:
            case ToolbarIcon.Redo:
                var state = g.Save();
                if (icon == ToolbarIcon.Redo)
                {
                    g.TranslateTransform(s, 0);
                    g.ScaleTransform(-1, 1);
                }

                g.DrawArc(pen, 0.24f * s, 0.3f * s, 0.56f * s, 0.46f * s, 180, 200);
                g.DrawLines(pen, new[] { P(0.12f, 0.34f), P(0.24f, 0.53f), P(0.42f, 0.42f) });
                g.Restore(state);
                break;
            case ToolbarIcon.Copy:
                g.DrawRectangle(pen, 0.32f * s, 0.32f * s, 0.5f * s, 0.52f * s);
                g.DrawLines(pen, new[] { P(0.18f, 0.68f), P(0.18f, 0.16f), P(0.62f, 0.16f) });
                break;
            case ToolbarIcon.Save:
                g.DrawRectangle(pen, 0.18f * s, 0.18f * s, 0.64f * s, 0.64f * s);
                g.DrawRectangle(pen, 0.32f * s, 0.18f * s, 0.36f * s, 0.2f * s);
                g.DrawRectangle(pen, 0.3f * s, 0.54f * s, 0.4f * s, 0.28f * s);
                break;
            case ToolbarIcon.Print:
                g.DrawRectangle(pen, 0.3f * s, 0.14f * s, 0.4f * s, 0.22f * s);
                g.DrawRectangle(pen, 0.14f * s, 0.36f * s, 0.72f * s, 0.3f * s);
                g.DrawRectangle(pen, 0.3f * s, 0.56f * s, 0.4f * s, 0.3f * s);
                break;
            case ToolbarIcon.Close:
                g.DrawLine(pen, P(0.26f, 0.26f), P(0.74f, 0.74f));
                g.DrawLine(pen, P(0.74f, 0.26f), P(0.26f, 0.74f));
                break;
        }
    }
}
