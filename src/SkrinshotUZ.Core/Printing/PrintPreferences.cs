namespace SkrinshotUZ.Core.Printing;

/// <summary>
/// Eslab qolinadigan chop etish sozlamalari (keyingi safar o'sha holatda ochiladi). Sarlavha eslab qolinmaydi —
/// u har bir skrinshotga alohida.
/// </summary>
public sealed class PrintPreferences
{
    /// <summary>Nusxalarning eng ko'p soni.</summary>
    public const int MaxCopies = 99;

    /// <summary>Rejim.</summary>
    public PrintMode Mode { get; set; } = PrintMode.FullPage;

    /// <summary>Yo'nalish.</summary>
    public OrientationChoice Orientation { get; set; } = OrientationChoice.Auto;

    /// <summary>Maydon, mm.</summary>
    public double MarginMm { get; set; } = PrintMargins.DefaultUserMarginMm;

    /// <summary>Asl o'lcham sig'masa.</summary>
    public OverflowChoice Overflow { get; set; } = OverflowChoice.FitToPage;

    /// <summary>Oq-qora.</summary>
    public bool Grayscale { get; set; }

    /// <summary>Rasm ostiga sana.</summary>
    public bool ShowDate { get; set; }

    /// <summary>Nusxalar soni.</summary>
    public int Copies { get; set; } = 1;

    /// <summary>Oxirgi tanlangan printer (<c>null</c> — Windows'dagi standart printer).</summary>
    public string? PrinterName { get; set; }

    /// <summary>Nusxa.</summary>
    /// <returns>Yangi obyekt.</returns>
    public PrintPreferences Clone() => (PrintPreferences)MemberwiseClone();

    /// <summary>
    /// Fayldan o'qilgan noto'g'ri qiymatlarni tuzatadi (qo'lda tahrirlangan yoki eski versiya fayli):
    /// maydon — ruxsat etilganlardan eng yaqini, nusxalar 1..99, noma'lum enum — standart.
    /// </summary>
    public void Normalize()
    {
        if (!Enum.IsDefined(Mode))
        {
            Mode = PrintMode.FullPage;
        }

        if (!Enum.IsDefined(Orientation))
        {
            Orientation = OrientationChoice.Auto;
        }

        if (!Enum.IsDefined(Overflow))
        {
            Overflow = OverflowChoice.FitToPage;
        }

        MarginMm = double.IsFinite(MarginMm)
            ? PrintMargins.AllowedUserMarginsMm.MinBy(m => Math.Abs(m - MarginMm))
            : PrintMargins.DefaultUserMarginMm;
        Copies = Math.Clamp(Copies, 1, MaxCopies);
        if (string.IsNullOrWhiteSpace(PrinterName))
        {
            PrinterName = null;
        }
    }

    /// <summary>Joriy rasm uchun <see cref="PrintOptions"/>.</summary>
    /// <param name="caption">Rasm ostidagi yozuv (<see cref="PrintPlanner.BuildCaption"/>).</param>
    /// <returns>Sozlamalar.</returns>
    public PrintOptions ToOptions(string? caption) => new()
    {
        Mode = Mode,
        Orientation = Orientation,
        MarginMm = MarginMm,
        Overflow = Overflow,
        Caption = caption,
    };
}
