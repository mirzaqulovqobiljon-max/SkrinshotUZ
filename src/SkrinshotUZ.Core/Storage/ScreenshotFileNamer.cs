using System.Globalization;

namespace SkrinshotUZ.Core.Storage;

/// <summary>Saqlanadigan rasm formati.</summary>
public enum ImageFileFormat
{
    /// <summary>PNG — sifat yo'qotilmaydi (standart).</summary>
    Png,

    /// <summary>JPG — fayl kichikroq.</summary>
    Jpg,
}

/// <summary>
/// Skrinshot fayl nomlari generatori: <c>skrinshot_2026-10-27_20-38-15.png</c>.
/// Nom doim invariant madaniyatda yoziladi — kompyuterning til/kalendar sozlamalaridan qat'i nazar
/// (masalan, arab raqamlari yoki hijriy kalendar nomni buzmaydi).
/// </summary>
public static class ScreenshotFileNamer
{
    /// <summary>Fayl nomi prefiksi.</summary>
    public const string Prefix = "skrinshot_";

    /// <summary>Sana-vaqt qismi formati.</summary>
    public const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

    /// <summary>Bir soniyada nechta nomgacha ketma-ket raqam qo'shib urinib ko'riladi.</summary>
    public const int MaxDuplicateIndex = 9999;

    /// <summary>Format uchun kengaytma (nuqta bilan).</summary>
    /// <param name="format">Format.</param>
    /// <returns><c>.png</c> yoki <c>.jpg</c>.</returns>
    public static string GetExtension(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Png => ".png",
        ImageFileFormat.Jpg => ".jpg",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Noma'lum format."),
    };

    /// <summary>Fayl nomini yaratadi.</summary>
    /// <param name="timestamp">Skrinshot olingan vaqt (mahalliy).</param>
    /// <param name="format">Format.</param>
    /// <returns>Masalan, <c>skrinshot_2026-10-27_20-38-15.png</c>.</returns>
    public static string CreateFileName(DateTime timestamp, ImageFileFormat format) =>
        Prefix + timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture) + GetExtension(format);

    /// <summary>
    /// Papkadagi band bo'lmagan to'liq yo'lni qaytaradi. Bir soniyada bir nechta skrinshot olinsa:
    /// <c>skrinshot_…_20-38-15.png</c>, <c>skrinshot_…_20-38-15_2.png</c>, <c>…_3.png</c> va h.k.
    /// </summary>
    /// <param name="folder">Papka.</param>
    /// <param name="timestamp">Vaqt.</param>
    /// <param name="format">Format.</param>
    /// <param name="fileExists">Fayl mavjudligini tekshiruvchi funksiya (testda almashtiriladi).</param>
    /// <returns>To'liq yo'l.</returns>
    public static string CreateUniquePath(string folder, DateTime timestamp, ImageFileFormat format, Func<string, bool> fileExists) =>
        UniquePath(folder, Prefix, timestamp, GetExtension(format), fileExists);

    /// <summary>
    /// Word hujjati (OCR natijasi) uchun band bo'lmagan yo'l: <c>matn_2026-10-29_19-40-00.docx</c>,
    /// bir soniyada bir nechta bo'lsa — <c>…_2.docx</c>.
    /// </summary>
    /// <param name="folder">Papka.</param>
    /// <param name="timestamp">Vaqt.</param>
    /// <param name="fileExists">Fayl mavjudligini tekshiruvchi funksiya.</param>
    /// <returns>To'liq yo'l.</returns>
    public static string CreateUniqueDocumentPath(string folder, DateTime timestamp, Func<string, bool> fileExists) =>
        UniquePath(folder, DocumentPrefix, timestamp, DocumentExtension, fileExists);

    /// <summary>Word hujjati nomi prefiksi.</summary>
    public const string DocumentPrefix = "matn_";

    /// <summary>Word hujjati kengaytmasi.</summary>
    public const string DocumentExtension = ".docx";

    private static string UniquePath(string folder, string prefix, DateTime timestamp, string ext, Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(fileExists);

        string stem = prefix + timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);

        string candidate = Path.Combine(folder, stem + ext);
        if (!fileExists(candidate))
        {
            return candidate;
        }

        for (int i = 2; i <= MaxDuplicateIndex; i++)
        {
            candidate = Path.Combine(folder, $"{stem}_{i.ToString(CultureInfo.InvariantCulture)}{ext}");
            if (!fileExists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"'{folder}' papkasida bo'sh fayl nomi topilmadi.");
    }
}
