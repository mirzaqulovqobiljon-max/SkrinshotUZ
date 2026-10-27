namespace SkrinshotUZ.Core.Storage;

/// <summary>Dastur papkalari. Yo'llar faqat hisoblanadi — papka yaratilmaydi (test qilish oson bo'lsin).</summary>
public static class AppPaths
{
    /// <summary>Papka nomlarida ishlatiladigan dastur nomi.</summary>
    public const string AppFolderName = "Skrinshot UZ";

    /// <summary>Loglar papkasi: <c>%LOCALAPPDATA%\Skrinshot UZ\logs</c>.</summary>
    /// <param name="localAppData">LocalApplicationData papkasi.</param>
    /// <returns>To'liq yo'l.</returns>
    public static string LogsFolder(string localAppData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppData);
        return Path.Combine(localAppData, AppFolderName, "logs");
    }

    /// <summary>Sozlamalar fayli: <c>%APPDATA%\Skrinshot UZ\settings.json</c>.</summary>
    /// <param name="appData">ApplicationData (Roaming) papkasi.</param>
    /// <returns>To'liq yo'l.</returns>
    public static string SettingsFile(string appData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appData);
        return Path.Combine(appData, AppFolderName, "settings.json");
    }

    /// <summary>Tarix fayli: <c>%APPDATA%\Skrinshot UZ\history.json</c>.</summary>
    /// <param name="appData">ApplicationData (Roaming) papkasi.</param>
    /// <returns>To'liq yo'l.</returns>
    public static string HistoryFile(string appData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appData);
        return Path.Combine(appData, AppFolderName, "history.json");
    }

    /// <summary>Skrinshotlar uchun standart papka: <c>Rasmlar\Skrinshot UZ</c>.</summary>
    /// <param name="myPictures">Foydalanuvchining "Rasmlar" (My Pictures) papkasi.</param>
    /// <returns>To'liq yo'l.</returns>
    public static string DefaultScreenshotsFolder(string myPictures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(myPictures);
        return Path.Combine(myPictures, AppFolderName);
    }

    /// <summary>Xatolik log faylining nomi: <c>xato_2026-10-27_20-38-15.log</c>.</summary>
    /// <param name="timestamp">Vaqt.</param>
    /// <returns>Fayl nomi.</returns>
    public static string CrashLogFileName(DateTime timestamp) =>
        "xato_" + timestamp.ToString(ScreenshotFileNamer.TimestampFormat, System.Globalization.CultureInfo.InvariantCulture) + ".log";
}
