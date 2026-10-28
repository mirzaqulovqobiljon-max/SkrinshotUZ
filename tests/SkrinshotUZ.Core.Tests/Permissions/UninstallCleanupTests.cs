using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Permissions;
using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Settings;
using SkrinshotUZ.Core.Tests.Fakes;

namespace SkrinshotUZ.Core.Tests.Permissions;

/// <summary>
/// Dastur o'chirilayotganda: o'z avtoishga tushirish yozuvini olib tashlaydi; Qirqish vositasi sozlamasini faqat
/// so'rab (jim rejimda — umuman) qaytaradi. PrintScreen ruxsati so'ralmaydi.
/// </summary>
public class UninstallCleanupTests
{
    private const string ExePath = @"C:\Users\o'qituvchi\AppData\Local\Programs\Skrinshot UZ\SkrinshotUZ.exe";

    private readonly InMemorySettingsStore _store = new();
    private readonly FakePrompts _prompts = new();
    private readonly InMemoryRegistry _registry = new();

    private PermissionFlow CreateFlow() =>
        new(_store, _prompts, new SnippingToolSetting(_registry, 22631), new AutostartService(_registry, ExePath));

    private object? Snipping => _registry.GetValue(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName);

    private object? RunValue => _registry.GetValue(AutostartService.RunSubKey, AutostartService.ValueName);

    [Fact]
    public void RemovesAutostartEntry_WithoutAsking()
    {
        new AutostartService(_registry, ExePath).Enable();
        Assert.NotNull(RunValue);

        CreateFlow().CleanUpForUninstall(quiet: false);

        Assert.Null(RunValue);
        Assert.Empty(_prompts.PrintScreenAsked);
        Assert.Equal(0, _prompts.RestoreAsked); // Qirqish vositasini Skrinshot UZ o'chirmagan — savol yo'q
    }

    [Fact]
    public void NoAutostart_NoSnippingChange_TouchesNothing()
    {
        _registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = 0; // foydalanuvchi o'zi o'chirgan

        bool restored = CreateFlow().CleanUpForUninstall(quiet: false);

        Assert.False(restored);
        Assert.Empty(_registry.Writes);
        Assert.Equal(0, _prompts.RestoreAsked);
        Assert.Equal(0, Snipping);
    }

    [Fact]
    public void SnippingDisabledByApp_UserAgrees_Restored()
    {
        _registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = 0;
        _store.Stored = new AppSettings { PrintScreenConsent = ConsentState.Allowed, SnippingToolDisabledByApp = true };
        _prompts.RestoreSnippingAnswer = true;

        bool restored = CreateFlow().CleanUpForUninstall(quiet: false);

        Assert.True(restored);
        Assert.Equal(1, _prompts.RestoreAsked);
        Assert.Equal(1, Snipping);
    }

    [Fact]
    public void SnippingDisabledByApp_UserDeclines_Unchanged()
    {
        _registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = 0;
        _store.Stored = new AppSettings { SnippingToolDisabledByApp = true };
        _prompts.RestoreSnippingAnswer = false;

        bool restored = CreateFlow().CleanUpForUninstall(quiet: false);

        Assert.False(restored);
        Assert.Equal(1, _prompts.RestoreAsked);
        Assert.Equal(0, Snipping);
        Assert.Empty(_registry.Writes);
    }

    [Fact]
    public void Quiet_NeverAsks_NeverChangesSystemSetting_ButRemovesAutostart()
    {
        _registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = 0;
        new AutostartService(_registry, ExePath).Enable();
        _registry.Writes.Clear();
        _store.Stored = new AppSettings { SnippingToolDisabledByApp = true };
        _prompts.RestoreSnippingAnswer = true; // so'ralsa "ha" derdi — lekin so'ralmasligi kerak

        bool restored = CreateFlow().CleanUpForUninstall(quiet: true);

        Assert.False(restored);
        Assert.Equal(0, _prompts.RestoreAsked);
        Assert.Equal(0, Snipping);
        Assert.Null(RunValue);
    }

    [Fact]
    public void RegistryDenied_DoesNotThrow_WarnsOnlyForRestore()
    {
        _registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = 0;
        _registry.Values[(AutostartService.RunSubKey, AutostartService.ValueName)] = "\"" + ExePath + "\" --autostart";
        _registry.DenyWrites = true;
        _store.Stored = new AppSettings { SnippingToolDisabledByApp = true };
        _prompts.RestoreSnippingAnswer = true;

        bool restored = CreateFlow().CleanUpForUninstall(quiet: false);

        Assert.False(restored);
        Assert.Equal(new[] { Strings.Warning_SystemSettingDenied }, _prompts.Warnings);
    }
}
