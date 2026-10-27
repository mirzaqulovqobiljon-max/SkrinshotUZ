namespace SkrinshotUZ.Core.Printing;

/// <summary>Varaq yo'nalishi.</summary>
public enum PageOrientation
{
    /// <summary>Kitob (tik) — 210×297 mm.</summary>
    Portrait,

    /// <summary>Albom (yotiq) — 297×210 mm.</summary>
    Landscape,
}

/// <summary>A4 varaqning qaysi yarmi.</summary>
public enum HalfPageSlot
{
    /// <summary>Yuqori yarmi (0 … 148.5 mm).</summary>
    Top,

    /// <summary>Pastki yarmi (148.5 … 297 mm).</summary>
    Bottom,
}

/// <summary>Millimetrdagi o'lcham.</summary>
/// <param name="Width">Kenglik, mm.</param>
/// <param name="Height">Balandlik, mm.</param>
public readonly record struct SizeMm(double Width, double Height);

/// <summary>Varaqdagi to'rtburchak, millimetrda. Koordinata boshi — varaqning yuqori-chap burchagi.</summary>
/// <param name="X">Chap chetdan masofa, mm.</param>
/// <param name="Y">Yuqori chetdan masofa, mm.</param>
/// <param name="Width">Kenglik, mm.</param>
/// <param name="Height">Balandlik, mm.</param>
public readonly record struct RectMm(double X, double Y, double Width, double Height)
{
    /// <summary>O'ng chegara, mm.</summary>
    public double Right => X + Width;

    /// <summary>Pastki chegara, mm.</summary>
    public double Bottom => Y + Height;

    /// <summary>To'rtburchakni siljitadi.</summary>
    /// <param name="dx">X bo'yicha siljish, mm.</param>
    /// <param name="dy">Y bo'yicha siljish, mm.</param>
    /// <returns>Siljitilgan yangi to'rtburchak.</returns>
    public RectMm Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>Boshqa to'rtburchak ichida yotadimi (<paramref name="tolerance"/> aniqlikda).</summary>
    /// <param name="outer">Tashqi to'rtburchak.</param>
    /// <param name="tolerance">Ruxsat etilgan xatolik, mm.</param>
    /// <returns>Ichida bo'lsa <c>true</c>.</returns>
    public bool IsInside(RectMm outer, double tolerance = 1e-6) =>
        X >= outer.X - tolerance &&
        Y >= outer.Y - tolerance &&
        Right <= outer.Right + tolerance &&
        Bottom <= outer.Bottom + tolerance;
}

/// <summary>Rasmdagi piksel sohasi (manba rasmdan qaysi qism olinadi).</summary>
/// <param name="X">Chapdan, px.</param>
/// <param name="Y">Yuqoridan, px.</param>
/// <param name="Width">Kenglik, px.</param>
/// <param name="Height">Balandlik, px.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);
