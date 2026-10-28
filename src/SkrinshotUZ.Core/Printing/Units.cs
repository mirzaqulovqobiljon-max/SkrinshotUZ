namespace SkrinshotUZ.Core.Printing;

/// <summary>O'lchov birliklarini o'zaro aylantirish.</summary>
public static class Units
{
    /// <summary>1 dyuymdagi millimetrlar.</summary>
    public const double MmPerInch = 25.4;

    /// <summary>Windows'ning "mantiqiy" bazaviy DPI qiymati (100% masshtab).</summary>
    public const double BaseDpi = 96.0;

    /// <summary>
    /// Pikselni millimetrga aylantiradi.
    /// Formula (vazifadagi): mm = px / (dpi/96) × 25.4 / 96, soddalashtirilsa: mm = px × 25.4 / dpi.
    /// </summary>
    /// <param name="pixels">Piksellar soni (manfiy emas).</param>
    /// <param name="dpi">Monitor DPI qiymati (musbat).</param>
    /// <returns>Millimetr.</returns>
    public static double PixelsToMm(double pixels, double dpi)
    {
        Guard.NonNegative(pixels, nameof(pixels));
        Guard.Positive(dpi, nameof(dpi));
        return pixels * MmPerInch / dpi;
    }

    /// <summary>Millimetrni berilgan DPI'dagi pikselga aylantiradi (yaxlitlamasdan).</summary>
    /// <param name="mm">Millimetr.</param>
    /// <param name="dpi">DPI (musbat).</param>
    /// <returns>Piksel (kasr bo'lishi mumkin).</returns>
    public static double MmToPixels(double mm, double dpi)
    {
        Guard.Positive(dpi, nameof(dpi));
        return mm * dpi / MmPerInch;
    }

    /// <summary>Rasm faylidan o'qilgan DPI uchun ruxsat etilgan eng kichik qiymat.</summary>
    public const double MinImageDpi = 48;

    /// <summary>Rasm faylidan o'qilgan DPI uchun ruxsat etilgan eng katta qiymat.</summary>
    public const double MaxImageDpi = 960;

    /// <summary>
    /// Rasm faylidagi DPI (Skrinshot UZ uni monitor masshtabi bilan yozadi). Yo'q yoki aqlga sig'maydigan qiymat bo'lsa — 96 (100%).
    /// </summary>
    /// <param name="dpi">Fayldan o'qilgan qiymat.</param>
    /// <returns>Ishonchli DPI.</returns>
    public static double SanitizeImageDpi(double dpi) =>
        double.IsFinite(dpi) && dpi >= MinImageDpi && dpi <= MaxImageDpi ? dpi : BaseDpi;

    /// <summary>
    /// Millimetrni dyuymning yuzdan bir ulushiga aylantiradi.
    /// WinForms PrintDocument'da Graphics standart birligi (GraphicsUnit.Display) aynan shu.
    /// </summary>
    /// <param name="mm">Millimetr.</param>
    /// <returns>1/100 dyuym.</returns>
    public static double MmToHundredthsInch(double mm) => mm / MmPerInch * 100.0;

    /// <summary>Dyuymning yuzdan bir ulushini millimetrga aylantiradi (PageSettings.HardMarginX va h.k. uchun).</summary>
    /// <param name="hundredthsInch">1/100 dyuym.</param>
    /// <returns>Millimetr.</returns>
    public static double HundredthsInchToMm(double hundredthsInch) => hundredthsInch / 100.0 * MmPerInch;
}

/// <summary>Argumentlarni tekshirish yordamchisi.</summary>
internal static class Guard
{
    public static void Positive(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Qiymat musbat chekli son bo'lishi kerak.");
        }
    }

    public static void Positive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Qiymat musbat bo'lishi kerak.");
        }
    }

    public static void NonNegative(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Qiymat manfiy bo'lmagan chekli son bo'lishi kerak.");
        }
    }
}
