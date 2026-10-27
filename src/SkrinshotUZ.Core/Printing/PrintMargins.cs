namespace SkrinshotUZ.Core.Printing;

/// <summary>
/// Printerning bosilmaydigan cheti (hard margin), millimetrda, varaq koordinatalarida.
/// WinForms'da: chap = PageSettings.HardMarginX, yuqori = HardMarginY,
/// o'ng = qog'oz kengligi − HardMarginX − PrintableArea.Width (pastki — xuddi shunday).
/// </summary>
/// <param name="Left">Chap, mm.</param>
/// <param name="Top">Yuqori, mm.</param>
/// <param name="Right">O'ng, mm.</param>
/// <param name="Bottom">Pastki, mm.</param>
public readonly record struct HardMargins(double Left, double Top, double Right, double Bottom)
{
    /// <summary>Bosilmaydigan cheti yo'q (masalan, "Microsoft Print to PDF").</summary>
    public static HardMargins None => default;

    /// <summary>Simmetrik chet: chap=o'ng=<paramref name="x"/>, yuqori=past=<paramref name="y"/>.</summary>
    /// <param name="x">Gorizontal chet, mm.</param>
    /// <param name="y">Vertikal chet, mm.</param>
    /// <returns>Yangi qiymat.</returns>
    public static HardMargins Symmetric(double x, double y) => new(x, y, x, y);
}

/// <summary>
/// Chop etish maydonlari. Foydalanuvchi maydoni (0/5/10/15 mm) qog'oz chetidan o'lchanadi;
/// agar printerning bosilmaydigan cheti kattaroq bo'lsa — o'sha ishlatiladi (rasm kesilib qolmasin).
/// </summary>
public sealed record PrintMargins
{
    /// <summary>Standart foydalanuvchi maydoni (vazifa bo'yicha 10 mm).</summary>
    public const double DefaultUserMarginMm = 10.0;

    /// <summary>Sozlamalarda ruxsat etilgan maydon qiymatlari.</summary>
    public static IReadOnlyList<double> AllowedUserMarginsMm { get; } = new[] { 0.0, 5.0, 10.0, 15.0 };

    /// <summary>Yangi maydonlar.</summary>
    /// <param name="userMarginMm">Foydalanuvchi maydoni, mm (manfiy emas).</param>
    /// <param name="hard">Printerning bosilmaydigan cheti.</param>
    public PrintMargins(double userMarginMm, HardMargins hard = default)
    {
        Guard.NonNegative(userMarginMm, nameof(userMarginMm));
        Guard.NonNegative(hard.Left, nameof(hard) + ".Left");
        Guard.NonNegative(hard.Top, nameof(hard) + ".Top");
        Guard.NonNegative(hard.Right, nameof(hard) + ".Right");
        Guard.NonNegative(hard.Bottom, nameof(hard) + ".Bottom");
        UserMarginMm = userMarginMm;
        Hard = hard;
    }

    /// <summary>Standart: 10 mm, bosilmaydigan chetsiz.</summary>
    public static PrintMargins Default { get; } = new(DefaultUserMarginMm);

    /// <summary>Foydalanuvchi maydoni, mm.</summary>
    public double UserMarginMm { get; }

    /// <summary>Printerning bosilmaydigan cheti, mm.</summary>
    public HardMargins Hard { get; }

    /// <summary>Haqiqiy chap maydon, mm.</summary>
    public double EffectiveLeft => Math.Max(UserMarginMm, Hard.Left);

    /// <summary>Haqiqiy yuqori maydon, mm.</summary>
    public double EffectiveTop => Math.Max(UserMarginMm, Hard.Top);

    /// <summary>Haqiqiy o'ng maydon, mm.</summary>
    public double EffectiveRight => Math.Max(UserMarginMm, Hard.Right);

    /// <summary>Haqiqiy pastki maydon, mm.</summary>
    public double EffectiveBottom => Math.Max(UserMarginMm, Hard.Bottom);
}
