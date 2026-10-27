using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ;

/// <summary>
/// Dasturning asosiy holati: oyna yo'q, faqat tray (soat yonidagi) ikonka.
/// 1-bosqich: ikonka va "Chiqish" menyusi. 2-bosqichda: ruxsat oynasi, PrintScreen, Ctrl+Shift+S.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _icon;

    /// <summary>Tray ikonkasini yaratadi.</summary>
    /// <param name="showWelcomeBalloon">Ishga tushganini bildiruvchi xabar ko'rsatilsinmi.</param>
    public TrayApplicationContext(bool showWelcomeBalloon)
    {
        _icon = AppIcon.LoadSmall();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(Strings.Menu_Exit, null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = Strings.Tray_Tooltip,
            ContextMenuStrip = _menu,
            Visible = true,
        };

        if (showWelcomeBalloon)
        {
            _trayIcon.ShowBalloonTip(4000, Strings.AppName, Strings.Tray_Started, ToolTipIcon.Info);
        }
    }

    /// <inheritdoc />
    protected override void ExitThreadCore()
    {
        // Ikonka darhol yo'qolsin (aks holda sichqoncha ustidan o'tguncha "arvoh" ikonka qoladi).
        _trayIcon.Visible = false;
        base.ExitThreadCore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _menu.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }
}
