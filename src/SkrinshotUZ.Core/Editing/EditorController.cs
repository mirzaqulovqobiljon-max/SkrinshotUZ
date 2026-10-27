using System.Drawing;

namespace SkrinshotUZ.Core.Editing;

/// <summary>Tahrirlash asbobi.</summary>
public enum EditorTool
{
    /// <summary>Asbob tanlanmagan — sichqoncha sohani suradi/o'zgartiradi.</summary>
    None,

    /// <summary>Qalam.</summary>
    Pen,

    /// <summary>To'g'ri chiziq.</summary>
    Line,

    /// <summary>Strelka.</summary>
    Arrow,

    /// <summary>To'rtburchak.</summary>
    Rectangle,

    /// <summary>Marker (yarim shaffof).</summary>
    Marker,

    /// <summary>Matn.</summary>
    Text,
}

/// <summary>Qalinlik darajasi.</summary>
public enum Thickness
{
    /// <summary>Ingichka.</summary>
    Thin,

    /// <summary>O'rtacha (standart).</summary>
    Medium,

    /// <summary>Qalin.</summary>
    Thick,

    /// <summary>Juda qalin.</summary>
    ExtraThick,
}

/// <summary>
/// Chizish holat mashinasi: asbob, rang, qalinlik, chizilayotgan shakl (oldindan ko'rish) va uni tarixga yozish.
/// Qalinlik monitor masshtabiga ko'paytiriladi — 150% ekranda chiziq ko'zga 100% dagidek ko'rinadi.
/// </summary>
public sealed class EditorController
{
    /// <summary>Standart rang — Lightshot'dagidek qizil.</summary>
    public static readonly Color DefaultColor = Color.FromArgb(230, 30, 40);

    private static readonly float[] StrokeWidths = { 2f, 4f, 7f, 12f };
    private static readonly float[] MarkerWidths = { 12f, 18f, 26f, 36f };
    private static readonly float[] FontSizes = { 16f, 20f, 28f, 40f };

    private readonly AnnotationHistory _history;
    private readonly List<Point> _points = new();
    private EditorTool _tool;
    private Point _start;
    private float _width;

    /// <summary>Yangi boshqaruvchi.</summary>
    /// <param name="history">Tarix (chizmalar shu yerga yoziladi).</param>
    public EditorController(AnnotationHistory history)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    /// <summary>Joriy asbob. O'zgartirilsa, chizilayotgan shakl bekor qilinadi.</summary>
    public EditorTool Tool
    {
        get => _tool;
        set
        {
            CancelDrawing();
            _tool = value;
        }
    }

    /// <summary>Rang.</summary>
    public Color Color { get; set; } = DefaultColor;

    /// <summary>Qalinlik.</summary>
    public Thickness Thickness { get; set; } = Thickness.Medium;

    /// <summary>Sichqoncha bilan chizilmoqda.</summary>
    public bool IsDrawing { get; private set; }

    /// <summary>Chizilayotgan shakl (ko'rsatish uchun) — chizilmayotgan bo'lsa <c>null</c>.</summary>
    public Annotation? Preview { get; private set; }

    /// <summary>Chiziq qalinligi, px.</summary>
    /// <param name="thickness">Daraja.</param>
    /// <param name="scale">Monitor masshtabi (1.0 = 100%).</param>
    /// <returns>Qalinlik.</returns>
    public static float StrokeWidth(Thickness thickness, double scale) => StrokeWidths[Index(thickness)] * (float)scale;

    /// <summary>Marker qalinligi, px.</summary>
    /// <param name="thickness">Daraja.</param>
    /// <param name="scale">Masshtab.</param>
    /// <returns>Qalinlik.</returns>
    public static float MarkerWidth(Thickness thickness, double scale) => MarkerWidths[Index(thickness)] * (float)scale;

    /// <summary>Matn shrifti o'lchami, px.</summary>
    /// <param name="thickness">Daraja.</param>
    /// <param name="scale">Masshtab.</param>
    /// <returns>O'lcham.</returns>
    public static float FontSize(Thickness thickness, double scale) => FontSizes[Index(thickness)] * (float)scale;

    /// <summary>Chizishni boshlash (Matn va "asbobsiz" holatda — hech narsa qilmaydi).</summary>
    /// <param name="p">Nuqta (kanvas).</param>
    /// <param name="scale">Shu joydagi monitor masshtabi.</param>
    public void Begin(Point p, double scale)
    {
        if (_tool is EditorTool.None or EditorTool.Text)
        {
            return;
        }

        IsDrawing = true;
        _start = p;
        _points.Clear();
        _points.Add(p);
        _width = _tool == EditorTool.Marker ? MarkerWidth(Thickness, scale) : StrokeWidth(Thickness, scale);
        Preview = Build(p, constrain: false);
    }

    /// <summary>Sichqoncha harakati.</summary>
    /// <param name="p">Nuqta.</param>
    /// <param name="constrain">Shift bosilganmi (45°/kvadrat/to'g'ri chiziq).</param>
    public void Move(Point p, bool constrain)
    {
        if (!IsDrawing)
        {
            return;
        }

        if (_tool is EditorTool.Pen or EditorTool.Marker && _points[^1] != p)
        {
            _points.Add(p);
        }

        Preview = Build(p, constrain);
    }

    /// <summary>Chizishni tugatish: yaroqli shakl tarixga yoziladi.</summary>
    /// <param name="p">Nuqta.</param>
    /// <param name="constrain">Shift bosilganmi.</param>
    /// <returns>Qo'shilgan chizma yoki <c>null</c> (masalan, nol uzunlikdagi chiziq).</returns>
    public Annotation? End(Point p, bool constrain)
    {
        if (!IsDrawing)
        {
            return null;
        }

        Move(p, constrain);
        var result = Preview;
        IsDrawing = false;
        Preview = null;

        if (result is null || !IsMeaningful(result))
        {
            return null;
        }

        _history.Add(result);
        return result;
    }

    /// <summary>Chizilayotgan shaklni bekor qilish.</summary>
    public void CancelDrawing()
    {
        IsDrawing = false;
        Preview = null;
        _points.Clear();
    }

    /// <summary>Matnni qo'shadi (bo'sh yoki faqat probellardan iborat bo'lsa — qo'shilmaydi).</summary>
    /// <param name="location">Chap-yuqori nuqta.</param>
    /// <param name="text">Matn.</param>
    /// <param name="measured">Windows o'lchagan matn o'lchami.</param>
    /// <param name="fontSize">Shrift o'lchami, px.</param>
    /// <returns>Qo'shilgan matn yoki <c>null</c>.</returns>
    public TextAnnotation? CommitText(Point location, string? text, Size measured, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Windows qator oxirlari bir xil bo'lsin, oxiridagi bo'sh qatorlar olib tashlansin.
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        var annotation = new TextAnnotation(location, normalized, Color, fontSize, measured);
        _history.Add(annotation);
        return annotation;
    }

    private static int Index(Thickness t) => t switch
    {
        Thickness.Thin => 0,
        Thickness.Medium => 1,
        Thickness.Thick => 2,
        Thickness.ExtraThick => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, null),
    };

    private static bool IsMeaningful(Annotation a) => a switch
    {
        StrokeAnnotation s => s.Points.Count > 0,              // bitta bosish — nuqta (qalam bilan belgi qo'yish)
        LineAnnotation l => l.Start != l.End && Distance(l.Start, l.End) >= 2,
        RectangleAnnotation r => r.Rect.Width >= 2 && r.Rect.Height >= 2,
        _ => true,
    };

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private Annotation Build(Point p, bool constrain) => _tool switch
    {
        // Shift bilan qalam/marker — to'g'ri chiziq (masalan, matn qatorini marker bilan tekis belgilash).
        EditorTool.Pen or EditorTool.Marker => new StrokeAnnotation(
            constrain ? new[] { _start, ShapeGeometry.SnapAngle(_start, p) } : _points.ToArray(),
            Color,
            _width,
            _tool == EditorTool.Marker),
        EditorTool.Line or EditorTool.Arrow => new LineAnnotation(
            _start,
            constrain ? ShapeGeometry.SnapAngle(_start, p) : p,
            Color,
            _width,
            _tool == EditorTool.Arrow),
        EditorTool.Rectangle => new RectangleAnnotation(
            constrain ? ShapeGeometry.SquareFrom(_start, p) : ShapeGeometry.RectFrom(_start, p),
            Color,
            _width),
        _ => throw new InvalidOperationException($"{_tool} asbobi chizish uchun emas."),
    };
}
