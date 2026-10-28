using SkrinshotUZ.Core.Printing;
using SkrinshotUZ.Core.Settings;

namespace SkrinshotUZ.Core.Tests.Printing;

/// <summary>Chop etish sozlamalarini eslab qolish va noto'g'ri qiymatlarni tuzatish.</summary>
public sealed class PrintPreferencesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.Tests." + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Defaults_FullPageAutoTenMmOneCopy()
    {
        var p = new PrintPreferences();
        Assert.Equal(PrintMode.FullPage, p.Mode);
        Assert.Equal(OrientationChoice.Auto, p.Orientation);
        Assert.Equal(10, p.MarginMm);
        Assert.Equal(OverflowChoice.FitToPage, p.Overflow);
        Assert.False(p.Grayscale);
        Assert.False(p.ShowDate);
        Assert.Equal(1, p.Copies);
        Assert.Null(p.PrinterName);
    }

    [Theory]
    [InlineData(7.0, 5.0)]
    [InlineData(8.0, 10.0)]
    [InlineData(-3.0, 0.0)]
    [InlineData(100.0, 15.0)]
    [InlineData(12.5, 10.0)] // teng masofada — kichigi
    [InlineData(15.0, 15.0)]
    [InlineData(double.NaN, 10.0)]
    [InlineData(double.PositiveInfinity, 10.0)]
    public void Normalize_Margin_SnapsToAllowed(double input, double expected)
    {
        var p = new PrintPreferences { MarginMm = input };
        p.Normalize();
        Assert.Equal(expected, p.MarginMm);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    [InlineData(99, 99)]
    [InlineData(500, 99)]
    public void Normalize_Copies_Clamped(int input, int expected)
    {
        var p = new PrintPreferences { Copies = input };
        p.Normalize();
        Assert.Equal(expected, p.Copies);
    }

    [Fact]
    public void Normalize_UnknownEnums_AndBlankPrinter_ResetToDefaults()
    {
        var p = new PrintPreferences
        {
            Mode = (PrintMode)77,
            Orientation = (OrientationChoice)9,
            Overflow = (OverflowChoice)5,
            PrinterName = "   ",
        };

        p.Normalize();

        Assert.Equal(PrintMode.FullPage, p.Mode);
        Assert.Equal(OrientationChoice.Auto, p.Orientation);
        Assert.Equal(OverflowChoice.FitToPage, p.Overflow);
        Assert.Null(p.PrinterName);
    }

    [Fact]
    public void Normalize_KeepsValidValues()
    {
        var p = new PrintPreferences
        {
            Mode = PrintMode.TwoPerPage,
            Orientation = OrientationChoice.Landscape,
            Overflow = OverflowChoice.SplitPages,
            MarginMm = 5,
            Copies = 3,
            Grayscale = true,
            ShowDate = true,
            PrinterName = "HP LaserJet",
        };

        p.Normalize();

        Assert.Equal(PrintMode.TwoPerPage, p.Mode);
        Assert.Equal(OrientationChoice.Landscape, p.Orientation);
        Assert.Equal(OverflowChoice.SplitPages, p.Overflow);
        Assert.Equal(5, p.MarginMm);
        Assert.Equal(3, p.Copies);
        Assert.Equal("HP LaserJet", p.PrinterName);
    }

    [Fact]
    public void ToOptions_CopiesLayoutFields_AndCaption()
    {
        var p = new PrintPreferences { Mode = PrintMode.ActualSize, Orientation = OrientationChoice.Portrait, MarginMm = 15, Overflow = OverflowChoice.SplitPages };

        var o = p.ToOptions("Sarlavha");

        Assert.Equal(PrintMode.ActualSize, o.Mode);
        Assert.Equal(OrientationChoice.Portrait, o.Orientation);
        Assert.Equal(15, o.MarginMm);
        Assert.Equal(OverflowChoice.SplitPages, o.Overflow);
        Assert.Equal("Sarlavha", o.Caption);
    }

    [Fact]
    public void Settings_RoundTrip_PrintPreferences()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var s = new AppSettings();
        s.Print.Mode = PrintMode.HalfPage;
        s.Print.Grayscale = true;
        s.Print.Copies = 4;
        s.Print.PrinterName = "Microsoft Print to PDF";
        store.Save(s);

        var back = store.Load();

        Assert.Equal(PrintMode.HalfPage, back.Print.Mode);
        Assert.True(back.Print.Grayscale);
        Assert.Equal(4, back.Print.Copies);
        Assert.Equal("Microsoft Print to PDF", back.Print.PrinterName);
        Assert.Contains("\"HalfPage\"", File.ReadAllText(SettingsPath)); // enum matn ko'rinishida
    }

    [Theory]
    [InlineData("{\"PrintScreenConsent\":\"Allowed\"}")] // 5-bosqichgacha bo'lgan fayl
    [InlineData("{\"PrintScreenConsent\":\"Allowed\",\"Print\":null}")]
    public void Settings_OldOrNullPrint_GetsDefaults(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, json);

        var s = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(ConsentState.Allowed, s.PrintScreenConsent);
        Assert.NotNull(s.Print);
        Assert.Equal(PrintMode.FullPage, s.Print.Mode);
        Assert.Equal(1, s.Print.Copies);
    }

    [Fact]
    public void Settings_Load_NormalizesHandEditedValues()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{\"Print\":{\"MarginMm\":3,\"Copies\":0}}");

        var s = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(5, s.Print.MarginMm);
        Assert.Equal(1, s.Print.Copies);
    }

    [Fact]
    public void AppSettings_Clone_IsDeepForPrint()
    {
        var s = new AppSettings();
        var copy = s.Clone();

        copy.Print.Copies = 7;

        Assert.Equal(1, s.Print.Copies);
        Assert.NotSame(s.Print, copy.Print);
    }
}
