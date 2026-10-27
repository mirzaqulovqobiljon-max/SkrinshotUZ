using SkrinshotUZ.Core.Settings;
using SkrinshotUZ.Core.Storage;

namespace SkrinshotUZ.Core.Tests.Storage;

/// <summary>Saqlash formati va papkasi.</summary>
public class SaveOptionsTests
{
    [Theory]
    [InlineData(@"C:\a\dars.png", 2, ImageFileFormat.Png)]   // kengaytma filtrdan ustun
    [InlineData(@"C:\a\dars.JPG", 1, ImageFileFormat.Jpg)]
    [InlineData(@"C:\a\dars.jpeg", 1, ImageFileFormat.Jpg)]
    [InlineData(@"C:\a\dars", 2, ImageFileFormat.Jpg)]       // kengaytmasiz — filtr bo'yicha
    [InlineData(@"C:\a\dars", 1, ImageFileFormat.Png)]
    [InlineData(@"C:\a\dars.bmp", 2, ImageFileFormat.Jpg)]
    public void FormatFor_ExtensionThenFilter(string path, int filterIndex, ImageFileFormat expected)
    {
        Assert.Equal(expected, SaveOptions.FormatFor(path, filterIndex));
    }

    [Theory]
    [InlineData(@"C:\a\dars", ImageFileFormat.Png, @"C:\a\dars.png")]
    [InlineData(@"C:\a\dars.png", ImageFileFormat.Png, @"C:\a\dars.png")]
    [InlineData(@"C:\a\dars.JPEG", ImageFileFormat.Jpg, @"C:\a\dars.JPEG")]
    [InlineData(@"C:\a\dars.png", ImageFileFormat.Jpg, @"C:\a\dars.png.jpg")]
    [InlineData(@"C:\a\7-sinf.matematika", ImageFileFormat.Png, @"C:\a\7-sinf.matematika.png")]
    public void EnsureExtension(string path, ImageFileFormat format, string expected)
    {
        Assert.Equal(expected, SaveOptions.EnsureExtension(path, format));
    }

    [Fact]
    public void FilterIndex_MatchesDialogFilterOrder()
    {
        Assert.Equal(1, SaveOptions.FilterIndexFor(ImageFileFormat.Png));
        Assert.Equal(2, SaveOptions.FilterIndexFor(ImageFileFormat.Jpg));

        var parts = SaveOptions.DialogFilter.Split('|');
        Assert.Equal(4, parts.Length);
        Assert.EndsWith("*.png", parts[1], StringComparison.Ordinal);
        Assert.Contains("*.jpg", parts[3], StringComparison.Ordinal);
        Assert.Equal(ImageFileFormat.Png, SaveOptions.FormatFor("x", SaveOptions.FilterIndexFor(ImageFileFormat.Png)));
        Assert.Equal(ImageFileFormat.Jpg, SaveOptions.FormatFor("x", SaveOptions.FilterIndexFor(ImageFileFormat.Jpg)));
    }

    [Fact]
    public void ResolveFolder_LastUsedIfExists_ElsePicturesSkrinshotUz()
    {
        string pictures = @"C:\Users\Ustoz\Pictures";

        Assert.Equal(@"D:\Darslar", SaveOptions.ResolveFolder(@"D:\Darslar", pictures, _ => true));
        Assert.Equal(Path.Combine(pictures, "Skrinshot UZ"), SaveOptions.ResolveFolder(@"E:\fleshka", pictures, _ => false));
        Assert.Equal(Path.Combine(pictures, "Skrinshot UZ"), SaveOptions.ResolveFolder(null, pictures, _ => true));
        Assert.Equal(Path.Combine(pictures, "Skrinshot UZ"), SaveOptions.ResolveFolder("  ", pictures, _ => true));
    }

    [Fact]
    public void JpegQuality_KeepsTextReadable()
    {
        Assert.InRange(SaveOptions.JpegQuality, 85, 95);
    }

    [Fact]
    public void Settings_RememberFolderAndFormat_RoundTrip()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.Tests." + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(Path.Combine(dir, "settings.json"));
            store.Save(new AppSettings { LastSaveFolder = @"D:\Darslar", SaveFormat = ImageFileFormat.Jpg });

            var s = store.Load();
            Assert.Equal(@"D:\Darslar", s.LastSaveFolder);
            Assert.Equal(ImageFileFormat.Jpg, s.SaveFormat);
            Assert.Equal(ImageFileFormat.Png, new AppSettings().SaveFormat);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

/// <summary>Tarix: eng yangisi birinchi, takrorlar yo'q, chegaralar, yo'qolgan fayllar.</summary>
public sealed class ScreenshotHistoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.Tests." + Guid.NewGuid().ToString("N"));

    private static HistoryEntry E(string name, int minute = 0) =>
        new(@"C:\Rasmlar\" + name, new DateTime(2026, 10, 27, 20, minute, 0), 1280, 720);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void Add_NewestFirst()
    {
        var h = new ScreenshotHistory();
        h.Add(E("a.png", 1));
        h.Add(E("b.png", 2));

        Assert.Equal(new[] { E("b.png", 2), E("a.png", 1) }, h.Entries);
    }

    [Fact]
    public void Add_SamePathAgain_MovesToTop_NoDuplicate_CaseInsensitive()
    {
        var h = new ScreenshotHistory();
        h.Add(E("a.png", 1));
        h.Add(E("b.png", 2));
        h.Add(new HistoryEntry(@"C:\RASMLAR\A.PNG", new DateTime(2026, 10, 27, 21, 0, 0), 10, 10));

        Assert.Equal(2, h.Entries.Count);
        Assert.Equal(@"C:\RASMLAR\A.PNG", h.Entries[0].Path);
    }

    [Fact]
    public void Add_KeepsAtMostMaxEntries_DropsOldest()
    {
        var h = new ScreenshotHistory();
        for (int i = 0; i < ScreenshotHistory.MaxEntries + 7; i++)
        {
            h.Add(E($"s{i}.png"));
        }

        Assert.Equal(ScreenshotHistory.MaxEntries, h.Entries.Count);
        Assert.EndsWith($"s{ScreenshotHistory.MaxEntries + 6}.png", h.Entries[0].Path, StringComparison.Ordinal);
        Assert.EndsWith("s7.png", h.Entries[^1].Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Initial_DeduplicatedAndTrimmed_InvalidSkipped()
    {
        var initial = Enumerable.Range(0, 60).Select(i => E($"s{i}.png")).Prepend(E("s0.png")).Append(new HistoryEntry(" ", DateTime.Now, 1, 1));
        var h = new ScreenshotHistory(initial);

        Assert.Equal(ScreenshotHistory.MaxEntries, h.Entries.Count);
        Assert.Single(h.Entries, e => e.Path.EndsWith("s0.png", StringComparison.Ordinal));
    }

    [Fact]
    public void Remove_And_RemoveMissing()
    {
        var h = new ScreenshotHistory();
        h.Add(E("a.png"));
        h.Add(E("b.png"));
        h.Add(E("c.png"));

        Assert.True(h.Remove(@"c:\rasmlar\b.png"));
        Assert.False(h.Remove(@"C:\Rasmlar\yoq.png"));
        Assert.Equal(1, h.RemoveMissing(p => p.EndsWith("c.png", StringComparison.Ordinal)));
        Assert.Equal(new[] { E("c.png") }, h.Entries);
    }

    [Fact]
    public void Add_EmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ScreenshotHistory().Add(new HistoryEntry("", DateTime.Now, 1, 1)));
    }

    [Fact]
    public void DisplayText_DateTimeAndSize_CultureIndependent()
    {
        Assert.Equal("27.10.2026 20:38 · 1280×720", new HistoryEntry("x", new DateTime(2026, 10, 27, 20, 38, 15), 1280, 720).DisplayText);
    }

    [Fact]
    public void Store_RoundTrip()
    {
        var store = new JsonHistoryStore(Path.Combine(_dir, "history.json"));
        var h = new ScreenshotHistory();
        h.Add(E("a.png", 1));
        h.Add(E("Oʻquvchilar ishi.png", 2));
        store.Save(h);

        var loaded = store.Load();
        Assert.Equal(h.Entries, loaded.Entries);
    }

    [Fact]
    public void Store_MissingOrCorruptFile_EmptyHistory_CorruptBackedUp()
    {
        string path = Path.Combine(_dir, "history.json");
        Assert.Empty(new JsonHistoryStore(path).Load().Entries);

        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "[{ buzilgan");
        Assert.Empty(new JsonHistoryStore(path).Load().Entries);
        Assert.True(File.Exists(path + ".bad"));
    }

    [Fact]
    public void HistoryFile_IsNextToSettings()
    {
        Assert.Equal(Path.Combine("A", "Skrinshot UZ", "history.json"), AppPaths.HistoryFile("A"));
        Assert.Equal(Path.GetDirectoryName(AppPaths.SettingsFile("A")), Path.GetDirectoryName(AppPaths.HistoryFile("A")));
    }
}
