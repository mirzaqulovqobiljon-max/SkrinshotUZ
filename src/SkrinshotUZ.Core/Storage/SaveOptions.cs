using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Core.Storage;

/// <summary>Saqlash oynasi va formatlar.</summary>
public static class SaveOptions
{
    /// <summary>JPG sifati (0–100): matn va chiziqlar tiniq qoladi, fayl PNG'dan ancha kichik.</summary>
    public const long JpegQuality = 90;

    /// <summary>Saqlash oynasining filtri (tartib — <see cref="ImageFileFormat"/> bilan bir xil: 1 — PNG, 2 — JPG).</summary>
    public static string DialogFilter =>
        Strings.Save_FilterPng + " (*.png)|*.png|" + Strings.Save_FilterJpg + " (*.jpg)|*.jpg;*.jpeg";

    /// <summary>Format uchun filtr raqami (Windows oynasida 1 dan boshlanadi).</summary>
    /// <param name="format">Format.</param>
    /// <returns>1 yoki 2.</returns>
    public static int FilterIndexFor(ImageFileFormat format) => format == ImageFileFormat.Jpg ? 2 : 1;

    /// <summary>
    /// Saqlanadigan format: avvalo fayl kengaytmasi (foydalanuvchi ".jpg" deb yozgan bo'lsa), aks holda tanlangan filtr.
    /// </summary>
    /// <param name="path">Tanlangan fayl yo'li.</param>
    /// <param name="filterIndex">Tanlangan filtr raqami.</param>
    /// <returns>Format.</returns>
    public static ImageFileFormat FormatFor(string path, int filterIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string ext = Path.GetExtension(path).ToUpperInvariant();
        return ext switch
        {
            ".JPG" or ".JPEG" => ImageFileFormat.Jpg,
            ".PNG" => ImageFileFormat.Png,
            _ => filterIndex == 2 ? ImageFileFormat.Jpg : ImageFileFormat.Png,
        };
    }

    /// <summary>
    /// Kengaytmasiz yoki boshqa kengaytmali nomga format kengaytmasini qo'shadi ("dars" → "dars.png").
    /// </summary>
    /// <param name="path">Yo'l.</param>
    /// <param name="format">Format.</param>
    /// <returns>To'g'ri kengaytmali yo'l.</returns>
    public static string EnsureExtension(string path, ImageFileFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string ext = Path.GetExtension(path).ToUpperInvariant();
        bool ok = format == ImageFileFormat.Png ? ext == ".PNG" : ext is ".JPG" or ".JPEG";
        return ok ? path : path + ScreenshotFileNamer.GetExtension(format);
    }

    /// <summary>
    /// Saqlash papkasi: oxirgi tanlangan papka (hali mavjud bo'lsa), aks holda standart "Rasmlar\Skrinshot UZ".
    /// </summary>
    /// <param name="lastFolder">Oxirgi papka (sozlamalardan).</param>
    /// <param name="myPictures">"Rasmlar" papkasi.</param>
    /// <param name="directoryExists">Papka mavjudligini tekshiruvchi.</param>
    /// <returns>Papka.</returns>
    public static string ResolveFolder(string? lastFolder, string myPictures, Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);
        return !string.IsNullOrWhiteSpace(lastFolder) && directoryExists(lastFolder)
            ? lastFolder
            : AppPaths.DefaultScreenshotsFolder(myPictures);
    }
}
