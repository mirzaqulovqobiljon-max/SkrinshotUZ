using SkrinshotUZ.Core.Hotkeys;

namespace SkrinshotUZ.Native;

/// <summary>
/// RegisterHotKey asosidagi <see cref="IHotkeyService"/>. WM_HOTKEY xabarlarini ko'rinmas
/// "faqat xabar" oynasi (HWND_MESSAGE) qabul qiladi.
/// </summary>
internal sealed class WindowsHotkeyService : NativeWindow, IHotkeyService
{
    private readonly HashSet<int> _ids = new();
    private bool _disposed;

    public WindowsHotkeyService()
    {
        CreateHandle(new CreateParams { Caption = "SkrinshotUZ.Hotkeys", Parent = NativeMethods.HWND_MESSAGE });
    }

    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    public bool TryRegister(int id, Hotkey hotkey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unregister(id);

        // MOD_NOREPEAT: tugmani bosib turish takroriy WM_HOTKEY bermaydi.
        bool ok = NativeMethods.RegisterHotKey(Handle, id, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)hotkey.VirtualKey);
        if (ok)
        {
            _ids.Add(id);
        }

        return ok;
    }

    public void Unregister(int id)
    {
        if (_ids.Remove(id))
        {
            NativeMethods.UnregisterHotKey(Handle, id);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (int id in _ids.ToList())
        {
            Unregister(id);
        }

        DestroyHandle();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            Pressed?.Invoke(this, new HotkeyPressedEventArgs(m.WParam.ToInt32()));
            return;
        }

        base.WndProc(ref m);
    }
}
