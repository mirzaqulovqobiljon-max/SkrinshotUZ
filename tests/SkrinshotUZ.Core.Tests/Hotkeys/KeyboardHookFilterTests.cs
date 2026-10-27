using SkrinshotUZ.Core.Hotkeys;

namespace SkrinshotUZ.Core.Tests.Hotkeys;

/// <summary>Past darajali hook holat mashinasi.</summary>
public class KeyboardHookFilterTests
{
    private const int Ps = VirtualKeys.PrintScreen;

    private static KeyboardHookFilter AllPrintScreenKeys() =>
        new(new[] { Hotkey.PrintScreen, Hotkey.AltPrintScreen, Hotkey.ShiftPrintScreen });

    [Fact]
    public void PrintScreen_FiresOnceOnKeyDown_SwallowsRepeatsAndKeyUp()
    {
        var f = AllPrintScreenKeys();

        Assert.Equal(new HookDecision(true, Hotkey.PrintScreen), f.Process(Ps, true, KeyModifiers.None));
        Assert.Equal(new HookDecision(true, null), f.Process(Ps, true, KeyModifiers.None)); // avtomatik takror
        Assert.Equal(new HookDecision(true, null), f.Process(Ps, true, KeyModifiers.None));
        Assert.Equal(new HookDecision(true, null), f.Process(Ps, false, KeyModifiers.None));
    }

    [Fact]
    public void KeyUpOnly_PrintScreen_FiresOnKeyUp()
    {
        // Ba'zi klaviaturalar/drayverlar PrintScreen uchun faqat key-up yuboradi.
        var f = AllPrintScreenKeys();

        Assert.Equal(new HookDecision(true, Hotkey.PrintScreen), f.Process(Ps, false, KeyModifiers.None));
    }

    [Fact]
    public void TwoSeparatePresses_FireTwice()
    {
        var f = AllPrintScreenKeys();

        Assert.NotNull(f.Process(Ps, true, KeyModifiers.None).Fired);
        f.Process(Ps, false, KeyModifiers.None);
        Assert.NotNull(f.Process(Ps, true, KeyModifiers.None).Fired);
    }

    [Fact]
    public void AltPrintScreen_ReleasingAltFirst_DoesNotFireSecondTime()
    {
        var f = AllPrintScreenKeys();

        Assert.Equal(Hotkey.AltPrintScreen, f.Process(Ps, true, KeyModifiers.Alt).Fired);
        Assert.Equal(HookDecision.PassThrough, f.Process(VirtualKeys.LeftAlt, false, KeyModifiers.None));
        Assert.Equal(new HookDecision(true, null), f.Process(Ps, false, KeyModifiers.None)); // Alt'siz key-up — yutiladi, qayta ishga tushmaydi
    }

    [Fact]
    public void ShiftPrintScreen_IsRecognized()
    {
        Assert.Equal(Hotkey.ShiftPrintScreen, AllPrintScreenKeys().Process(Ps, true, KeyModifiers.Shift).Fired);
    }

    [Fact]
    public void UnwatchedCombination_ReleasingModifierFirst_DoesNotFireOnKeyUp()
    {
        // Regressiya: Ctrl+PrintScreen bizniki emas. Ctrl avval qo'yib yuborilsa, key-up "toza PrintScreen"
        // bo'lib ko'rinadi — u ishga tushmasligi va boshqa dasturga uzatilishi kerak.
        var f = AllPrintScreenKeys();

        Assert.Equal(HookDecision.PassThrough, f.Process(Ps, true, KeyModifiers.Control));
        Assert.Equal(HookDecision.PassThrough, f.Process(VirtualKeys.LeftControl, false, KeyModifiers.None));
        Assert.Equal(HookDecision.PassThrough, f.Process(Ps, false, KeyModifiers.None));
    }

    [Fact]
    public void WinPrintScreen_BelongsToWindows_IsNeverCaptured()
    {
        var f = AllPrintScreenKeys();

        Assert.Equal(HookDecision.PassThrough, f.Process(Ps, true, KeyModifiers.Win));
        Assert.Equal(HookDecision.PassThrough, f.Process(Ps, false, KeyModifiers.Win));
    }

    [Fact]
    public void OrdinaryTyping_PassesThrough()
    {
        var f = AllPrintScreenKeys();

        Assert.Equal(HookDecision.PassThrough, f.Process(0x41, true, KeyModifiers.None));
        Assert.Equal(HookDecision.PassThrough, f.Process(0x41, false, KeyModifiers.None));
        Assert.Equal(HookDecision.PassThrough, f.Process(VirtualKeys.LeftShift, true, KeyModifiers.Shift));
        Assert.Equal(HookDecision.PassThrough, f.Process(VirtualKeys.S, true, KeyModifiers.Control | KeyModifiers.Shift));
    }

    [Fact]
    public void OnlyWatchedHotkeysAreCaptured()
    {
        // Masalan, RegisterHotKey faqat Alt+PrintScreen uchun ishlamadi — hook faqat shuni kuzatadi.
        var f = new KeyboardHookFilter(new[] { Hotkey.AltPrintScreen });

        Assert.Equal(HookDecision.PassThrough, f.Process(Ps, true, KeyModifiers.None));
        Assert.Equal(HookDecision.PassThrough, f.Process(Ps, false, KeyModifiers.None));
        Assert.Equal(Hotkey.AltPrintScreen, f.Process(Ps, true, KeyModifiers.Alt).Fired);
    }

    [Fact]
    public void PassedThroughThenWatchedPress_StillFires()
    {
        var f = AllPrintScreenKeys();

        f.Process(Ps, true, KeyModifiers.Control);                  // bizniki emas
        Assert.Equal(Hotkey.PrintScreen, f.Process(Ps, true, KeyModifiers.None).Fired); // Ctrl qo'yib yuborildi, PrintScreen hali bosilgan — takror kelsa
        Assert.Equal(new HookDecision(true, null), f.Process(Ps, false, KeyModifiers.None));
    }

    [Fact]
    public void Hotkey_ToString_IsReadable()
    {
        Assert.Equal("PrintScreen", Hotkey.PrintScreen.ToString());
        Assert.Equal("Alt+PrintScreen", Hotkey.AltPrintScreen.ToString());
        Assert.Equal("Shift+PrintScreen", Hotkey.ShiftPrintScreen.ToString());
        Assert.Equal("Ctrl+Shift+S", Hotkey.CtrlShiftS.ToString());
        Assert.Equal("Win+Ctrl+Alt+Shift+0x7B", new Hotkey(KeyModifiers.Win | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, 0x7B).ToString());
    }

    [Fact]
    public void KeyModifiers_MatchWinApiModConstants()
    {
        // RegisterHotKey'ga to'g'ridan-to'g'ri uzatiladi: MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8.
        Assert.Equal(1, (int)KeyModifiers.Alt);
        Assert.Equal(2, (int)KeyModifiers.Control);
        Assert.Equal(4, (int)KeyModifiers.Shift);
        Assert.Equal(8, (int)KeyModifiers.Win);
        Assert.Equal(0x2C, VirtualKeys.PrintScreen);
    }
}
