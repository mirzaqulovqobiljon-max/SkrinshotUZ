using System.Globalization;
using System.Text;
using SkrinshotUZ.Core.Hotkeys;

namespace SkrinshotUZ.Core.Localization;

/// <summary>Resurslardan murakkabroq matnlar yig'ish (testlanadigan qilib, WinForms'dan tashqarida).</summary>
public static class UiText
{
    /// <summary>Amal nomi.</summary>
    /// <param name="action">Amal.</param>
    /// <returns>Matn.</returns>
    public static string ActionName(HotkeyAction action) => action switch
    {
        HotkeyAction.CaptureRegion => Strings.Action_CaptureRegion,
        HotkeyAction.CaptureActiveWindow => Strings.Action_CaptureActiveWindow,
        HotkeyAction.CaptureFullScreenSave => Strings.Action_CaptureFullScreenSave,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    /// <summary>Ishlash usuli nomi.</summary>
    /// <param name="mode">Usul.</param>
    /// <returns>Matn.</returns>
    public static string ModeName(HotkeyMode mode) => mode switch
    {
        HotkeyMode.RegisterHotKey => Strings.HotkeyMode_RegisterHotKey,
        HotkeyMode.KeyboardHook => Strings.HotkeyMode_KeyboardHook,
        HotkeyMode.Failed => Strings.HotkeyMode_Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>"Tugmalar holati" oynasi uchun matn.</summary>
    /// <param name="statuses">Holatlar.</param>
    /// <param name="printScreenAllowed">PrintScreen'ga ruxsat berilganmi.</param>
    /// <returns>Ko'p qatorli matn.</returns>
    public static string HotkeyStatusReport(IEnumerable<HotkeyStatus> statuses, bool printScreenAllowed)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        var sb = new StringBuilder();
        foreach (var s in statuses)
        {
            sb.Append("• ").Append(s.Hotkey).Append(" — ").Append(ActionName(s.Action)).Append(": ").AppendLine(ModeName(s.Mode));
        }

        if (!printScreenAllowed)
        {
            sb.Append("• ").Append(Hotkey.PrintScreen).Append(" — ").AppendLine(Strings.HotkeyStatus_PrintScreenNotAllowed);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Ishlamay qolgan tugmalar haqida ogohlantirish (hammasi ishlasa — <c>null</c>).</summary>
    /// <param name="statuses">Holatlar.</param>
    /// <returns>Matn yoki <c>null</c>.</returns>
    public static string? FailedHotkeysWarning(IEnumerable<HotkeyStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        var failed = statuses.Where(s => s.Mode == HotkeyMode.Failed).Select(s => s.Hotkey.ToString()).ToList();
        return failed.Count == 0
            ? null
            : string.Format(CultureInfo.CurrentCulture, Strings.Warning_HotkeysFailed, string.Join(", ", failed));
    }
}
