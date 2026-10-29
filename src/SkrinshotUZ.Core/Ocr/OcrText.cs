using System.Text;

namespace SkrinshotUZ.Core.Ocr;

/// <summary>Hujjatdagi bitta xatboshi: qatorlari ekrandagidek saqlanadi (ro'yxat va jadvallar buzilmasin).</summary>
/// <param name="Lines">Qatorlar (bo'sh emas).</param>
public sealed record OcrParagraph(IReadOnlyList<string> Lines);

/// <summary>Tesseract natijasini Word hujjati uchun xatboshilarga ajratish.</summary>
public static class OcrText
{
    /// <summary>
    /// Matnni xatboshilarga ajratadi: bo'sh qator — xatboshi chegarasi. Boshqaruv belgilari (sahifa almashinuvi va h.k.)
    /// olib tashlanadi, qator oxiridagi bo'shliqlar kesiladi. Tab saqlanadi.
    /// </summary>
    /// <param name="raw">Tesseract chiqargan matn (<c>null</c> bo'lishi mumkin).</param>
    /// <returns>Xatboshilar (matn bo'lmasa — bo'sh ro'yxat).</returns>
    public static IReadOnlyList<OcrParagraph> ToParagraphs(string? raw)
    {
        var result = new List<OcrParagraph>();
        if (string.IsNullOrEmpty(raw))
        {
            return result;
        }

        var current = new List<string>();
        foreach (string rawLine in raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            string line = Clean(rawLine);
            if (line.Trim().Length == 0)
            {
                Flush();
                continue;
            }

            current.Add(line);
        }

        Flush();
        return result;

        void Flush()
        {
            if (current.Count > 0)
            {
                result.Add(new OcrParagraph(current.ToArray()));
                current.Clear();
            }
        }
    }

    /// <summary>Xatboshilardagi belgilar soni (bo'shliqlarsiz) — "matn topildimi" degan savol uchun.</summary>
    /// <param name="paragraphs">Xatboshilar.</param>
    /// <returns>Belgilar soni.</returns>
    public static int CountVisibleChars(IEnumerable<OcrParagraph> paragraphs) =>
        paragraphs.SelectMany(p => p.Lines).Sum(l => l.Count(c => !char.IsWhiteSpace(c)));

    private static string Clean(string line)
    {
        var sb = new StringBuilder(line.Length);
        foreach (char c in line)
        {
            if (c == '\t' || !char.IsControl(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString().TrimEnd();
    }
}
