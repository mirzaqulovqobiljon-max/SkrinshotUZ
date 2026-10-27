namespace SkrinshotUZ.Core.Settings;

/// <summary>Foydalanuvchi roziligi holati.</summary>
public enum ConsentState
{
    /// <summary>Hali so'ralmagan (yoki oyna yopib yuborilgan) — keyingi ishga tushishda yana so'raladi.</summary>
    NotAsked,

    /// <summary>Ruxsat berilgan.</summary>
    Allowed,

    /// <summary>Rad etilgan.</summary>
    Denied,
}

/// <summary>
/// Dastur sozlamalari (<c>%APPDATA%\Skrinshot UZ\settings.json</c>).
/// "Windows bilan birga ishga tushirish" bu yerda saqlanmaydi — uning yagona manbai reyestrdagi Run kaliti.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Joriy sxema versiyasi (keyinchalik migratsiya uchun).</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Sxema versiyasi.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>PrintScreen tugmasidan foydalanishga rozilik.</summary>
    public ConsentState PrintScreenConsent { get; set; } = ConsentState.NotAsked;

    /// <summary>Qirqish vositasi haqidagi savolga javob berilganmi (har safar qayta so'ralmasin).</summary>
    public bool SnippingToolPromptAnswered { get; set; }

    /// <summary>
    /// Qirqish vositasining PrintScreen sozlamasini aynan Skrinshot UZ o'chirganmi
    /// (ruxsat qaytarib olinsa yoki dastur o'chirilsa — qayta yoqishni taklif qilish uchun).
    /// </summary>
    public bool SnippingToolDisabledByApp { get; set; }

    /// <summary>Nusxa.</summary>
    /// <returns>Yangi obyekt.</returns>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
