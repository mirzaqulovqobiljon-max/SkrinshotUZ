using System.Drawing;
using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Core.Editing;

/// <summary>Palitradagi rang.</summary>
/// <param name="Color">Rang.</param>
/// <param name="Name">O'zbekcha nomi.</param>
public sealed record PaletteEntry(Color Color, string Name);

/// <summary>Asboblar paneli uchun ma'lumotlar: ranglar, nomlar, klaviatura qisqartmalari, panellar joylashuvi.</summary>
public static class EditorUi
{
    /// <summary>Asosiy ranglar (maktabda ko'p ishlatiladiganlari; qolganlari — "Boshqa rang…").</summary>
    public static IReadOnlyList<PaletteEntry> Palette { get; } = new[]
    {
        new PaletteEntry(EditorController.DefaultColor, Strings.Color_Red),
        new PaletteEntry(Color.FromArgb(255, 200, 0), Strings.Color_Yellow),
        new PaletteEntry(Color.FromArgb(30, 160, 60), Strings.Color_Green),
        new PaletteEntry(Color.FromArgb(0, 110, 220), Strings.Color_Blue),
        new PaletteEntry(Color.FromArgb(255, 130, 0), Strings.Color_Orange),
        new PaletteEntry(Color.FromArgb(140, 60, 200), Strings.Color_Purple),
        new PaletteEntry(Color.FromArgb(20, 20, 20), Strings.Color_Black),
        new PaletteEntry(Color.FromArgb(255, 255, 255), Strings.Color_White),
    };

    /// <summary>Asboblar tartibi (paneldagi kabi).</summary>
    public static IReadOnlyList<EditorTool> Tools { get; } = new[]
    {
        EditorTool.Pen, EditorTool.Line, EditorTool.Arrow, EditorTool.Rectangle, EditorTool.Marker, EditorTool.Text,
    };

    /// <summary>Asbob nomi va klaviatura qisqartmasi (tooltip uchun).</summary>
    /// <param name="tool">Asbob.</param>
    /// <returns>Matn.</returns>
    public static string ToolName(EditorTool tool) => tool switch
    {
        EditorTool.Pen => Strings.Tool_Pen,
        EditorTool.Line => Strings.Tool_Line,
        EditorTool.Arrow => Strings.Tool_Arrow,
        EditorTool.Rectangle => Strings.Tool_Rectangle,
        EditorTool.Marker => Strings.Tool_Marker,
        EditorTool.Text => Strings.Tool_Text,
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
    };

    /// <summary>Qalinlik nomi.</summary>
    /// <param name="thickness">Daraja.</param>
    /// <returns>Matn.</returns>
    public static string ThicknessName(Thickness thickness) => thickness switch
    {
        Thickness.Thin => Strings.Thickness_Thin,
        Thickness.Medium => Strings.Thickness_Medium,
        Thickness.Thick => Strings.Thickness_Thick,
        Thickness.ExtraThick => Strings.Thickness_ExtraThick,
        _ => throw new ArgumentOutOfRangeException(nameof(thickness), thickness, null),
    };

    /// <summary>
    /// Bir harfli qisqartma (modifikatorsiz): P — qalam, L — chiziq, A — strelka, R — to'rtburchak, M — marker, T — matn.
    /// Lotin harflari — o'zbek lotin klaviaturasida ham shu joyda.
    /// </summary>
    /// <param name="key">Harf (katta yoki kichik).</param>
    /// <returns>Asbob yoki <c>null</c>.</returns>
    public static EditorTool? ToolForKey(char key) => char.ToUpperInvariant(key) switch
    {
        'P' => EditorTool.Pen,
        'L' => EditorTool.Line,
        'A' => EditorTool.Arrow,
        'R' => EditorTool.Rectangle,
        'M' => EditorTool.Marker,
        'T' => EditorTool.Text,
        _ => null,
    };

    /// <summary>
    /// Tik asboblar paneli: sohaning o'ng tomonida; joy bo'lmasa — chapda; u yerda ham bo'lmasa — soha ichida o'ngda.
    /// Panel doim monitor ichida qoladi.
    /// </summary>
    /// <param name="selection">Soha (kanvas).</param>
    /// <param name="bar">Panel o'lchami.</param>
    /// <param name="monitor">Monitor (kanvas).</param>
    /// <param name="gap">Oraliq, px.</param>
    /// <returns>Panel chap-yuqori nuqtasi.</returns>
    public static Point PlaceToolsBar(Rectangle selection, Size bar, Rectangle monitor, int gap)
    {
        int x = selection.Right + gap;
        if (x + bar.Width > monitor.Right)
        {
            x = selection.Left - gap - bar.Width;
            if (x < monitor.Left)
            {
                x = selection.Right - gap - bar.Width;
            }
        }

        int y = selection.Top;
        return ClampInto(new Point(x, y), bar, monitor);
    }

    /// <summary>
    /// Gorizontal amallar paneli: sohaning ostida, o'ng tomonga tekislangan; joy bo'lmasa — ustida;
    /// u yerda ham bo'lmasa — soha ichida pastda.
    /// </summary>
    /// <param name="selection">Soha (kanvas).</param>
    /// <param name="bar">Panel o'lchami.</param>
    /// <param name="monitor">Monitor (kanvas).</param>
    /// <param name="gap">Oraliq, px.</param>
    /// <returns>Panel chap-yuqori nuqtasi.</returns>
    public static Point PlaceActionsBar(Rectangle selection, Size bar, Rectangle monitor, int gap)
    {
        int y = selection.Bottom + gap;
        if (y + bar.Height > monitor.Bottom)
        {
            y = selection.Top - gap - bar.Height;
            if (y < monitor.Top)
            {
                y = selection.Bottom - gap - bar.Height;
            }
        }

        int x = selection.Right - bar.Width;
        return ClampInto(new Point(x, y), bar, monitor);
    }

    private static Point ClampInto(Point p, Size size, Rectangle area) => new(
        Math.Clamp(p.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
        Math.Clamp(p.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
}
