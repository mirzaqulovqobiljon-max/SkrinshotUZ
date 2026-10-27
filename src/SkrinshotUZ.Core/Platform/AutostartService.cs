namespace SkrinshotUZ.Core.Platform;

/// <summary>"Windows bilan birga ishga tushirish".</summary>
public interface IAutostartService
{
    /// <summary>Hozirgi .exe uchun yoqilganmi.</summary>
    /// <returns>Yoqilgan bo'lsa <c>true</c>.</returns>
    bool IsEnabled();

    /// <summary>Yoqadi. FAQAT foydalanuvchi roziligi bilan chaqiriladi.</summary>
    void Enable();

    /// <summary>O'chiradi.</summary>
    void Disable();
}

/// <summary>
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> → "Skrinshot UZ" = <c>"…\SkrinshotUZ.exe" --autostart</c>.
/// Faqat joriy foydalanuvchi uchun; administrator huquqi kerak emas.
/// </summary>
public sealed class AutostartService : IAutostartService
{
    /// <summary>Run kaliti.</summary>
    public const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Qiymat nomi.</summary>
    public const string ValueName = "Skrinshot UZ";

    private readonly IRegistryStore _registry;
    private readonly string _exePath;

    /// <summary>Yangi xizmat.</summary>
    /// <param name="registry">Reyestr.</param>
    /// <param name="exePath">SkrinshotUZ.exe to'liq yo'li.</param>
    public AutostartService(IRegistryStore registry, string exePath)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        _exePath = exePath;
    }

    /// <summary>Reyestrga yoziladigan buyruq (yo'lda bo'sh joy bo'lsa ham to'g'ri ishlashi uchun qo'shtirnoqda).</summary>
    public string CommandLine => "\"" + _exePath + "\" --autostart";

    /// <summary>
    /// Yozuv bor VA aynan shu .exe'ga ishora qiladi. Dastur boshqa papkaga ko'chirilgan bo'lsa — eski yozuv
    /// hisobga olinmaydi (menyuda belgisiz ko'rinadi, yoqilganda to'g'ri yo'l bilan qayta yoziladi).
    /// </summary>
    /// <returns>Yoqilgan bo'lsa <c>true</c>.</returns>
    public bool IsEnabled() =>
        _registry.GetValue(RunSubKey, ValueName) is string value &&
        string.Equals(value.Trim(), CommandLine, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Enable() => _registry.SetString(RunSubKey, ValueName, CommandLine);

    /// <inheritdoc />
    public void Disable() => _registry.DeleteValue(RunSubKey, ValueName);
}
