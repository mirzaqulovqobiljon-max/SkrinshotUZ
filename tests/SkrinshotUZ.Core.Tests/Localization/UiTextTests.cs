using SkrinshotUZ.Core.Hotkeys;
using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Core.Tests.Localization;

public class UiTextTests
{
    [Fact]
    public void StatusReport_ListsEveryHotkeyWithActionAndMode()
    {
        var statuses = new[]
        {
            new HotkeyStatus(Hotkey.CtrlShiftS, HotkeyAction.CaptureRegion, HotkeyMode.RegisterHotKey),
            new HotkeyStatus(Hotkey.PrintScreen, HotkeyAction.CaptureRegion, HotkeyMode.KeyboardHook),
            new HotkeyStatus(Hotkey.ShiftPrintScreen, HotkeyAction.CaptureFullScreenSave, HotkeyMode.Failed),
        };

        string text = UiText.HotkeyStatusReport(statuses, printScreenAllowed: true);
        string[] lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        Assert.Equal(3, lines.Length);
        Assert.Equal("• Ctrl+Shift+S — Soha tanlash: ishlayapti", lines[0]);
        Assert.StartsWith("• PrintScreen — Soha tanlash: ishlayapti (zaxira usul", lines[1], StringComparison.Ordinal);
        Assert.Equal("• Shift+PrintScreen — Butun ekranni darhol saqlash: ishlamayapti — tugmani boshqa dastur egallagan", lines[2]);
    }

    [Fact]
    public void StatusReport_WithoutPermission_ExplainsPrintScreen()
    {
        var statuses = new[] { new HotkeyStatus(Hotkey.CtrlShiftS, HotkeyAction.CaptureRegion, HotkeyMode.RegisterHotKey) };

        string text = UiText.HotkeyStatusReport(statuses, printScreenAllowed: false);

        Assert.EndsWith("• PrintScreen — ruxsat berilmagan (menyudan yoqish mumkin)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedWarning_NullWhenAllWork_ListsFailedOtherwise()
    {
        var ok = new[] { new HotkeyStatus(Hotkey.PrintScreen, HotkeyAction.CaptureRegion, HotkeyMode.KeyboardHook) };
        Assert.Null(UiText.FailedHotkeysWarning(ok));

        var bad = new[]
        {
            new HotkeyStatus(Hotkey.PrintScreen, HotkeyAction.CaptureRegion, HotkeyMode.Failed),
            new HotkeyStatus(Hotkey.AltPrintScreen, HotkeyAction.CaptureActiveWindow, HotkeyMode.Failed),
        };
        string? warning = UiText.FailedHotkeysWarning(bad);
        Assert.NotNull(warning);
        Assert.Contains("PrintScreen, Alt+PrintScreen", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryActionAndMode_HasText()
    {
        foreach (var a in Enum.GetValues<HotkeyAction>())
        {
            Assert.False(string.IsNullOrWhiteSpace(UiText.ActionName(a)));
        }

        foreach (var m in Enum.GetValues<HotkeyMode>())
        {
            Assert.False(string.IsNullOrWhiteSpace(UiText.ModeName(m)));
        }
    }
}
