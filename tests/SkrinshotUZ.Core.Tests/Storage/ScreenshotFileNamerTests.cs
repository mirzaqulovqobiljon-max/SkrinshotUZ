using System.Globalization;
using SkrinshotUZ.Core.Storage;

namespace SkrinshotUZ.Core.Tests.Storage;

/// <summary>Fayl nomi generatori.</summary>
public class ScreenshotFileNamerTests
{
    private static readonly DateTime Sample = new(2026, 10, 27, 20, 38, 15);

    [Fact]
    public void CreateFileName_Png_MatchesSpecification()
    {
        Assert.Equal("skrinshot_2026-10-27_20-38-15.png", ScreenshotFileNamer.CreateFileName(Sample, ImageFileFormat.Png));
    }

    [Fact]
    public void CreateFileName_Jpg_UsesJpgExtension()
    {
        Assert.Equal("skrinshot_2026-10-27_20-38-15.jpg", ScreenshotFileNamer.CreateFileName(Sample, ImageFileFormat.Jpg));
    }

    [Fact]
    public void CreateFileName_PadsSingleDigitsWithZero_And24HourClock()
    {
        Assert.Equal("skrinshot_2026-01-05_03-04-09.png", ScreenshotFileNamer.CreateFileName(new DateTime(2026, 1, 5, 3, 4, 9), ImageFileFormat.Png));
        Assert.Equal("skrinshot_2026-12-31_23-59-59.png", ScreenshotFileNamer.CreateFileName(new DateTime(2026, 12, 31, 23, 59, 59), ImageFileFormat.Png));
    }

    [Theory]
    [InlineData("uz-Latn-UZ")]
    [InlineData("uz-Cyrl-UZ")]
    [InlineData("ru-RU")]
    [InlineData("ar-SA")] // hijriy kalendar
    [InlineData("fa-IR")] // forscha raqamlar / Hijri-Shamsi
    [InlineData("th-TH")] // buddaviy kalendar
    public void CreateFileName_IsIndependentOfComputerCulture(string culture)
    {
        var saved = CultureInfo.CurrentCulture;
        var savedUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            Assert.Equal("skrinshot_2026-10-27_20-38-15.png", ScreenshotFileNamer.CreateFileName(Sample, ImageFileFormat.Png));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = savedUi;
        }
    }

    [Fact]
    public void CreateFileName_HasNoCharactersForbiddenOnWindows()
    {
        string name = ScreenshotFileNamer.CreateFileName(Sample, ImageFileFormat.Png);

        Assert.Equal(-1, name.IndexOfAny("<>:\"/\\|?*".ToCharArray()));
    }

    [Fact]
    public void CreateUniquePath_NoConflict_ReturnsBaseName()
    {
        string path = ScreenshotFileNamer.CreateUniquePath("C:\\Rasmlar", Sample, ImageFileFormat.Png, _ => false);

        Assert.Equal(Path.Combine("C:\\Rasmlar", "skrinshot_2026-10-27_20-38-15.png"), path);
    }

    [Fact]
    public void CreateUniquePath_SameSecond_AddsSequentialSuffix()
    {
        var existing = new HashSet<string>
        {
            Path.Combine("papka", "skrinshot_2026-10-27_20-38-15.png"),
            Path.Combine("papka", "skrinshot_2026-10-27_20-38-15_2.png"),
            Path.Combine("papka", "skrinshot_2026-10-27_20-38-15_3.png"),
        };

        string path = ScreenshotFileNamer.CreateUniquePath("papka", Sample, ImageFileFormat.Png, existing.Contains);

        Assert.Equal(Path.Combine("papka", "skrinshot_2026-10-27_20-38-15_4.png"), path);
    }

    [Fact]
    public void CreateUniquePath_PngAndJpgDoNotConflict()
    {
        var existing = new HashSet<string> { Path.Combine("p", "skrinshot_2026-10-27_20-38-15.png") };

        string path = ScreenshotFileNamer.CreateUniquePath("p", Sample, ImageFileFormat.Jpg, existing.Contains);

        Assert.Equal(Path.Combine("p", "skrinshot_2026-10-27_20-38-15.jpg"), path);
    }

    [Fact]
    public void CreateUniquePath_AllNamesTaken_ThrowsIOException()
    {
        Assert.Throws<IOException>(() => ScreenshotFileNamer.CreateUniquePath("p", Sample, ImageFileFormat.Png, _ => true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateUniquePath_EmptyFolder_Throws(string folder)
    {
        Assert.Throws<ArgumentException>(() => ScreenshotFileNamer.CreateUniquePath(folder, Sample, ImageFileFormat.Png, _ => false));
    }

    [Fact]
    public void GetExtension_UnknownFormat_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScreenshotFileNamer.GetExtension((ImageFileFormat)99));
    }
}
