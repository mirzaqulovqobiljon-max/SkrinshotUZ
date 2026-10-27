using System.Text;

namespace SkrinshotUZ.Core.Hotkeys;

/// <summary>
/// Modifikator tugmalar. Qiymatlar WinAPI RegisterHotKey'dagi MOD_* bilan aynan bir xil,
/// shuning uchun to'g'ridan-to'g'ri uzatiladi.
/// </summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>Modifikatorsiz.</summary>
    None = 0,

    /// <summary>Alt (MOD_ALT).</summary>
    Alt = 0x1,

    /// <summary>Ctrl (MOD_CONTROL).</summary>
    Control = 0x2,

    /// <summary>Shift (MOD_SHIFT).</summary>
    Shift = 0x4,

    /// <summary>Windows tugmasi (MOD_WIN).</summary>
    Win = 0x8,
}

/// <summary>Tugma bosilganda bajariladigan amal.</summary>
public enum HotkeyAction
{
    /// <summary>Ekran xiralashadi, soha tanlanadi (PrintScreen, Ctrl+Shift+S).</summary>
    CaptureRegion,

    /// <summary>Faol oyna skrinshoti (Alt+PrintScreen).</summary>
    CaptureActiveWindow,

    /// <summary>Butun ekranni darhol faylga saqlash (Shift+PrintScreen).</summary>
    CaptureFullScreenSave,
}

/// <summary>Kerakli virtual tugma kodlari (WinAPI VK_*).</summary>
public static class VirtualKeys
{
    /// <summary>VK_SNAPSHOT — PrintScreen.</summary>
    public const int PrintScreen = 0x2C;

    /// <summary>VK_S.</summary>
    public const int S = 0x53;

    /// <summary>VK_SHIFT.</summary>
    public const int Shift = 0x10;

    /// <summary>VK_CONTROL.</summary>
    public const int Control = 0x11;

    /// <summary>VK_MENU (Alt).</summary>
    public const int Alt = 0x12;

    /// <summary>VK_LSHIFT.</summary>
    public const int LeftShift = 0xA0;

    /// <summary>VK_RSHIFT.</summary>
    public const int RightShift = 0xA1;

    /// <summary>VK_LCONTROL.</summary>
    public const int LeftControl = 0xA2;

    /// <summary>VK_RCONTROL.</summary>
    public const int RightControl = 0xA3;

    /// <summary>VK_LMENU.</summary>
    public const int LeftAlt = 0xA4;

    /// <summary>VK_RMENU.</summary>
    public const int RightAlt = 0xA5;

    /// <summary>VK_LWIN.</summary>
    public const int LeftWin = 0x5B;

    /// <summary>VK_RWIN.</summary>
    public const int RightWin = 0x5C;

    /// <summary>Tugma modifikatormi (Shift/Ctrl/Alt/Win, chap yoki o'ng).</summary>
    /// <param name="vk">Virtual kod.</param>
    /// <returns>Modifikator bo'lsa <c>true</c>.</returns>
    public static bool IsModifier(int vk) => vk is Shift or Control or Alt
        or LeftShift or RightShift or LeftControl or RightControl or LeftAlt or RightAlt or LeftWin or RightWin;
}

/// <summary>Tugmalar birikmasi.</summary>
/// <param name="Modifiers">Modifikatorlar.</param>
/// <param name="VirtualKey">Asosiy tugma (VK_*).</param>
public readonly record struct Hotkey(KeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>PrintScreen.</summary>
    public static Hotkey PrintScreen { get; } = new(KeyModifiers.None, VirtualKeys.PrintScreen);

    /// <summary>Alt+PrintScreen.</summary>
    public static Hotkey AltPrintScreen { get; } = new(KeyModifiers.Alt, VirtualKeys.PrintScreen);

    /// <summary>Shift+PrintScreen.</summary>
    public static Hotkey ShiftPrintScreen { get; } = new(KeyModifiers.Shift, VirtualKeys.PrintScreen);

    /// <summary>Ctrl+Shift+S — PrintScreen'ga ruxsat berilmasa ham doim ishlaydi.</summary>
    public static Hotkey CtrlShiftS { get; } = new(KeyModifiers.Control | KeyModifiers.Shift, VirtualKeys.S);

    /// <summary>Foydalanuvchiga ko'rsatiladigan nom: "Ctrl+Shift+S", "Alt+PrintScreen".</summary>
    /// <returns>Matn.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(KeyModifiers.Win))
        {
            sb.Append("Win+");
        }

        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            sb.Append("Ctrl+");
        }

        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            sb.Append("Alt+");
        }

        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            sb.Append("Shift+");
        }

        sb.Append(VirtualKey switch
        {
            VirtualKeys.PrintScreen => "PrintScreen",
            >= 0x41 and <= 0x5A => ((char)VirtualKey).ToString(),
            >= 0x30 and <= 0x39 => ((char)VirtualKey).ToString(),
            _ => "0x" + VirtualKey.ToString("X2", System.Globalization.CultureInfo.InvariantCulture),
        });
        return sb.ToString();
    }
}
