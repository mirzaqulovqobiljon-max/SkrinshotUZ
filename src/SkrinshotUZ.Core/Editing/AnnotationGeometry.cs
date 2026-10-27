using System.Drawing;

namespace SkrinshotUZ.Core.Editing;

/// <summary>Chizmaning qaysi qismi ushlandi.</summary>
public enum AnnotationHandle
{
    /// <summary>Hech qaysi.</summary>
    None,

    /// <summary>Chizmaning o'zi — siljitish.</summary>
    Body,

    /// <summary>Chiziq/strelka boshi.</summary>
    Start,

    /// <summary>Chiziq/strelka oxiri (strelka uchi).</summary>
    End,

    /// <summary>To'rtburchak chap-yuqori burchagi.</summary>
    TopLeft,

    /// <summary>To'rtburchak o'ng-yuqori burchagi.</summary>
    TopRight,

    /// <summary>To'rtburchak o'ng-pastki burchagi.</summary>
    BottomRight,

    /// <summary>To'rtburchak chap-pastki burchagi.</summary>
    BottomLeft,
}

/// <summary>
/// Chizilgan shakllarni keyin tahrirlash: ushlash (hit-test), siljitish, uchlari/burchaklaridan cho'zish.
/// Hammasi o'zgarmas (immutable) — har bir amal yangi chizma qaytaradi (Ctrl+Z uchun eski holat saqlanadi).
/// </summary>
public static class AnnotationGeometry
{
    /// <summary>Tutqichlar (tanlangan chizmada ko'rsatiladi va sudraladi).</summary>
    /// <param name="a">Chizma.</param>
    /// <returns>Tutqich va uning nuqtasi.</returns>
    public static IReadOnlyList<(AnnotationHandle Handle, Point Point)> Handles(Annotation a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return a switch
        {
            LineAnnotation l => new[] { (AnnotationHandle.Start, l.Start), (AnnotationHandle.End, l.End) },
            RectangleAnnotation r => new[]
            {
                (AnnotationHandle.TopLeft, new Point(r.Rect.Left, r.Rect.Top)),
                (AnnotationHandle.TopRight, new Point(r.Rect.Right, r.Rect.Top)),
                (AnnotationHandle.BottomRight, new Point(r.Rect.Right, r.Rect.Bottom)),
                (AnnotationHandle.BottomLeft, new Point(r.Rect.Left, r.Rect.Bottom)),
            },

            // Erkin chiziq va matn — faqat siljitiladi.
            _ => Array.Empty<(AnnotationHandle, Point)>(),
        };
    }

    /// <summary>
    /// Nuqta chizmaning qaysi qismiga tushadi. Tutqichlar faqat <paramref name="withHandles"/> bo'lsa (tanlangan chizmada)
    /// tekshiriladi va tanadan ustun turadi. To'rtburchakning ichi "tana" emas — ichiga yangi shakl chizish mumkin bo'lsin.
    /// </summary>
    /// <param name="a">Chizma.</param>
    /// <param name="p">Nuqta.</param>
    /// <param name="tolerance">Ushlash radiusi, px.</param>
    /// <param name="withHandles">Tutqichlarni ham tekshirish.</param>
    /// <returns>Qism.</returns>
    public static AnnotationHandle HitTest(Annotation a, Point p, int tolerance, bool withHandles)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (withHandles)
        {
            // Eng yaqin tutqich (strelkaning ikki uchi yaqin bo'lsa ham to'g'risi tanlansin).
            AnnotationHandle best = AnnotationHandle.None;
            double bestDistance = double.MaxValue;
            foreach (var (handle, point) in Handles(a))
            {
                double d = Distance(p, point);
                if (d <= tolerance + 2 && d < bestDistance)
                {
                    best = handle;
                    bestDistance = d;
                }
            }

            if (best != AnnotationHandle.None)
            {
                return best;
            }
        }

        double reach = (a.Width / 2) + tolerance;
        bool body = a switch
        {
            LineAnnotation l => DistanceToSegment(p, l.Start, l.End) <= (l.HasArrowHead ? Math.Max(reach, ShapeGeometry.ArrowHeadLength(l.Width) / 2) : reach)
                                || (l.HasArrowHead && Distance(p, l.End) <= ShapeGeometry.ArrowHeadLength(l.Width)),
            RectangleAnnotation r => DistanceToRectBorder(p, r.Rect) <= reach,
            StrokeAnnotation s => DistanceToPolyline(p, s.Points) <= reach,
            TextAnnotation t => Rectangle.Inflate(new Rectangle(t.Location, t.Size), tolerance, tolerance).Contains(p),
            _ => false,
        };
        return body ? AnnotationHandle.Body : AnnotationHandle.None;
    }

    /// <summary>Chizmani siljitadi.</summary>
    /// <param name="a">Chizma.</param>
    /// <param name="dx">X bo'yicha, px.</param>
    /// <param name="dy">Y bo'yicha, px.</param>
    /// <returns>Yangi chizma.</returns>
    public static Annotation Move(Annotation a, int dx, int dy)
    {
        ArgumentNullException.ThrowIfNull(a);
        return a switch
        {
            LineAnnotation l => l with { Start = Offset(l.Start, dx, dy), End = Offset(l.End, dx, dy) },
            RectangleAnnotation r => r with { Rect = new Rectangle(r.Rect.X + dx, r.Rect.Y + dy, r.Rect.Width, r.Rect.Height) },
            StrokeAnnotation s => s with { Points = s.Points.Select(p => Offset(p, dx, dy)).ToArray() },
            TextAnnotation t => t with { Location = Offset(t.Location, dx, dy) },
            _ => a,
        };
    }

    /// <summary>
    /// Tutqichni sudrash. Chiziq/strelka: shu uchi yangi joyga (Shift — 45° ga, qarama-qarshi uch atrofida).
    /// To'rtburchak: qarama-qarshi burchak joyida qoladi (Shift — kvadrat).
    /// </summary>
    /// <param name="original">Sudrash boshlanganidagi chizma.</param>
    /// <param name="handle">Tutqich.</param>
    /// <param name="p">Joriy nuqta.</param>
    /// <param name="constrain">Shift bosilganmi.</param>
    /// <returns>Yangi chizma.</returns>
    public static Annotation DragHandle(Annotation original, AnnotationHandle handle, Point p, bool constrain)
    {
        ArgumentNullException.ThrowIfNull(original);
        switch (original)
        {
            case LineAnnotation l when handle == AnnotationHandle.Start:
                return l with { Start = constrain ? ShapeGeometry.SnapAngle(l.End, p) : p };
            case LineAnnotation l when handle == AnnotationHandle.End:
                return l with { End = constrain ? ShapeGeometry.SnapAngle(l.Start, p) : p };
            case RectangleAnnotation r when handle is AnnotationHandle.TopLeft or AnnotationHandle.TopRight
                                             or AnnotationHandle.BottomRight or AnnotationHandle.BottomLeft:
                var anchor = handle switch
                {
                    AnnotationHandle.TopLeft => new Point(r.Rect.Right, r.Rect.Bottom),
                    AnnotationHandle.TopRight => new Point(r.Rect.Left, r.Rect.Bottom),
                    AnnotationHandle.BottomRight => new Point(r.Rect.Left, r.Rect.Top),
                    _ => new Point(r.Rect.Right, r.Rect.Top),
                };
                return r with { Rect = constrain ? ShapeGeometry.SquareFrom(anchor, p) : ShapeGeometry.RectFrom(anchor, p) };
            default:
                return original;
        }
    }

    /// <summary>Tahrirdan keyin chizma hali ham ma'noli (masalan, strelka uchlari ustma-ust tushmagan).</summary>
    /// <param name="a">Chizma.</param>
    /// <returns>Ma'noli bo'lsa <c>true</c>.</returns>
    public static bool IsValid(Annotation a) => a switch
    {
        LineAnnotation l => Distance(l.Start, l.End) >= 2,
        RectangleAnnotation r => r.Rect.Width >= 2 && r.Rect.Height >= 2,
        StrokeAnnotation s => s.Points.Count > 0,
        TextAnnotation t => !string.IsNullOrWhiteSpace(t.Text),
        _ => true,
    };

    /// <summary>Nuqtadan kesmagacha masofa.</summary>
    public static double DistanceToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared == 0)
        {
            return Distance(p, a);
        }

        double t = Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1);
        double x = a.X + (t * dx);
        double y = a.Y + (t * dy);
        return Math.Sqrt(((p.X - x) * (p.X - x)) + ((p.Y - y) * (p.Y - y)));
    }

    private static double DistanceToPolyline(Point p, IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return double.MaxValue;
        }

        if (points.Count == 1)
        {
            return Distance(p, points[0]);
        }

        double best = double.MaxValue;
        for (int i = 1; i < points.Count; i++)
        {
            best = Math.Min(best, DistanceToSegment(p, points[i - 1], points[i]));
        }

        return best;
    }

    private static double DistanceToRectBorder(Point p, Rectangle r)
    {
        var tl = new Point(r.Left, r.Top);
        var tr = new Point(r.Right, r.Top);
        var br = new Point(r.Right, r.Bottom);
        var bl = new Point(r.Left, r.Bottom);
        return Math.Min(
            Math.Min(DistanceToSegment(p, tl, tr), DistanceToSegment(p, tr, br)),
            Math.Min(DistanceToSegment(p, br, bl), DistanceToSegment(p, bl, tl)));
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static Point Offset(Point p, int dx, int dy) => new(p.X + dx, p.Y + dy);
}
