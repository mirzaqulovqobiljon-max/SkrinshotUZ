using System.Globalization;
using SkrinshotUZ.Core.Settings;

namespace SkrinshotUZ.Core.Storage;

/// <summary>Tarixdagi bitta saqlangan skrinshot.</summary>
/// <param name="Path">Fayl yo'li.</param>
/// <param name="SavedAt">Saqlangan vaqt (mahalliy).</param>
/// <param name="Width">Kenglik, px.</param>
/// <param name="Height">Balandlik, px.</param>
public sealed record HistoryEntry(string Path, DateTime SavedAt, int Width, int Height)
{
    /// <summary>Tarix oynasidagi yozuv: "27.10.2026 20:38 · 1280×720".</summary>
    public string DisplayText =>
        SavedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " · " +
        Width.ToString(CultureInfo.InvariantCulture) + "×" + Height.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Oxirgi saqlangan skrinshotlar (eng yangisi birinchi). Faqat fayl yo'llari saqlanadi — rasmlarning o'zi
/// foydalanuvchi papkasida qoladi; hech narsa internetga chiqmaydi.
/// </summary>
public sealed class ScreenshotHistory
{
    /// <summary>Tarixda saqlanadigan eng ko'p yozuvlar soni.</summary>
    public const int MaxEntries = 50;

    private readonly List<HistoryEntry> _entries = new();

    /// <summary>Yangi tarix.</summary>
    /// <param name="initial">Avvalgi yozuvlar (eng yangisi birinchi).</param>
    public ScreenshotHistory(IEnumerable<HistoryEntry>? initial = null)
    {
        if (initial is null)
        {
            return;
        }

        foreach (var e in initial)
        {
            if (!string.IsNullOrWhiteSpace(e.Path) && !_entries.Exists(x => SamePath(x.Path, e.Path)))
            {
                _entries.Add(e);
            }
        }

        Trim();
    }

    /// <summary>Yozuvlar (eng yangisi birinchi).</summary>
    public IReadOnlyList<HistoryEntry> Entries => _entries;

    /// <summary>Yozuv qo'shadi (o'sha fayl avval bo'lsa — yuqoriga ko'chadi).</summary>
    /// <param name="entry">Yozuv.</param>
    public void Add(HistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Path);
        _entries.RemoveAll(x => SamePath(x.Path, entry.Path));
        _entries.Insert(0, entry);
        Trim();
    }

    /// <summary>Yozuvni olib tashlaydi (faylning o'zi o'chirilmaydi).</summary>
    /// <param name="path">Fayl yo'li.</param>
    /// <returns>Olib tashlangan bo'lsa <c>true</c>.</returns>
    public bool Remove(string path) => _entries.RemoveAll(x => SamePath(x.Path, path)) > 0;

    /// <summary>Diskdan o'chirilgan yoki ko'chirilgan fayllarni ro'yxatdan olib tashlaydi.</summary>
    /// <param name="fileExists">Fayl mavjudligini tekshiruvchi (testda almashtiriladi).</param>
    /// <returns>Olib tashlanganlar soni.</returns>
    public int RemoveMissing(Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        return _entries.RemoveAll(x => !fileExists(x.Path));
    }

    /// <summary>Windows'da fayl yo'llari katta-kichik harfni farqlamaydi.</summary>
    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void Trim()
    {
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }
    }
}

/// <summary>Tarix fayli: <c>%APPDATA%\Skrinshot UZ\history.json</c>.</summary>
public sealed class JsonHistoryStore
{
    private readonly string _path;

    /// <summary>Yangi ombor.</summary>
    /// <param name="path">history.json to'liq yo'li.</param>
    public JsonHistoryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>O'qiydi (fayl yo'q yoki buzilgan bo'lsa — bo'sh tarix).</summary>
    /// <returns>Tarix.</returns>
    public ScreenshotHistory Load() => new(JsonFile.Read<List<HistoryEntry>>(_path, () => new List<HistoryEntry>()));

    /// <summary>Yozadi.</summary>
    /// <param name="history">Tarix.</param>
    public void Save(ScreenshotHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        JsonFile.Write(_path, history.Entries.ToList());
    }
}
