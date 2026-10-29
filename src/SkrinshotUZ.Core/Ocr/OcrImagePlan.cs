namespace SkrinshotUZ.Core.Ocr;

/// <summary>
/// Matnni aniqlash (OCR) tillari. Tesseract'ning "fast" modellari: o'zbek (lotin), o'zbek (kirill), rus, ingliz.
/// Bir vaqtda to'rttasi yuklanadi — o'qituvchi tilni tanlashi shart emas.
/// </summary>
public static class OcrLanguages
{
    /// <summary>Tesseract'ga beriladigan tillar qatori.</summary>
    public const string All = "uzb+uzb_cyrl+rus+eng";

    /// <summary>Kerakli model fayllari nomlari (<c>uzb.traineddata</c> va h.k.).</summary>
    public static IReadOnlyList<string> ModelFiles { get; } = All.Split('+').Select(l => l + ".traineddata").ToArray();
}

/// <summary>
/// Rasmni OCR'dan oldin necha marta kattalashtirish. Skrinshotdagi harflar kichik (100% masshtabda 11–13 px) —
/// Tesseract esa ~300 DPI'dagi skanerlangan matnga o'rgatilgan. Shuning uchun rasm monitor DPI'siga qarab
/// 1–3 marta kattalashtiriladi, lekin xotira chegarasidan oshmaydi (eski kompyuterlar uchun).
/// </summary>
public static class OcrImagePlan
{
    /// <summary>Maqsadli o'lcham, DPI (96 DPI'li skrinshot uchun 3 marta).</summary>
    public const int TargetDpi = 288;

    /// <summary>Eng katta masshtab.</summary>
    public const int MaxScale = 3;

    /// <summary>Kattalashtirilgan rasmning bir tomoni, px.</summary>
    public const int MaxSide = 8000;

    /// <summary>Kattalashtirilgan rasm piksellari soni (kulrang — 1 bayt/piksel → ~24 MB).</summary>
    public const long MaxPixels = 24_000_000;

    /// <summary>Kattalashtirish koeffitsienti (1, 2 yoki 3).</summary>
    /// <param name="width">Rasm kengligi, px.</param>
    /// <param name="height">Rasm balandligi, px.</param>
    /// <param name="dpi">Rasm olingan monitor DPI'si (noma'lum yoki noto'g'ri bo'lsa 96 deb olinadi).</param>
    /// <returns>Koeffitsient.</returns>
    public static int ChooseScale(int width, int height, double dpi)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (double.IsNaN(dpi) || dpi < 48 || dpi > 960)
        {
            dpi = 96;
        }

        int scale = Math.Clamp((int)Math.Round(TargetDpi / dpi, MidpointRounding.AwayFromZero), 1, MaxScale);
        while (scale > 1 && !Fits(width, height, scale))
        {
            scale--;
        }

        return scale;
    }

    /// <summary>Tesseract'ga beriladigan "manba DPI" (kattalashtirilgandan keyin).</summary>
    /// <param name="dpi">Monitor DPI'si.</param>
    /// <param name="scale">Koeffitsient.</param>
    /// <returns>DPI, 70..1200 oralig'ida (Tesseract shu oraliqni qabul qiladi).</returns>
    public static int SourceResolution(double dpi, int scale)
    {
        if (double.IsNaN(dpi) || dpi < 48 || dpi > 960)
        {
            dpi = 96;
        }

        return Math.Clamp((int)Math.Round(dpi * Math.Max(1, scale)), 70, 1200);
    }

    private static bool Fits(int width, int height, int scale) =>
        (long)width * scale <= MaxSide
        && (long)height * scale <= MaxSide
        && (long)width * scale * height * scale <= MaxPixels;
}
