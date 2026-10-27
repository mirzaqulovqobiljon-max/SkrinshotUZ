namespace SkrinshotUZ.Core.Platform;

/// <summary>Windows'ning "PrintScreen bilan Qirqish vositasini ochish" sozlamasi.</summary>
public interface ISnippingToolSetting
{
    /// <summary>Sozlama yoqilganmi (PrintScreen'ni Qirqish vositasi egallaganmi).</summary>
    /// <returns>Yoqilgan bo'lsa <c>true</c>.</returns>
    bool IsEnabled();

    /// <summary>O'chiradi. FAQAT foydalanuvchi aniq rozilik bergandan keyin chaqiriladi.</summary>
    void Disable();

    /// <summary>Qayta yoqadi. FAQAT foydalanuvchi aniq rozilik bergandan keyin chaqiriladi.</summary>
    void Enable();
}

/// <summary>
/// <c>HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled</c> (DWORD): 1 — yoqilgan, 0 — o'chirilgan.
/// Qiymat umuman yo'q bo'lsa, Windows standart qiymatni ishlatadi: Windows 11 (build ≥ 22000) da
/// so'nggi yangilanishlardan beri standart — yoqilgan, Windows 10 da — o'chirilgan.
/// </summary>
public sealed class SnippingToolSetting : ISnippingToolSetting
{
    /// <summary>Reyestr kaliti (HKCU ichida).</summary>
    public const string SubKey = @"Control Panel\Keyboard";

    /// <summary>Qiymat nomi.</summary>
    public const string ValueName = "PrintScreenKeyForSnippingEnabled";

    /// <summary>Windows 11 ning birinchi build raqami.</summary>
    public const int Windows11FirstBuild = 22000;

    private readonly IRegistryStore _registry;
    private readonly int _windowsBuild;

    /// <summary>Yangi obyekt.</summary>
    /// <param name="registry">Reyestr.</param>
    /// <param name="windowsBuild">Windows build raqami (<c>Environment.OSVersion.Version.Build</c>).</param>
    public SnippingToolSetting(IRegistryStore registry, int windowsBuild)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _windowsBuild = windowsBuild;
    }

    /// <inheritdoc />
    public bool IsEnabled() => _registry.GetValue(SubKey, ValueName) switch
    {
        int i => i != 0,
        long l => l != 0,
        null => _windowsBuild >= Windows11FirstBuild,
        _ => false, // kutilmagan tur — ehtiyotkorlik bilan "o'chirilgan" deb hisoblaymiz (foydalanuvchi bezovta qilinmaydi)
    };

    /// <inheritdoc />
    public void Disable() => _registry.SetDword(SubKey, ValueName, 0);

    /// <inheritdoc />
    public void Enable() => _registry.SetDword(SubKey, ValueName, 1);
}
