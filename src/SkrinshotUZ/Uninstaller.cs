using SkrinshotUZ.Core.Permissions;
using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Settings;
using SkrinshotUZ.Core.Storage;
using SkrinshotUZ.Native;
using SkrinshotUZ.UI;

namespace SkrinshotUZ;

/// <summary>
/// <c>SkrinshotUZ.exe --uninstall [--quiet]</c> — o'rnatuvchi dasturni o'chirishdan oldin chaqiradi.
/// Mantiq <see cref="PermissionFlow.CleanUpForUninstall"/>'da (unit-testlangan); bu yerda faqat Windows qismlari ulanadi.
/// Skrinshotlar (Rasmlar\Skrinshot UZ) hech qachon o'chirilmaydi.
/// </summary>
internal static class Uninstaller
{
    /// <summary>Tozalaydi. Xato bo'lsa ham o'chirish to'xtamaydi.</summary>
    /// <param name="quiet">Jim rejim — hech qanday oyna chiqmaydi.</param>
    public static void CleanUp(bool quiet)
    {
        try
        {
            var registry = new WindowsRegistryStore();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var flow = new PermissionFlow(
                new JsonSettingsStore(AppPaths.SettingsFile(appData)),
                new WindowsUserPrompts(),
                new SnippingToolSetting(registry, Environment.OSVersion.Version.Build),
                new AutostartService(registry, Environment.ProcessPath ?? Application.ExecutablePath));
            flow.CleanUpForUninstall(quiet);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            // O'chirish baribir davom etadi: bu yerdagi xato foydalanuvchini to'xtatmasligi kerak.
        }
    }
}
