namespace SkrinshotUZ.Core.Hotkeys;

/// <summary>Tugma qaysi usul bilan ishlayapti.</summary>
public enum HotkeyMode
{
    /// <summary>RegisterHotKey — asosiy usul.</summary>
    RegisterHotKey,

    /// <summary>Past darajali klaviatura hook'i — tugmani boshqa dastur egallagani uchun zaxira usul.</summary>
    KeyboardHook,

    /// <summary>Ikkala usul ham ishlamadi.</summary>
    Failed,
}

/// <summary>Bitta birikmaning holati.</summary>
/// <param name="Hotkey">Birikma.</param>
/// <param name="Action">Amal.</param>
/// <param name="Mode">Ishlash usuli.</param>
public sealed record HotkeyStatus(Hotkey Hotkey, HotkeyAction Action, HotkeyMode Mode);

/// <summary>Tugma bosilib, amal so'raldi.</summary>
public sealed class HotkeyActionEventArgs : EventArgs
{
    /// <summary>Yangi hodisa.</summary>
    /// <param name="action">Amal.</param>
    /// <param name="hotkey">Bosilgan birikma.</param>
    public HotkeyActionEventArgs(HotkeyAction action, Hotkey hotkey)
    {
        Action = action;
        Hotkey = hotkey;
    }

    /// <summary>Amal.</summary>
    public HotkeyAction Action { get; }

    /// <summary>Bosilgan birikma.</summary>
    public Hotkey Hotkey { get; }
}

/// <summary>
/// Tugmalarni boshqaradi:
/// <list type="number">
/// <item>Ctrl+Shift+S — doim (ruxsatdan qat'i nazar).</item>
/// <item>PrintScreen, Alt+PrintScreen, Shift+PrintScreen — FAQAT foydalanuvchi ruxsat bergan bo'lsa.</item>
/// <item>Avval RegisterHotKey; boshqa dastur egallagan bo'lsa — o'sha birikmalar uchun past darajali hook.</item>
/// </list>
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly IHotkeyService _hotkeys;
    private readonly IKeyboardHook _hook;
    private readonly Dictionary<int, HotkeyBinding> _registered = new();
    private readonly Dictionary<Hotkey, HotkeyAction> _hooked = new();
    private IReadOnlyList<HotkeyStatus> _status = Array.Empty<HotkeyStatus>();
    private bool _disposed;

    /// <summary>Yangi boshqaruvchi.</summary>
    /// <param name="hotkeys">RegisterHotKey xizmati.</param>
    /// <param name="hook">Zaxira hook.</param>
    public HotkeyManager(IHotkeyService hotkeys, IKeyboardHook hook)
    {
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
        _hotkeys.Pressed += OnHotkeyPressed;
        _hook.Pressed += OnHookPressed;
    }

    /// <summary>Foydalanuvchi tugma bosdi — amal bajarilishi kerak.</summary>
    public event EventHandler<HotkeyActionEventArgs>? ActionRequested;

    /// <summary>Oxirgi <see cref="Apply"/> natijasi.</summary>
    public IReadOnlyList<HotkeyStatus> Status => _status;

    /// <summary>Ruxsatga qarab qaysi birikmalar kerakligini qaytaradi.</summary>
    /// <param name="printScreenAllowed">PrintScreen tugmasiga ruxsat berilganmi.</param>
    /// <returns>Birikmalar ro'yxati (identifikator bilan).</returns>
    public static IReadOnlyList<HotkeyBinding> GetBindings(bool printScreenAllowed)
    {
        var list = new List<HotkeyBinding> { new(1, Hotkey.CtrlShiftS, HotkeyAction.CaptureRegion) };
        if (printScreenAllowed)
        {
            list.Add(new HotkeyBinding(2, Hotkey.PrintScreen, HotkeyAction.CaptureRegion));
            list.Add(new HotkeyBinding(3, Hotkey.AltPrintScreen, HotkeyAction.CaptureActiveWindow));
            list.Add(new HotkeyBinding(4, Hotkey.ShiftPrintScreen, HotkeyAction.CaptureFullScreenSave));
        }

        return list;
    }

    /// <summary>
    /// Tugmalarni (qayta) o'rnatadi. Avvalgi barcha ro'yxatdan o'tishlar va hook olib tashlanadi —
    /// shuning uchun ruxsat qaytarib olinsa, PrintScreen darhol bo'shatiladi.
    /// </summary>
    /// <param name="printScreenAllowed">PrintScreen tugmasiga ruxsat berilganmi.</param>
    /// <returns>Har bir birikmaning holati.</returns>
    public IReadOnlyList<HotkeyStatus> Apply(bool printScreenAllowed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReleaseAll();

        var status = new List<HotkeyStatus>();
        var needHook = new List<HotkeyBinding>();

        foreach (var binding in GetBindings(printScreenAllowed))
        {
            if (_hotkeys.TryRegister(binding.Id, binding.Hotkey))
            {
                _registered[binding.Id] = binding;
                status.Add(new HotkeyStatus(binding.Hotkey, binding.Action, HotkeyMode.RegisterHotKey));
            }
            else
            {
                needHook.Add(binding);
            }
        }

        if (needHook.Count > 0)
        {
            bool hooked = _hook.Install(needHook.Select(b => b.Hotkey).ToList());
            foreach (var binding in needHook)
            {
                if (hooked)
                {
                    _hooked[binding.Hotkey] = binding.Action;
                }

                status.Add(new HotkeyStatus(binding.Hotkey, binding.Action, hooked ? HotkeyMode.KeyboardHook : HotkeyMode.Failed));
            }
        }

        // Foydalanuvchiga tushunarli tartib: Ctrl+Shift+S, PrintScreen, Alt+..., Shift+...
        var order = GetBindings(printScreenAllowed).Select(b => b.Hotkey).ToList();
        _status = status.OrderBy(s => order.IndexOf(s.Hotkey)).ToList();
        return _status;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseAll();
        _hotkeys.Pressed -= OnHotkeyPressed;
        _hook.Pressed -= OnHookPressed;
        _disposed = true;
    }

    private void ReleaseAll()
    {
        foreach (int id in _registered.Keys)
        {
            _hotkeys.Unregister(id);
        }

        _registered.Clear();

        if (_hook.IsInstalled)
        {
            _hook.Uninstall();
        }

        _hooked.Clear();
        _status = Array.Empty<HotkeyStatus>();
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (_registered.TryGetValue(e.Id, out var binding))
        {
            ActionRequested?.Invoke(this, new HotkeyActionEventArgs(binding.Action, binding.Hotkey));
        }
    }

    private void OnHookPressed(object? sender, KeyboardHookPressedEventArgs e)
    {
        if (_hooked.TryGetValue(e.Hotkey, out var action))
        {
            ActionRequested?.Invoke(this, new HotkeyActionEventArgs(action, e.Hotkey));
        }
    }
}

/// <summary>Birikma va uning amali.</summary>
/// <param name="Id">RegisterHotKey identifikatori.</param>
/// <param name="Hotkey">Birikma.</param>
/// <param name="Action">Amal.</param>
public sealed record HotkeyBinding(int Id, Hotkey Hotkey, HotkeyAction Action);
