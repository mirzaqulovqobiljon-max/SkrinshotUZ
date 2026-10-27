using System.Drawing;

namespace SkrinshotUZ.Core.Editing;

/// <summary>Strelka uchi uchburchagi.</summary>
/// <param name="Tip">Uchi (chiziq oxiri).</param>
/// <param name="Left">Chap qanot.</param>
/// <param name="Right">O'ng qanot.</param>
/// <param name="Base">Asos markazi — chiziq shu yerda tugaydi (qalin chiziq uchdan chiqib turmasligi uchun).</param>
public readonly record struct ArrowHead(PointF Tip, PointF Left, PointF Right, PointF Base);

/// <summary>Shakllar geometriyasi (Shift cheklovlari, strelka uchi).</summary>
public static class ShapeGeometry
{
    /// <summary>Strelka uchining uzunligi qalinlikka qarab, px.</summary>
    /// <param name="width">Chiziq qalinligi, px.</param>
    /// <returns>Uzunlik.</returns>
    public static float ArrowHeadLength(float width) => Math.Max(12f, width * 4f);

    /// <summary>Ikki nuqtadan normallashtirilgan to'rtburchak (istalgan yo'nalishda sudrash).</summary>
    /// <param name="a">Birinchi nuqta.</param>
    /// <param name="b">Ikkinchi nuqta.</param>
    /// <returns>To'rtburchak.</returns>
    public static Rectangle RectFrom(Point a, Point b) =>
        Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    /// <summary>Shift: kvadrat — kichik tomon kattasiga tenglashtiriladi, sudrash yo'nalishi saqlanadi.</summary>
    /// <param name="anchor">Boshlang'ich nuqta.</param>
    /// <param name="p">Joriy nuqta.</param>
    /// <returns>Kvadrat.</returns>
    public static Rectangle SquareFrom(Point anchor, Point p)
    {
        int dx = p.X - anchor.X;
        int dy = p.Y - anchor.Y;
        int side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        var corner = new Point(anchor.X + (dx < 0 ? -side : side), anchor.Y + (dy < 0 ? -side : side));
        return RectFrom(anchor, corner);
    }

    /// <summary>Shift: chiziq yo'nalishini eng yaqin 45° ga (0°, 45°, 90°, …) tekislaydi, uzunlik saqlanadi.</summary>
    /// <param name="origin">Boshlang'ich nuqta.</param>
    /// <param name="p">Joriy nuqta.</param>
    /// <returns>Tekislangan nuqta.</returns>
    public static Point SnapAngle(Point origin, Point p)
    {
        int dx = p.X - origin.X;
        int dy = p.Y - origin.Y;
        if (dx == 0 && dy == 0)
        {
            return p;
        }

        // 8 yo'nalishdan biri: 0 = o'ngga, 1 = o'ng-past, 2 = pastga, … (ekranda Y pastga o'sadi).
        int sector = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
        sector = ((sector % 8) + 8) % 8;
        int sx = sector is 0 or 1 or 7 ? 1 : sector is 3 or 4 or 5 ? -1 : 0;
        int sy = sector is 1 or 2 or 3 ? 1 : sector is 5 or 6 or 7 ? -1 : 0;

        if (sx != 0 && sy != 0)
        {
            // Diagonal: butun piksellarda aniq 45° — katta proyeksiya bo'yicha (nuqta sichqonchaga yaqin qoladi).
            int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new Point(origin.X + (sx * d), origin.Y + (sy * d));
        }

        // Gorizontal yoki vertikal: faqat tegishli proyeksiya qoladi.
        return sx != 0 ? new Point(p.X, origin.Y) : new Point(origin.X, p.Y);
    }

    /// <summary>Strelka uchi. Juda qisqa strelkada uchi chiziqdan uzun bo'lmaydi.</summary>
    /// <param name="start">Boshi.</param>
    /// <param name="end">Oxiri (uchi).</param>
    /// <param name="width">Qalinlik, px.</param>
    /// <returns>Uchburchak.</returns>
    public static ArrowHead ArrowHeadFor(Point start, Point end, float width)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.001f)
        {
            return new ArrowHead(end, end, end, end);
        }

        float ux = dx / length;
        float uy = dy / length;
        float headLength = Math.Min(ArrowHeadLength(width), length);
        float halfWidth = headLength * 0.5f;

        var basePoint = new PointF(end.X - (ux * headLength), end.Y - (uy * headLength));
        var left = new PointF(basePoint.X + (uy * halfWidth), basePoint.Y - (ux * halfWidth));
        var right = new PointF(basePoint.X - (uy * halfWidth), basePoint.Y + (ux * halfWidth));
        return new ArrowHead(end, left, right, basePoint);
    }
}
