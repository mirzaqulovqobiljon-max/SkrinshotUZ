using SkrinshotUZ.Core.Hotkeys;
using SkrinshotUZ.Core.Tests.Fakes;

namespace SkrinshotUZ.Core.Tests.Hotkeys;

/// <summary>Hotkey boshqaruvi: ruxsat, RegisterHotKey → hook zaxirasi (mock bilan).</summary>
public class HotkeyManagerTests
{
    private readonly FakeHotkeyService _service = new();
    private readonly FakeKeyboardHook _hook = new();

    private HotkeyManager CreateManager() => new(_service, _hook);

    [Fact]
    public void WithoutPermission_OnlyCtrlShiftS_PrintScreenIsNeverTouched()
    {
        using var m = CreateManager();
        var status = m.Apply(printScreenAllowed: false);

        var s = Assert.Single(status);
        Assert.Equal(Hotkey.CtrlShiftS, s.Hotkey);
        Assert.Equal(HotkeyMode.RegisterHotKey, s.Mode);
        Assert.DoesNotContain(_service.Registered.Values, h => h.VirtualKey == VirtualKeys.PrintScreen);
        Assert.False(_hook.IsInstalled);
        Assert.Equal(0, _hook.InstallCalls);
    }

    [Fact]
    public void WithoutPermission_EvenIfCtrlShiftSIsTaken_HookDoesNotWatchPrintScreen()
    {
        _service.Taken.Add(Hotkey.CtrlShiftS);
        using var m = CreateManager();
        var status = m.Apply(printScreenAllowed: false);

        Assert.Equal(HotkeyMode.KeyboardHook, Assert.Single(status).Mode);
        Assert.Equal(new[] { Hotkey.CtrlShiftS }, _hook.Watched);
    }

    [Fact]
    public void WithPermission_AllFree_FourHotkeysViaRegisterHotKey_NoHook()
    {
        using var m = CreateManager();
        var status = m.Apply(printScreenAllowed: true);

        Assert.Equal(4, status.Count);
        Assert.Equal(new[] { Hotkey.CtrlShiftS, Hotkey.PrintScreen, Hotkey.AltPrintScreen, Hotkey.ShiftPrintScreen }, status.Select(s => s.Hotkey));
        Assert.Equal(new[] { HotkeyAction.CaptureRegion, HotkeyAction.CaptureRegion, HotkeyAction.CaptureActiveWindow, HotkeyAction.CaptureFullScreenSave }, status.Select(s => s.Action));
        Assert.DoesNotContain(status, s => s.Mode != HotkeyMode.RegisterHotKey);
        Assert.False(_hook.IsInstalled);
    }

    [Fact]
    public void PrintScreenTakenByAnotherApp_FallsBackToHook_OnlyForTakenKeys()
    {
        // Masalan, Windows 11 Qirqish vositasi PrintScreen'ni egallagan.
        _service.Taken.Add(Hotkey.PrintScreen);
        using var m = CreateManager();
        var status = m.Apply(printScreenAllowed: true);

        Assert.Equal(HotkeyMode.KeyboardHook, status.Single(s => s.Hotkey == Hotkey.PrintScreen).Mode);
        Assert.Equal(HotkeyMode.RegisterHotKey, status.Single(s => s.Hotkey == Hotkey.AltPrintScreen).Mode);
        Assert.Equal(new[] { Hotkey.PrintScreen }, _hook.Watched);
    }

    [Fact]
    public void BothMethodsFail_StatusIsFailed_NoException()
    {
        _service.Taken.Add(Hotkey.PrintScreen);
        _service.Taken.Add(Hotkey.ShiftPrintScreen);
        _hook.FailInstall = true;
        using var m = CreateManager();
        var status = m.Apply(printScreenAllowed: true);

        Assert.Equal(HotkeyMode.Failed, status.Single(s => s.Hotkey == Hotkey.PrintScreen).Mode);
        Assert.Equal(HotkeyMode.Failed, status.Single(s => s.Hotkey == Hotkey.ShiftPrintScreen).Mode);
        Assert.Equal(HotkeyMode.RegisterHotKey, status.Single(s => s.Hotkey == Hotkey.CtrlShiftS).Mode);
        Assert.Equal(4, status.Count);
    }

    [Theory]
    [InlineData(1, HotkeyAction.CaptureRegion)]
    [InlineData(2, HotkeyAction.CaptureRegion)]
    [InlineData(3, HotkeyAction.CaptureActiveWindow)]
    [InlineData(4, HotkeyAction.CaptureFullScreenSave)]
    public void RegisteredHotkeyPressed_RaisesMatchingAction(int id, HotkeyAction expected)
    {
        using var m = CreateManager();
        m.Apply(printScreenAllowed: true);
        var actions = new List<HotkeyAction>();
        m.ActionRequested += (_, e) => actions.Add(e.Action);

        _service.Press(id);

        Assert.Equal(new[] { expected }, actions);
    }

    [Fact]
    public void HookPressed_RaisesMatchingAction()
    {
        _service.Taken.Add(Hotkey.AltPrintScreen);
        using var m = CreateManager();
        m.Apply(printScreenAllowed: true);
        HotkeyActionEventArgs? got = null;
        m.ActionRequested += (_, e) => got = e;

        _hook.Press(Hotkey.AltPrintScreen);

        Assert.NotNull(got);
        Assert.Equal(HotkeyAction.CaptureActiveWindow, got.Action);
        Assert.Equal(Hotkey.AltPrintScreen, got.Hotkey);
    }

    [Fact]
    public void RevokingPermission_ReleasesPrintScreenImmediately()
    {
        _service.Taken.Add(Hotkey.PrintScreen);
        using var m = CreateManager();
        m.Apply(printScreenAllowed: true);
        Assert.True(_hook.IsInstalled);

        m.Apply(printScreenAllowed: false);

        Assert.False(_hook.IsInstalled);
        Assert.Equal(new[] { Hotkey.CtrlShiftS }, _service.Registered.Values);

        // Eski id'lar bosilsa ham (kechikkan xabar) hech narsa bo'lmaydi.
        int count = 0;
        m.ActionRequested += (_, _) => count++;
        _service.Press(2);
        _service.Press(3);
        _service.Press(4);
        _hook.Press(Hotkey.PrintScreen);
        Assert.Equal(0, count);
    }

    [Fact]
    public void RevokingPermission_OldRegisterHotKeyIdsNoLongerTrigger()
    {
        using var m = CreateManager();
        m.Apply(printScreenAllowed: true); // hammasi RegisterHotKey orqali
        m.Apply(printScreenAllowed: false);

        var actions = new List<HotkeyAction>();
        m.ActionRequested += (_, e) => actions.Add(e.Action);
        for (int id = 1; id <= 4; id++)
        {
            _service.Press(id);
        }

        Assert.Equal(new[] { HotkeyAction.CaptureRegion }, actions); // faqat Ctrl+Shift+S (id=1)
        Assert.Single(m.Status);
    }

    [Fact]
    public void ApplyTwice_DoesNotDoubleRegister()
    {
        using var m = CreateManager();
        m.Apply(printScreenAllowed: true);
        m.Apply(printScreenAllowed: true); // FakeHotkeyService takroriy id'da xato beradi

        Assert.Equal(4, _service.Registered.Count);
    }

    [Fact]
    public void Dispose_UnregistersEverything_AndStopsEvents()
    {
        _service.Taken.Add(Hotkey.PrintScreen);
        var m = CreateManager();
        m.Apply(printScreenAllowed: true);
        int count = 0;
        m.ActionRequested += (_, _) => count++;

        m.Dispose();
        _service.Press(1);
        _hook.Press(Hotkey.PrintScreen);

        Assert.Empty(_service.Registered);
        Assert.False(_hook.IsInstalled);
        Assert.Equal(0, count);
        Assert.Throws<ObjectDisposedException>(() => m.Apply(true));
        m.Dispose(); // ikkinchi Dispose xavfsiz
    }

    [Fact]
    public void GetBindings_IdsAreUniqueAndInWinApiRange()
    {
        var b = HotkeyManager.GetBindings(true);

        Assert.Equal(b.Count, b.Select(x => x.Id).Distinct().Count());
        Assert.DoesNotContain(b, x => x.Id < 0 || x.Id > 0xBFFF); // RegisterHotKey: ilova id'lari 0x0000–0xBFFF
    }
}
