using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Permissions;
using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Settings;
using SkrinshotUZ.Core.Tests.Fakes;

namespace SkrinshotUZ.Core.Tests.Permissions;

/// <summary>
/// Ruxsat oqimi. Asosiy kafolat: tizim sozlamalari (reyestr) faqat foydalanuvchining aniq roziligidan keyin o'zgaradi.
/// </summary>
public class PermissionFlowTests
{
    private const string ExePath = @"C:\Program Files\Skrinshot UZ\SkrinshotUZ.exe";
    private const int Win11Build = 22631;
    private const int Win10Build = 19045;

    private readonly InMemorySettingsStore _store = new();
    private readonly FakePrompts _prompts = new();
    private readonly InMemoryRegistry _registry = new();

    private PermissionFlow CreateFlow(int windowsBuild = Win11Build) =>
        new(_store, _prompts, new SnippingToolSetting(_registry, windowsBuild), new AutostartService(_registry, ExePath));

    private void SetSnipping(int value) =>
        _registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = value;

    private object? SnippingValue => _registry.GetValue(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName);

    private bool AutostartWritten => _registry.GetValue(AutostartService.RunSubKey, AutostartService.ValueName) is not null;

    [Fact]
    public void FirstRun_AsksPrintScreenOnce_WithAutostartOption()
    {
        var flow = CreateFlow(Win10Build);
        flow.RunAtStartup();

        Assert.Equal(new[] { true }, _prompts.PrintScreenAsked);
    }

    [Fact]
    public void FirstRun_Allow_IsSaved_AndNotAskedAgainOnNextStart()
    {
        _prompts.PrintScreenAnswer = new PrintScreenAnswer(PermissionChoice.Allow, false);
        CreateFlow(Win10Build).RunAtStartup();

        Assert.Equal(ConsentState.Allowed, _store.Stored!.PrintScreenConsent);

        var next = CreateFlow(Win10Build);
        next.RunAtStartup();
        Assert.True(next.PrintScreenAllowed);
        Assert.Single(_prompts.PrintScreenAsked);
    }

    [Fact]
    public void FirstRun_Deny_IsSaved_SnippingNeverAsked_RegistryUntouched()
    {
        SetSnipping(1);
        _prompts.PrintScreenAnswer = new PrintScreenAnswer(PermissionChoice.Deny, false);
        var flow = CreateFlow();
        flow.RunAtStartup();

        Assert.False(flow.PrintScreenAllowed);
        Assert.Equal(ConsentState.Denied, _store.Stored!.PrintScreenConsent);
        Assert.Equal(0, _prompts.SnippingAsked);
        Assert.Empty(_registry.Writes);
    }

    [Fact]
    public void DialogDismissed_NothingSaved_NothingEnabled_AskedAgainNextTime()
    {
        SetSnipping(1);
        _prompts.PrintScreenAnswer = new PrintScreenAnswer(PermissionChoice.Dismissed, EnableAutostart: true);
        var flow = CreateFlow();
        flow.RunAtStartup();

        Assert.False(flow.PrintScreenAllowed);
        Assert.Null(_store.Stored);
        Assert.Empty(_registry.Writes); // belgi qo'yilgan bo'lsa ham — avtoishga tushirish yozilmaydi
        Assert.Equal(0, _prompts.SnippingAsked);

        CreateFlow().RunAtStartup();
        Assert.Equal(2, _prompts.PrintScreenAsked.Count);
    }

    [Theory]
    [InlineData(PermissionChoice.Allow)]
    [InlineData(PermissionChoice.Deny)]
    public void AutostartCheckbox_Checked_WritesRunKeyWithQuotedPath(PermissionChoice choice)
    {
        _prompts.PrintScreenAnswer = new PrintScreenAnswer(choice, EnableAutostart: true);
        var flow = CreateFlow(Win10Build);
        flow.RunAtStartup();

        Assert.Equal("\"" + ExePath + "\" --autostart", _registry.GetValue(AutostartService.RunSubKey, AutostartService.ValueName));
        Assert.True(flow.IsAutostartEnabled());
    }

    [Fact]
    public void AutostartCheckbox_Unchecked_RunKeyNotWritten()
    {
        _prompts.PrintScreenAnswer = new PrintScreenAnswer(PermissionChoice.Allow, EnableAutostart: false);
        CreateFlow(Win10Build).RunAtStartup();

        Assert.False(AutostartWritten);
    }

    [Fact]
    public void SnippingToolEnabled_UserAgrees_DisabledAndRemembered()
    {
        SetSnipping(1);
        _prompts.DisableSnippingAnswer = true;
        CreateFlow().RunAtStartup();

        Assert.Equal(0, SnippingValue);
        Assert.Equal(1, _prompts.SnippingAsked);
        Assert.Equal(1, _prompts.DisabledShown);
        Assert.True(_store.Stored!.SnippingToolDisabledByApp);
        Assert.True(_store.Stored.SnippingToolPromptAnswered);
    }

    [Fact]
    public void SnippingToolEnabled_UserRefuses_RegistryUntouched_NotAskedAgain()
    {
        SetSnipping(1);
        _prompts.DisableSnippingAnswer = false;
        CreateFlow().RunAtStartup();

        Assert.Equal(1, SnippingValue);
        Assert.Empty(_registry.Writes);
        Assert.False(_store.Stored!.SnippingToolDisabledByApp);

        CreateFlow().RunAtStartup();
        Assert.Equal(1, _prompts.SnippingAsked);
    }

    [Fact]
    public void Windows11_ValueMissing_TreatedAsEnabled_AndAsked()
    {
        _prompts.DisableSnippingAnswer = false;
        CreateFlow(Win11Build).RunAtStartup();

        Assert.Equal(1, _prompts.SnippingAsked);
    }

    [Fact]
    public void Windows10_ValueMissing_NotAsked()
    {
        CreateFlow(Win10Build).RunAtStartup();

        Assert.Equal(0, _prompts.SnippingAsked);
    }

    [Fact]
    public void SnippingAlreadyDisabled_NotAsked()
    {
        SetSnipping(0);
        CreateFlow().RunAtStartup();

        Assert.Equal(0, _prompts.SnippingAsked);
    }

    [Fact]
    public void AdministratorRestriction_ShowsWarning_DoesNotCrash_DoesNotClaimSuccess()
    {
        SetSnipping(1);
        _registry.DenyWrites = true;
        _prompts.DisableSnippingAnswer = true;
        _prompts.PrintScreenAnswer = new PrintScreenAnswer(PermissionChoice.Allow, EnableAutostart: true);
        CreateFlow().RunAtStartup();

        Assert.Equal(1, SnippingValue);
        Assert.False(_store.Stored!.SnippingToolDisabledByApp);
        Assert.Equal(0, _prompts.DisabledShown);
        Assert.Equal(2, _prompts.Warnings.Count); // avtoishga tushirish + Qirqish vositasi
        Assert.Equal(Strings.Warning_SystemSettingDenied, _prompts.Warnings[0]);
    }

    [Fact]
    public void SettingsCannotBeSaved_WarnsOnlyOnce()
    {
        SetSnipping(1);
        _store.FailSave = true;
        _prompts.DisableSnippingAnswer = true;
        var flow = CreateFlow();
        flow.RunAtStartup();
        flow.RevokePrintScreen();

        Assert.True(_store.SaveCalls >= 3);
        Assert.Equal(new[] { Strings.Warning_SettingsNotSaved }, _prompts.Warnings);
    }

    [Fact]
    public void AlreadyDecided_StartupShowsNoDialogs()
    {
        _store.Stored = new AppSettings { PrintScreenConsent = ConsentState.Allowed, SnippingToolPromptAnswered = true };
        SetSnipping(1);
        CreateFlow().RunAtStartup();

        Assert.Empty(_prompts.PrintScreenAsked);
        Assert.Equal(0, _prompts.SnippingAsked);
    }

    [Fact]
    public void RequestPrintScreenFromMenu_AsksWithoutAutostartOption()
    {
        _store.Stored = new AppSettings { PrintScreenConsent = ConsentState.Denied };
        _prompts.PrintScreenAnswer = new PrintScreenAnswer(PermissionChoice.Allow, EnableAutostart: true);
        var flow = CreateFlow(Win10Build);

        Assert.True(flow.RequestPrintScreen());
        Assert.Equal(new[] { false }, _prompts.PrintScreenAsked);
        Assert.False(AutostartWritten);
        Assert.Equal(ConsentState.Allowed, _store.Stored!.PrintScreenConsent);
    }

    [Fact]
    public void Revoke_WhenAppHadDisabledSnipping_AsksAndRestoresOnYes()
    {
        SetSnipping(0);
        _store.Stored = new AppSettings { PrintScreenConsent = ConsentState.Allowed, SnippingToolPromptAnswered = true, SnippingToolDisabledByApp = true };
        _prompts.RestoreSnippingAnswer = true;
        var flow = CreateFlow();

        flow.RevokePrintScreen();

        Assert.Equal(1, _prompts.RestoreAsked);
        Assert.Equal(1, SnippingValue);
        Assert.False(_store.Stored!.SnippingToolDisabledByApp);
        Assert.Equal(ConsentState.Denied, _store.Stored.PrintScreenConsent);
    }

    [Fact]
    public void Revoke_UserSaysNo_SnippingStaysDisabled()
    {
        SetSnipping(0);
        _store.Stored = new AppSettings { PrintScreenConsent = ConsentState.Allowed, SnippingToolDisabledByApp = true };
        _prompts.RestoreSnippingAnswer = false;
        CreateFlow().RevokePrintScreen();

        Assert.Equal(0, SnippingValue);
        Assert.Empty(_registry.Writes);
        Assert.True(_store.Stored!.SnippingToolDisabledByApp);
    }

    [Fact]
    public void Revoke_WhenUserChangedSnippingThemselves_DoesNotAsk()
    {
        _store.Stored = new AppSettings { PrintScreenConsent = ConsentState.Allowed };
        CreateFlow().RevokePrintScreen();

        Assert.Equal(0, _prompts.RestoreAsked);
        Assert.Empty(_registry.Writes);
    }

    [Fact]
    public void MenuSnippingFix_AsksEvenIfAnsweredBefore()
    {
        SetSnipping(1);
        _store.Stored = new AppSettings { PrintScreenConsent = ConsentState.Allowed, SnippingToolPromptAnswered = true };
        _prompts.DisableSnippingAnswer = true;
        CreateFlow().OfferSnippingToolFix();

        Assert.Equal(1, _prompts.SnippingAsked);
        Assert.Equal(0, SnippingValue);
    }

    [Fact]
    public void SetAutostart_OnAndOff()
    {
        var flow = CreateFlow();

        Assert.True(flow.SetAutostart(true));
        Assert.True(flow.IsAutostartEnabled());
        Assert.True(flow.SetAutostart(false));
        Assert.False(flow.IsAutostartEnabled());
        Assert.False(AutostartWritten);
    }

    [Fact]
    public void NoScenario_WritesSnippingKeyWithoutExplicitYes()
    {
        // Barcha "yo'q/yopildi" kombinatsiyalarida Qirqish vositasi kaliti bir marta ham yozilmasligi kerak.
        foreach (var choice in new[] { PermissionChoice.Allow, PermissionChoice.Deny, PermissionChoice.Dismissed })
        {
            var registry = new InMemoryRegistry();
            registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = 1;
            var prompts = new FakePrompts { PrintScreenAnswer = new PrintScreenAnswer(choice, false), DisableSnippingAnswer = false, RestoreSnippingAnswer = false };
            var flow = new PermissionFlow(new InMemorySettingsStore(), prompts, new SnippingToolSetting(registry, Win11Build), new AutostartService(registry, ExePath));

            flow.RunAtStartup();
            flow.RequestPrintScreen();
            flow.RevokePrintScreen();

            Assert.DoesNotContain(registry.Writes, w => w.Contains(SnippingToolSetting.ValueName, StringComparison.Ordinal));
        }
    }
}
