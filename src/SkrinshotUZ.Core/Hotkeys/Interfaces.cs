namespace SkrinshotUZ.Core.Hotkeys;

/// <summary>Ro'yxatdan o'tgan tugma bosildi (RegisterHotKey → WM_HOTKEY).</summary>
public sealed class HotkeyPressedEventArgs : EventArgs
{
    /// <summary>Yangi hodisa.</summary>
    /// <param name="id">Ro'yxatdan o'tkazishdagi identifikator.</param>
    public HotkeyPressedEventArgs(int id) => Id = id;

    /// <summary>Identifikator.</summary>
    public int Id { get; }
}

/// <summary>Klaviatura hook'i tugma birikmasini ushladi.</summary>
public sealed class KeyboardHookPressedEventArgs : EventArgs
{
    /// <summary>Yangi hodisa.</summary>
    /// <param name="hotkey">Bosilgan birikma.</param>
    public KeyboardHookPressedEventArgs(Hotkey hotkey) => Hotkey = hotkey;

    /// <summary>Bosilgan birikma.</summary>
    public Hotkey Hotkey { get; }
}

/// <summary>
/// Tizim bo'yicha tugmalarni ro'yxatdan o'tkazish (Windows'da — RegisterHotKey).
/// Interfeys orqali — testlarda soxta (mock) amalga oshirish bilan almashtiriladi.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>Tugma bosildi.</summary>
    event EventHandler<HotkeyPressedEventArgs>? Pressed;

    /// <summary>Tugmani ro'yxatdan o'tkazishga urinadi.</summary>
    /// <param name="id">Identifikator.</param>
    /// <param name="hotkey">Birikma.</param>
    /// <returns>Muvaffaqiyatli bo'lsa <c>true</c>; boshqa dastur egallagan bo'lsa <c>false</c>.</returns>
    bool TryRegister(int id, Hotkey hotkey);

    /// <summary>Ro'yxatdan chiqaradi (ro'yxatda bo'lmasa — hech narsa qilmaydi).</summary>
    /// <param name="id">Identifikator.</param>
    void Unregister(int id);
}

/// <summary>
/// Past darajali klaviatura hook'i (Windows'da — WH_KEYBOARD_LL). RegisterHotKey ishlamaganda zaxira usul:
/// tugmani boshqa dasturdan oldin ushlaydi.
/// </summary>
public interface IKeyboardHook : IDisposable
{
    /// <summary>Kuzatilayotgan birikma bosildi.</summary>
    event EventHandler<KeyboardHookPressedEventArgs>? Pressed;

    /// <summary>Hook o'rnatilganmi.</summary>
    bool IsInstalled { get; }

    /// <summary>Hook'ni faqat shu birikmalar uchun o'rnatadi (avvalgisi olib tashlanadi).</summary>
    /// <param name="hotkeys">Birikmalar (bo'sh bo'lmasligi kerak).</param>
    /// <returns>Muvaffaqiyatli bo'lsa <c>true</c>.</returns>
    bool Install(IReadOnlyCollection<Hotkey> hotkeys);

    /// <summary>Hook'ni olib tashlaydi.</summary>
    void Uninstall();
}
