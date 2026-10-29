using System.Security.Cryptography;
using System.Text;

namespace SkrinshotUZ.Core.Ocr;

/// <summary>Dastur ichiga joylangan OCR fayli (Tesseract kutubxonasi yoki til modeli).</summary>
/// <param name="RelativePath">Nisbiy yo'l: <c>bin/tesseract50.dll</c>, <c>tessdata/uzb.traineddata</c>.</param>
/// <param name="Length">Hajmi, bayt.</param>
/// <param name="Open">Mazmunini o'qish uchun oqim ochadi.</param>
public sealed record OcrFile(string RelativePath, long Length, Func<Stream> Open);

/// <summary>OCR ishlatib bo'lmaydi (fayllar yo'q yoki buzilgan) — foydalanuvchiga tushunarli xabar bilan.</summary>
public sealed class OcrUnavailableException : Exception
{
    /// <summary>Yaratish.</summary>
    public OcrUnavailableException()
    {
    }

    /// <summary>Yaratish.</summary>
    /// <param name="message">Xabar.</param>
    public OcrUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Yaratish.</summary>
    /// <param name="message">Xabar.</param>
    /// <param name="inner">Ichki xato.</param>
    public OcrUnavailableException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// OCR fayllari bitta .exe ichida saqlanadi va birinchi ishlatilganda
/// <c>%LOCALAPPDATA%\Skrinshot UZ\ocr\&lt;id&gt;</c> papkasiga bir marta chiqariladi (keyingi safar — tayyor papka).
/// Papka nomi fayllar ro'yxati va versiyadan hisoblanadi: yangi versiya o'z papkasini yaratadi, eskisi o'chiriladi.
/// Yarim chiqarilgan papka hech qachon ishlatilmaydi: avval vaqtinchalik papkaga yoziladi, oxirida nomi o'zgartiriladi.
/// </summary>
public static class OcrFiles
{
    /// <summary>Kutubxonalar papkasi.</summary>
    public const string BinFolder = "bin";

    /// <summary>Til modellari papkasi.</summary>
    public const string TessdataFolder = "tessdata";

    /// <summary>To'liq chiqarilganlik belgisi.</summary>
    public const string ReadyMarker = "ready.txt";

    /// <summary>Fayllar to'plami identifikatori (12 ta hex belgi).</summary>
    /// <param name="files">Fayllar.</param>
    /// <param name="version">Dastur versiyasi.</param>
    /// <returns>Identifikator.</returns>
    public static string BundleId(IEnumerable<OcrFile> files, string version)
    {
        ArgumentNullException.ThrowIfNull(files);
        var sb = new StringBuilder(version ?? string.Empty).Append('\n');
        foreach (var f in files.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
        {
            sb.Append(f.RelativePath).Append('|').Append(f.Length).Append('\n');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
    }

    /// <summary>To'plamda nima yetishmaydi: Tesseract kutubxonasi, Leptonica va har bir til modeli.</summary>
    /// <param name="files">Fayllar.</param>
    /// <returns>Yetishmayotganlar ro'yxati (bo'sh — hammasi joyida).</returns>
    public static IReadOnlyList<string> Missing(IReadOnlyCollection<OcrFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var names = files.Select(f => f.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        if (EngineLibrary(names) is null)
        {
            missing.Add(BinFolder + "/tesseract*.dll");
        }

        if (!names.Any(n => IsInFolder(n, BinFolder) && FileName(n).StartsWith("leptonica", StringComparison.OrdinalIgnoreCase)))
        {
            missing.Add(BinFolder + "/leptonica*.dll");
        }

        missing.AddRange(OcrLanguages.ModelFiles.Select(m => TessdataFolder + "/" + m).Where(p => !names.Contains(p)));
        return missing;
    }

    /// <summary>Tesseract kutubxonasining nisbiy yo'li (<c>bin/tesseract50.dll</c>) yoki <c>null</c>.</summary>
    /// <param name="relativePaths">Fayllar.</param>
    /// <returns>Yo'l.</returns>
    public static string? EngineLibrary(IEnumerable<string> relativePaths) =>
        relativePaths
            .Where(p => IsInFolder(p, BinFolder))
            .Where(p => FileName(p).StartsWith("tesseract", StringComparison.OrdinalIgnoreCase)
                && FileName(p).EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    /// <summary>
    /// Kutubxonalarni yuklash tartibi: avval Visual C++ runtime, keyin boshqa yordamchilar, Leptonica, oxirida Tesseract.
    /// Shunda har bir kutubxonaning bog'liqliklari allaqachon xotirada bo'ladi (tizimda VC++ o'rnatilmagan bo'lsa ham).
    /// </summary>
    /// <param name="relativePaths">Fayllar.</param>
    /// <returns><c>bin/</c> dagi .dll'lar tartib bilan.</returns>
    public static IReadOnlyList<string> NativeLoadOrder(IEnumerable<string> relativePaths) =>
        relativePaths
            .Where(p => IsInFolder(p, BinFolder) && FileName(p).EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Rank(FileName(p)))
            .ThenBy(p => FileName(p), StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Papka to'liq va to'g'ri chiqarilganmi (belgi bor, har bir fayl kutilgan hajmda).</summary>
    /// <param name="directory">Papka.</param>
    /// <param name="files">Kutilgan fayllar.</param>
    /// <returns>Tayyorligi.</returns>
    public static bool IsComplete(string directory, IEnumerable<OcrFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!File.Exists(Path.Combine(directory, ReadyMarker)))
        {
            return false;
        }

        foreach (var f in files)
        {
            var info = new FileInfo(Path.Combine(directory, ToLocal(f.RelativePath)));
            if (!info.Exists || info.Length != f.Length)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Fayllarni <paramref name="root"/> ichidagi o'z papkasiga chiqaradi (tayyor bo'lsa — qayta yozmaydi)
    /// va boshqa (eski versiya) papkalarini o'chirishga urinadi.
    /// </summary>
    /// <param name="root">Asosiy papka (<c>…\Skrinshot UZ\ocr</c>).</param>
    /// <param name="files">Fayllar.</param>
    /// <param name="version">Dastur versiyasi.</param>
    /// <returns>Tayyor papka yo'li.</returns>
    /// <exception cref="OcrUnavailableException">Fayllar to'liq emas yoki yozib bo'lmadi.</exception>
    public static string EnsureExtracted(string root, IReadOnlyList<OcrFile> files, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(files);

        var missing = Missing(files);
        if (missing.Count > 0)
        {
            throw new OcrUnavailableException("OCR fayllari yo'q: " + string.Join(", ", missing));
        }

        foreach (var f in files)
        {
            ValidateRelativePath(f.RelativePath);
        }

        string id = BundleId(files, version);
        string target = Path.Combine(root, id);
        if (IsComplete(target, files))
        {
            CleanupOthers(root, id);
            return target;
        }

        string temp = Path.Combine(root, id + ".tmp-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            foreach (var f in files)
            {
                string path = Path.Combine(temp, ToLocal(f.RelativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                long written;
                using (var source = f.Open())
                using (var dest = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(dest);
                    written = dest.Length;
                }

                if (written != f.Length)
                {
                    throw new OcrUnavailableException($"OCR fayli buzilgan: {f.RelativePath} ({written} ≠ {f.Length} bayt)");
                }
            }

            File.WriteAllText(Path.Combine(temp, ReadyMarker), id);

            if (Directory.Exists(target))
            {
                // Oldingi chiqarish yarimda qolgan (masalan, kompyuter o'chib qolgan) — yangisi bilan almashtiriladi.
                Directory.Delete(target, recursive: true);
            }

            Directory.Move(temp, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteDirectory(temp);
            if (IsComplete(target, files))
            {
                return target; // parallel nusxa ulgurib chiqarib bo'lgan
            }

            throw new OcrUnavailableException("OCR fayllarini yozib bo'lmadi: " + ex.Message, ex);
        }
        catch
        {
            TryDeleteDirectory(temp);
            throw;
        }

        CleanupOthers(root, id);
        return target;
    }

    /// <summary>Nisbiy yo'lni joriy tizim ko'rinishiga o'tkazadi.</summary>
    /// <param name="relativePath">"/" bilan yo'l.</param>
    /// <returns>Yo'l.</returns>
    public static string ToLocal(string relativePath) => relativePath.Replace('/', Path.DirectorySeparatorChar);

    private static void ValidateRelativePath(string p)
    {
        bool ok = !string.IsNullOrWhiteSpace(p)
            && !Path.IsPathRooted(p)
            && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '/')
            && p.Split('/').All(s => s.Length > 0 && s != "." && s != "..");
        if (!ok)
        {
            throw new OcrUnavailableException("OCR fayl nomi noto'g'ri: " + p);
        }
    }

    private static void CleanupOthers(string root, string keep)
    {
        try
        {
            foreach (string dir in Directory.EnumerateDirectories(root))
            {
                if (!string.Equals(Path.GetFileName(dir), keep, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteDirectory(dir);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // tozalash — ixtiyoriy
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // band (boshqa nusxa ishlatyapti) — keyingi safar o'chiriladi
        }
    }

    private static int Rank(string name)
    {
        string n = name.ToLowerInvariant();
        return n switch
        {
            "vcruntime140.dll" => 0,
            _ when n.StartsWith("vcruntime140", StringComparison.Ordinal) => 1,
            _ when n.StartsWith("msvcp140", StringComparison.Ordinal) => 2,
            _ when n.StartsWith("vcomp", StringComparison.Ordinal) || n.StartsWith("concrt", StringComparison.Ordinal) => 3,
            _ when n.StartsWith("leptonica", StringComparison.Ordinal) => 5,
            _ when n.StartsWith("tesseract", StringComparison.Ordinal) => 6,
            _ => 4,
        };
    }

    private static bool IsInFolder(string relativePath, string folder) =>
        relativePath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) && relativePath.IndexOf('/', folder.Length + 1) < 0;

    private static string FileName(string relativePath) => relativePath[(relativePath.LastIndexOf('/') + 1)..];
}
