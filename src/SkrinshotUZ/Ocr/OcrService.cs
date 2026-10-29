using System.Reflection;
using SkrinshotUZ.Core.Ocr;
using SkrinshotUZ.Core.Storage;

namespace SkrinshotUZ.Ocr;

/// <summary>OCR natijasi: xatboshilar va hujjat yo'li.</summary>
/// <param name="Paragraphs">Aniqlangan matn.</param>
/// <param name="VisibleChars">Bo'shliqsiz belgilar soni (0 — matn topilmadi).</param>
internal sealed record OcrResult(IReadOnlyList<OcrParagraph> Paragraphs, int VisibleChars);

/// <summary>
/// "Matnni Word'ga": .exe ichidagi Tesseract fayllarini (birinchi marta) chiqaradi, matnni aniqlaydi va .docx yozadi.
/// Hammasi kompyuterning o'zida — rasm hech qayerga yuborilmaydi.
/// </summary>
internal static class OcrService
{
    /// <summary>Resurs nomlari prefiksi (csproj'dagi LogicalName bilan bir xil).</summary>
    public const string ResourcePrefix = "ocr/";

    private static readonly Lazy<IReadOnlyList<OcrFile>> Embedded = new(() => FilesFrom(typeof(OcrService).Assembly));

    /// <summary>Dastur ichidagi OCR fayllari.</summary>
    public static IReadOnlyList<OcrFile> EmbeddedFiles => Embedded.Value;

    /// <summary>OCR fayllari to'liq joylanganmi (to'liq bo'lmasa — tugma bosilganda tushuntirish ko'rsatiladi).</summary>
    public static bool IsAvailable => OcrFiles.Missing(EmbeddedFiles.ToList()).Count == 0;

    /// <summary>Fayllar chiqariladigan papka.</summary>
    public static string Root => AppPaths.OcrFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>Assembly resurslaridan OCR fayllari ro'yxati.</summary>
    public static IReadOnlyList<OcrFile> FilesFrom(Assembly assembly)
    {
        var files = new List<OcrFile>();
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            long length;
            using (var s = assembly.GetManifestResourceStream(name))
            {
                if (s is null)
                {
                    continue;
                }

                length = s.Length;
            }

            string resource = name;
            files.Add(new OcrFile(name[ResourcePrefix.Length..], length, () => assembly.GetManifestResourceStream(resource)
                ?? throw new OcrUnavailableException("Resurs topilmadi: " + resource)));
        }

        return files;
    }

    /// <summary>Kutubxonalarni tayyorlaydi (chiqarish + yuklash). Tutun testi va birinchi ishlatish uchun.</summary>
    /// <exception cref="OcrUnavailableException">Fayllar yo'q yoki yuklanmadi.</exception>
    public static TesseractOcr Prepare()
    {
        var files = EmbeddedFiles;
        string version = typeof(OcrService).Assembly.GetName().Version?.ToString() ?? "0";
        string dir = OcrFiles.EnsureExtracted(Root, files, version);
        return TesseractOcr.Load(dir);
    }

    /// <summary>Rasmdagi matnni aniqlaydi (fon oqimida chaqiriladi).</summary>
    /// <param name="image">Rasm.</param>
    /// <param name="dpi">Monitor DPI'si.</param>
    /// <returns>Natija.</returns>
    public static OcrResult Recognize(Bitmap image, double dpi)
    {
        string text = Prepare().Recognize(image, dpi);
        var paragraphs = OcrText.ToParagraphs(text);
        return new OcrResult(paragraphs, OcrText.CountVisibleChars(paragraphs));
    }

    /// <summary>Natijani papkaga yangi .docx sifatida yozadi.</summary>
    /// <param name="result">Natija.</param>
    /// <param name="folder">Papka (yo'q bo'lsa — yaratiladi).</param>
    /// <returns>Fayl yo'li.</returns>
    public static string SaveDocument(OcrResult result, string folder)
    {
        Directory.CreateDirectory(folder);
        string path = ScreenshotFileNamer.CreateUniqueDocumentPath(folder, DateTime.Now, File.Exists);
        DocxWriter.WriteFile(path, result.Paragraphs, DateTime.UtcNow);
        return path;
    }
}
