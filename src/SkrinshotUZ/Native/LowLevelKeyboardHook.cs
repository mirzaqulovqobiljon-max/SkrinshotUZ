using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Hotkeys;

namespace SkrinshotUZ.Native;

/// <summary>
/// WH_KEYBOARD_LL asosidagi zaxira usul. Faqat RegisterHotKey ishlamagan birikmalar uchun o'rnatiladi
/// va faqat o'shalarni yutadi — qolgan barcha tugmalar o'zgarishsiz o'tadi.
/// Qaror qabul qilish mantiqi <see cref="KeyboardHookFilter"/> da (unit-testlangan).
/// </summary>
internal sealed class LowLevelKeyboardHook : IKeyboardHook
{
    // Delegat maydonda saqlanadi: aks holda GC uni yig'ib oladi va Windows o'chgan funksiyani chaqiradi.
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hook;
    private KeyboardHookFilter? _filter;
    private SynchronizationContext? _sync;

    public LowLevelKeyboardHook()
    {
        _proc = HookCallback;
    }

    public event EventHandler<KeyboardHookPressedEventArgs>? Pressed;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public bool Install(IReadOnlyCollection<Hotkey> hotkeys)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        Uninstall();

        _filter = new KeyboardHookFilter(hotkeys);
        _sync = SynchronizationContext.Current;
        _hook = NativeMethods.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandleW(null), 0);
        return _hook != IntPtr.Zero;
    }

    public void Uninstall()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _filter = null;
    }

    public void Dispose() => Uninstall();

    private static bool IsDown(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static KeyModifiers CurrentModifiers(uint flags)
    {
        var m = KeyModifiers.None;
        if (IsDown(VirtualKeys.Control))
        {
            m |= KeyModifiers.Control;
        }

        if ((flags & NativeMethods.LLKHF_ALTDOWN) != 0 || IsDown(VirtualKeys.Alt))
        {
            m |= KeyModifiers.Alt;
        }

        if (IsDown(VirtualKeys.Shift))
        {
            m |= KeyModifiers.Shift;
        }

        if (IsDown(VirtualKeys.LeftWin) || IsDown(VirtualKeys.RightWin))
        {
            m |= KeyModifiers.Win;
        }

        return m;
    }

    /// <summary>
    /// Windows har bir tugma bosilishida chaqiradi. Juda tez qaytishi shart (aks holda Windows hook'ni o'chiradi),
    /// shuning uchun hodisa UI oqimiga navbat orqali yuboriladi.
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var filter = _filter;
        if (nCode >= 0 && filter is not null)
        {
            int msg = wParam.ToInt32();
            bool down = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            bool up = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
            if (down || up)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                var decision = filter.Process((int)data.VkCode, down, CurrentModifiers(data.Flags));

                if (decision.Fired is Hotkey fired)
                {
                    Raise(fired);
                }

                if (decision.Swallow)
                {
                    return new IntPtr(1);
                }
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void Raise(Hotkey hotkey)
    {
        var args = new KeyboardHookPressedEventArgs(hotkey);
        if (_sync is not null)
        {
            _sync.Post(_ => Pressed?.Invoke(this, args), null);
        }
        else
        {
            Pressed?.Invoke(this, args);
        }
    }
}
