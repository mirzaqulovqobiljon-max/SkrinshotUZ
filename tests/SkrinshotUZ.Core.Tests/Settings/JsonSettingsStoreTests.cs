using SkrinshotUZ.Core.Settings;

namespace SkrinshotUZ.Core.Tests.Settings;

/// <summary>Sozlamalar fayli (haqiqiy vaqtinchalik papkada).</summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.Tests." + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "Skrinshot UZ", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        var s = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(ConsentState.NotAsked, s.PrintScreenConsent);
        Assert.False(s.SnippingToolPromptAnswered);
        Assert.Equal(AppSettings.CurrentSchemaVersion, s.SchemaVersion);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_AndCreatesFolder()
    {
        var store = new JsonSettingsStore(SettingsPath);
        store.Save(new AppSettings { PrintScreenConsent = ConsentState.Allowed, SnippingToolPromptAnswered = true, SnippingToolDisabledByApp = true });

        var s = new JsonSettingsStore(SettingsPath).Load();
        Assert.Equal(ConsentState.Allowed, s.PrintScreenConsent);
        Assert.True(s.SnippingToolPromptAnswered);
        Assert.True(s.SnippingToolDisabledByApp);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void File_IsHumanReadable_EnumsAsText()
    {
        new JsonSettingsStore(SettingsPath).Save(new AppSettings { PrintScreenConsent = ConsentState.Denied });

        Assert.Contains("\"Denied\"", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Overwrite_ReplacesPreviousValues()
    {
        var store = new JsonSettingsStore(SettingsPath);
        store.Save(new AppSettings { PrintScreenConsent = ConsentState.Allowed });
        store.Save(new AppSettings { PrintScreenConsent = ConsentState.Denied });

        Assert.Equal(ConsentState.Denied, store.Load().PrintScreenConsent);
    }

    [Theory]
    [InlineData("{ bu json emas")]
    [InlineData("{\"PrintScreenConsent\": \"Balki\"}")]
    [InlineData("")]
    public void CorruptFile_ReturnsDefaults_AndKeepsBackup(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, content);

        var s = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(ConsentState.NotAsked, s.PrintScreenConsent);
        Assert.True(File.Exists(SettingsPath + ".bad"));
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void UnknownProperties_FromNewerVersion_AreIgnored()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{\"PrintScreenConsent\":\"Allowed\",\"KelajakdagiSozlama\":42}");

        Assert.Equal(ConsentState.Allowed, new JsonSettingsStore(SettingsPath).Load().PrintScreenConsent);
    }

    [Fact]
    public void JsonNull_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "null");

        Assert.Equal(ConsentState.NotAsked, new JsonSettingsStore(SettingsPath).Load().PrintScreenConsent);
    }
}
