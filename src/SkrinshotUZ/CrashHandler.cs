using System.Globalization;
using System.Text;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Startup;
using SkrinshotUZ.Core.Storage;

namespace SkrinshotUZ;

/// <summary>Kutilmagan xatoliklarni logga yozadi va foydalanuvchiga o'zbekcha xabar beradi.</summary>
internal static class CrashHandler
{
    private static int _handling;

    /// <summary>Xatolikni qayta ishlaydi va dasturni yopadi.</summary>
    /// <param name="exception">Xatolik.</param>
    /// <param name="options">Ishga tushirish parametrlari.</param>
    public static void Handle(Exception? exception, StartupOptions options)
    {
        // Xatolik ichida yana xatolik bo'lsa — cheksiz takrorlanmasin.
        if (Interlocked.Exchange(ref _handling, 1) == 1)
        {
            return;
        }

        string details = exception?.ToString() ?? "Noma'lum xatolik";
        string? logPath = TryWriteLog(details);

        if (options.SmokeTest)
        {
            // CI rejimida oyna chiqarilmaydi (aks holda build osilib qoladi).
            if (options.SmokeReportPath is not null)
            {
                TryWrite(options.SmokeReportPath, "status=error" + Environment.NewLine + details);
            }

            Environment.Exit(1);
        }

        MessageBox.Show(
            string.Format(CultureInfo.CurrentCulture, Strings.Crash_Message, logPath ?? "—"),
            Strings.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        Environment.Exit(1);
    }

    private static string? TryWriteLog(string details)
    {
        try
        {
            string folder = AppPaths.LogsFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, AppPaths.CrashLogFileName(DateTime.Now));
            return TryWrite(path, details) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryWrite(string path, string text)
    {
        try
        {
            File.WriteAllText(path, text, Encoding.UTF8);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
