namespace SkrinshotUZ.Core.Hotkeys;

/// <summary>Hook qarori.</summary>
/// <param name="Swallow">Tugma boshqa dasturlarga uzatilmasin (PrintScreen Qirqish vositasini ochmasin).</param>
/// <param name="Fired">Ishga tushgan birikma (bo'lmasa <c>null</c>).</param>
public readonly record struct HookDecision(bool Swallow, Hotkey? Fired)
{
    /// <summary>O'tkazib yuborish (bizga tegishli emas).</summary>
    public static HookDecision PassThrough => default;
}

/// <summary>
/// Past darajali hook uchun holat mashinasi (Windows'ga bog'liq emas — to'liq testlanadi).
/// <list type="bullet">
/// <item>Birikma bosilganda (key-down) — bir marta ishga tushadi va tugma yutiladi.</item>
/// <item>Tugmani bosib turish (avtomatik takror) qayta ishga tushirmaydi.</item>
/// <item>Ba'zi klaviaturalar PrintScreen uchun faqat key-up yuboradi — key-down ko'rilmagan bo'lsa, key-up'da ishga tushadi.</item>
/// <item>Qo'yib yuborishda (key-up) modifikatorlar o'zgargan bo'lsa ham, mos key-up yutiladi.</item>
/// <item>Win tugmasi bilan birikmalar (Win+PrintScreen — Windows'ning o'zi) hech qachon ushlanmaydi.</item>
/// </list>
/// </summary>
public sealed class KeyboardHookFilter
{
    private readonly HashSet<Hotkey> _hotkeys;
    private readonly HashSet<int> _held = new();
    private readonly HashSet<int> _passedThrough = new();

    /// <summary>Yangi filtr.</summary>
    /// <param name="hotkeys">Kuzatiladigan birikmalar.</param>
    public KeyboardHookFilter(IEnumerable<Hotkey> hotkeys)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        _hotkeys = new HashSet<Hotkey>(hotkeys);
    }

    /// <summary>Bitta klaviatura hodisasini qayta ishlaydi.</summary>
    /// <param name="virtualKey">Tugma (VK_*).</param>
    /// <param name="isKeyDown">Bosildi (<c>true</c>) yoki qo'yib yuborildi (<c>false</c>).</param>
    /// <param name="modifiers">Shu paytda bosib turilgan modifikatorlar.</param>
    /// <returns>Qaror.</returns>
    public HookDecision Process(int virtualKey, bool isKeyDown, KeyModifiers modifiers)
    {
        if (VirtualKeys.IsModifier(virtualKey))
        {
            return HookDecision.PassThrough;
        }

        if (isKeyDown)
        {
            if (_held.Contains(virtualKey))
            {
                // Avtomatik takror: allaqachon ishga tushgan — faqat yutamiz.
                return new HookDecision(true, null);
            }

            if (_hotkeys.Contains(new Hotkey(modifiers, virtualKey)))
            {
                _held.Add(virtualKey);
                _passedThrough.Remove(virtualKey);
                return new HookDecision(true, new Hotkey(modifiers, virtualKey));
            }

            // Bizga tegishli bo'lmagan bosish (masalan, Ctrl+PrintScreen) — eslab qolamiz,
            // aks holda Ctrl avval qo'yib yuborilsa, key-up "toza PrintScreen" bo'lib ko'rinadi.
            _passedThrough.Add(virtualKey);
            return HookDecision.PassThrough;
        }

        if (_held.Remove(virtualKey))
        {
            return new HookDecision(true, null);
        }

        if (_passedThrough.Remove(virtualKey))
        {
            return HookDecision.PassThrough;
        }

        var hotkey = new Hotkey(modifiers, virtualKey);
        return _hotkeys.Contains(hotkey) ? new HookDecision(true, hotkey) : HookDecision.PassThrough;
    }
}
