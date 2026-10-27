using System.Drawing;

namespace SkrinshotUZ.Core.Editing;

/// <summary>
/// Skrinshot ustidagi chizma. Koordinatalar — kanvas pikselarida (ekran rasmi bilan bir xil), shuning uchun
/// soha surilsa ham chizma ekrandagi joyida qoladi (Lightshot kabi).
/// </summary>
/// <param name="Color">Rang.</param>
/// <param name="Width">Chiziq qalinligi, px (matn uchun — shrift o'lchami, px).</param>
public abstract record Annotation(Color Color, float Width)
{
    /// <summary>Chizma egallaydigan soha (qalinlik va strelka uchi bilan) — qayta chizish uchun.</summary>
    public abstract Rectangle Bounds { get; }

    /// <summary>Nuqtalar to'plamini o'rab oluvchi to'rtburchak, <paramref name="pad"/> px kengaytirilgan.</summary>
    protected static Rectangle BoundsOf(IEnumerable<Point> points, float pad)
    {
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        foreach (var p in points)
        {
            left = Math.Min(left, p.X);
            top = Math.Min(top, p.Y);
            right = Math.Max(right, p.X);
            bottom = Math.Max(bottom, p.Y);
        }

        if (left == int.MaxValue)
        {
            return Rectangle.Empty;
        }

        int k = (int)Math.Ceiling(pad);
        return Rectangle.FromLTRB(left - k, top - k, right + k + 1, bottom + k + 1);
    }
}

/// <summary>Qalam yoki marker izi (erkin chiziq).</summary>
/// <param name="Points">Nuqtalar (kamida bitta; bitta bo'lsa — nuqta).</param>
/// <param name="Color">Rang.</param>
/// <param name="Width">Qalinlik, px.</param>
/// <param name="IsMarker">Marker (yarim shaffof, keng) — matnni belgilash uchun.</param>
public sealed record StrokeAnnotation(IReadOnlyList<Point> Points, Color Color, float Width, bool IsMarker)
    : Annotation(Color, Width)
{
    /// <summary>Marker shaffofligi (0–255).</summary>
    public const int MarkerAlpha = 110;

    /// <inheritdoc />
    public override Rectangle Bounds => BoundsOf(Points, (Width / 2) + 1);
}

/// <summary>To'g'ri chiziq yoki strelka.</summary>
/// <param name="Start">Boshi.</param>
/// <param name="End">Oxiri (strelka uchi shu yerda).</param>
/// <param name="Color">Rang.</param>
/// <param name="Width">Qalinlik, px.</param>
/// <param name="HasArrowHead">Strelka uchi bormi.</param>
public sealed record LineAnnotation(Point Start, Point End, Color Color, float Width, bool HasArrowHead)
    : Annotation(Color, Width)
{
    /// <inheritdoc />
    public override Rectangle Bounds =>
        BoundsOf(new[] { Start, End }, HasArrowHead ? ShapeGeometry.ArrowHeadLength(Width) + Width : (Width / 2) + 1);
}

/// <summary>To'rtburchak (ramka).</summary>
/// <param name="Rect">To'rtburchak.</param>
/// <param name="Color">Rang.</param>
/// <param name="Width">Qalinlik, px.</param>
public sealed record RectangleAnnotation(Rectangle Rect, Color Color, float Width) : Annotation(Color, Width)
{
    /// <inheritdoc />
    public override Rectangle Bounds => Rectangle.Inflate(Rect, (int)Math.Ceiling(Width) + 1, (int)Math.Ceiling(Width) + 1);
}

/// <summary>Matn.</summary>
/// <param name="Location">Chap-yuqori nuqta.</param>
/// <param name="Text">Matn (bir necha qator bo'lishi mumkin).</param>
/// <param name="Color">Rang.</param>
/// <param name="Width">Shrift o'lchami, px.</param>
/// <param name="Size">O'lchab olingan matn o'lchami, px (Windows tomonida o'lchanadi).</param>
public sealed record TextAnnotation(Point Location, string Text, Color Color, float Width, Size Size) : Annotation(Color, Width)
{
    /// <inheritdoc />
    public override Rectangle Bounds => Rectangle.Inflate(new Rectangle(Location, Size), 2, 2);
}
