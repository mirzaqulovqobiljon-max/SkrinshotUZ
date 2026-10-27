using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using SkrinshotUZ.Capture;
using SkrinshotUZ.Core.Capture;
using SkrinshotUZ.Core.Hotkeys;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Permissions;
using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Settings;
using SkrinshotUZ.Core.Startup;
using SkrinshotUZ.Core.Storage;
using SkrinshotUZ.Native;
using SkrinshotUZ.UI;

namespace SkrinshotUZ;

/// <summary>
/// Dasturning asosiy holati: oyna yo'q, faqat tray (soat yonidagi) ikonka.
/// Ishga tushish tartibi: ikonka → (birinchi marta) ruxsat oynasi → tugmalarni ro'yxatdan o'tkazish.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly StartupOptions _options;
    private readonly EventWaitHandle? _activateSignal;
    private readonly WindowsUserPrompts _prompts = new();

    private readonly Icon _icon;
    private readonly Font _menuFont;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _captureItem;
    private readonly ToolStripMenuItem _printScreenItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly ToolStripMenuItem _snippingItem;
    private readonly ToolStripMenuItem _statusItem;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _startupTimer;

    private WindowsHotkeyService? _hotkeyService;
    private LowLevelKeyboardHook? _keyboardHook;
    private HotkeyManager? _hotkeys;
    private PermissionFlow? _permissions;
    private RegisteredWaitHandle? _activateWait;
    private SynchronizationContext? _ui;
    private bool _dialogOpen;
    private bool _capturing;
    private string? _smokeCaptureResult;

    /// <summary>Tray ikonkasini yaratadi; qolgan ishlar xabar sikli boshlangach bajariladi.</summary>
    /// <param name="options">Ishga tushirish parametrlari.</param>
    /// <param name="activateSignal">Ikkinchi nusxa ochilganda signal beradigan hodisa.</param>
    public TrayApplicationContext(StartupOptions options, EventWaitHandle? activateSignal)
    {
        _options = options;
        _activateSignal = activateSignal;
        _icon = AppIcon.LoadSmall();

        // O'qituvchilar uchun menyu shrifti odatdagidan biroz kattaroq.
        _menuFont = new Font("Segoe UI", 10.5f);
        _menu = new ContextMenuStrip { Font = _menuFont, ShowImageMargin = false, ShowCheckMargin = true };

        _captureItem = new ToolStripMenuItem(Strings.Menu_Capture, null, (_, _) => OnAction(HotkeyAction.CaptureRegion, Strings.Source_TrayIcon));
        _captureItem.Font = new Font(_menuFont, FontStyle.Bold);
        _printScreenItem = new ToolStripMenuItem(Strings.Menu_PrintScreen, null, (_, _) => RunDialog(TogglePrintScreen));
        _autostartItem = new ToolStripMenuItem(Strings.Menu_Autostart, null, (_, _) => RunDialog(ToggleAutostart));
        _snippingItem = new ToolStripMenuItem(Strings.Menu_SnippingTool, null, (_, _) => RunDialog(FixSnippingTool));
        _statusItem = new ToolStripMenuItem(Strings.Menu_HotkeyStatus, null, (_, _) => RunDialog(ShowHotkeyStatus));

        _menu.Items.Add(_captureItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_printScreenItem);
        _menu.Items.Add(_autostartItem);
        _menu.Items.Add(_snippingItem);
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Strings.Menu_Exit, null, (_, _) => ExitThread());
        _menu.Opening += (_, _) => RefreshMenu();

        _trayIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = Strings.Tray_Tooltip,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OnAction(HotkeyAction.CaptureRegion, Strings.Source_TrayIcon);
            }
        };

        // Ruxsat oynasi va tugmalar — xabar sikli ishga tushgandan keyin (Application.Run ichida).
        _startupTimer = new System.Windows.Forms.Timer { Interval = 1 };
        _startupTimer.Tick += OnStartup;
        _startupTimer.Start();
    }

    /// <summary>Tutun testi hisobotiga qo'shiladigan diagnostika qatorlari.</summary>
    public IEnumerable<string> DiagnosticLines
    {
        get
        {
            var lines = _hotkeys?.Status.Select(s => string.Create(CultureInfo.InvariantCulture, $"hotkey.{s.Hotkey}={s.Mode}")).ToList() ?? new List<string>();
            lines.Add("capture=" + (_smokeCaptureResult ?? "not-run"));
            return lines;
        }
    }

    protected override void ExitThreadCore()
    {
        // Tugmalar darhol bo'shatilsin, ikonka darhol yo'qolsin.
        _hotkeys?.Dispose();
        _trayIcon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _startupTimer.Dispose();
            _activateWait?.Unregister(null);
            _hotkeys?.Dispose();
            _keyboardHook?.Dispose();
            _hotkeyService?.Dispose();
            _trayIcon.Dispose();
            _captureItem.Font.Dispose();
            _menu.Dispose();
            _menuFont.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnStartup(object? sender, EventArgs e)
    {
        _startupTimer.Stop();
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _hotkeyService = new WindowsHotkeyService();
        _keyboardHook = new LowLevelKeyboardHook();
        _hotkeys = new HotkeyManager(_hotkeyService, _keyboardHook);
        _hotkeys.ActionRequested += (_, a) => OnAction(a.Action, a.Hotkey.ToString());

        if (_options.SmokeTest)
        {
            // CI: hech qanday oyna yo'q, foydalanuvchi sozlamalari va reyestr o'zgarmaydi —
            // faqat tugmalarni ro'yxatdan o'tkazish mexanizmi haqiqiy Windows'da tekshiriladi.
            _hotkeys.Apply(printScreenAllowed: true);

            // To'liq yo'l: ekranni olish → overlay oyna → soha → buferga nusxa → buferdan qayta o'qish.
            _ui.Post(_ => _smokeCaptureResult = RunSmokeCapture(), null);
            MemoryTrimmer.Trim();
            return;
        }

        var registry = new WindowsRegistryStore();
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _permissions = new PermissionFlow(
            new JsonSettingsStore(AppPaths.SettingsFile(appData)),
            _prompts,
            new SnippingToolSetting(registry, Environment.OSVersion.Version.Build),
            new AutostartService(registry, Environment.ProcessPath ?? Application.ExecutablePath));

        RunDialog(_permissions.RunAtStartup);
        ApplyHotkeys();
        ListenForSecondInstance();

        if (!_options.Autostart)
        {
            ShowBalloon(Strings.Tray_Started, ToolTipIcon.Info);
        }

        // Ishga tushish tugadi — endi dastur tray'da jim kutadi.
        MemoryTrimmer.Trim();
    }

    private void ApplyHotkeys()
    {
        if (_hotkeys is null)
        {
            return;
        }

        var status = _hotkeys.Apply(_permissions?.PrintScreenAllowed ?? false);
        string? warning = UiText.FailedHotkeysWarning(status);
        if (warning is not null)
        {
            ShowBalloon(warning, ToolTipIcon.Warning);
        }
    }

    private void OnAction(HotkeyAction action, string source)
    {
        switch (action)
        {
            case HotkeyAction.CaptureRegion:
                StartCapture(presetScreenRect: null);
                break;
            case HotkeyAction.CaptureActiveWindow:
                // Faol oyna hozir, overlay ochilishidan OLDIN aniqlanadi.
                StartCapture(WindowsScreenInfo.GetForegroundWindowBounds());
                break;
            default:
                // Shift+PrintScreen (darhol saqlash) — 5-bosqichda.
                ShowBalloon(
                    string.Format(CultureInfo.CurrentCulture, Strings.Placeholder_ActionTriggered, UiText.ActionName(action), source),
                    ToolTipIcon.Info);
                break;
        }
    }

    /// <summary>
    /// Ekranni oladi va soha tanlash oynasini ochadi. 3-bosqich: tanlangan soha buferga nusxalanadi
    /// (4-bosqichda bu yerda tahrirlash asboblari paydo bo'ladi).
    /// </summary>
    private void StartCapture(Rectangle? presetScreenRect)
    {
        if (_capturing)
        {
            return; // overlay allaqachon ochiq
        }

        _capturing = true;
        try
        {
            ScreenLayout layout;
            Bitmap screen;
            try
            {
                layout = WindowsScreenInfo.GetLayout();
                screen = ScreenCapture.CaptureVirtualScreen(layout);
            }
            catch (Exception ex) when (ex is Win32Exception or ExternalException or ArgumentException or OutOfMemoryException)
            {
                ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Capture_Failed, ex.Message), ToolTipIcon.Warning);
                return;
            }

            using (screen)
            using (var overlay = new CaptureOverlayForm(layout, screen, presetScreenRect))
            {
                if (overlay.ShowDialog() == DialogResult.OK && overlay.Result is Rectangle selection)
                {
                    using var cropped = ScreenCapture.Crop(screen, selection);
                    if (CopyToClipboard(cropped))
                    {
                        ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Capture_Copied, OverlayLayout.SizeText(cropped.Size)), ToolTipIcon.Info);
                    }
                    else
                    {
                        ShowBalloon(Strings.Capture_ClipboardBusy, ToolTipIcon.Warning);
                    }
                }
            }
        }
        finally
        {
            _capturing = false;
            MemoryTrimmer.Trim(); // katta ekran rasmlari endi kerak emas
        }
    }

    /// <summary>Buferga nusxa (bufer boshqa dastur tomonidan band bo'lsa — bir necha marta urinadi).</summary>
    private static bool CopyToClipboard(Bitmap image)
    {
        try
        {
            var data = new DataObject();
            data.SetImage(image);
            Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 100);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    /// <summary>CI tutun testi: overlay'ni haqiqiy Windows'da ochib, soha tanlab, buferdan natijani tekshiradi.</summary>
    private string RunSmokeCapture()
    {
        try
        {
            var layout = WindowsScreenInfo.GetLayout();
            using var screen = ScreenCapture.CaptureVirtualScreen(layout);
            var expected = new Rectangle(
                Math.Max(0, (layout.CanvasSize.Width / 2) - 100),
                Math.Max(0, (layout.CanvasSize.Height / 2) - 50),
                Math.Min(200, layout.CanvasSize.Width),
                Math.Min(100, layout.CanvasSize.Height));

            using var overlay = new CaptureOverlayForm(layout, screen, null);
            overlay.Shown += (_, _) => overlay.AcceptForTest(expected);
            if (overlay.ShowDialog() != DialogResult.OK || overlay.Result is not Rectangle selection || selection != expected)
            {
                return "error: overlay natijasi kutilganidek emas";
            }

            using var cropped = ScreenCapture.Crop(screen, selection);
            if (!CopyToClipboard(cropped))
            {
                return "error: bufer band";
            }

            using var fromClipboard = Clipboard.GetImage();
            string monitors = string.Join(";", layout.Monitors.Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Bounds.Width}x{m.Bounds.Height}@{m.Dpi}")));
            return fromClipboard is not null && fromClipboard.Size == cropped.Size
                ? string.Create(CultureInfo.InvariantCulture, $"ok {cropped.Width}x{cropped.Height} canvas={layout.CanvasSize.Width}x{layout.CanvasSize.Height} monitors={monitors}")
                : "error: buferdagi rasm o'lchami mos emas";
        }
        catch (Exception ex) when (ex is Win32Exception or ExternalException or ArgumentException or InvalidOperationException)
        {
            return "error: " + ex.GetType().Name + ": " + ex.Message.Replace('\r', ' ').Replace('\n', ' ');
        }
        finally
        {
            MemoryTrimmer.Trim();
        }
    }

    private void RefreshMenu()
    {
        bool ready = _permissions is not null;
        _printScreenItem.Enabled = ready;
        _autostartItem.Enabled = ready;
        _statusItem.Enabled = _hotkeys is not null;

        _printScreenItem.Checked = _permissions?.PrintScreenAllowed ?? false;
        _autostartItem.Checked = _permissions?.IsAutostartEnabled() ?? false;
        _snippingItem.Visible = _permissions is not null && _permissions.PrintScreenAllowed && _permissions.IsSnippingToolEnabled();
    }

    private void TogglePrintScreen()
    {
        if (_permissions is null)
        {
            return;
        }

        if (_permissions.PrintScreenAllowed)
        {
            _permissions.RevokePrintScreen();
        }
        else
        {
            _permissions.RequestPrintScreen();
        }

        ApplyHotkeys();
    }

    private void ToggleAutostart() => _permissions?.SetAutostart(!_permissions.IsAutostartEnabled());

    private void FixSnippingTool()
    {
        _permissions?.OfferSnippingToolFix();
        ApplyHotkeys();
    }

    private void ShowHotkeyStatus()
    {
        if (_hotkeys is not null)
        {
            _prompts.ShowInfo(Strings.HotkeyStatus_Title, UiText.HotkeyStatusReport(_hotkeys.Status, _permissions?.PrintScreenAllowed ?? false));
        }
    }

    /// <summary>Bir vaqtda faqat bitta oyna (menyu bir necha marta bosilsa ham).</summary>
    private void RunDialog(Action action)
    {
        if (_dialogOpen)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            action();
        }
        finally
        {
            _dialogOpen = false;
            MemoryTrimmer.Trim(); // oyna resurslari endi kerak emas
        }
    }

    private void ListenForSecondInstance()
    {
        if (_activateSignal is null)
        {
            return;
        }

        _activateWait = ThreadPool.RegisterWaitForSingleObject(
            _activateSignal,
            (_, _) => _ui?.Post(_ => ShowBalloon(Strings.Tray_AlreadyRunning, ToolTipIcon.Info), null),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private void ShowBalloon(string text, ToolTipIcon icon)
    {
        if (!_options.SmokeTest)
        {
            _trayIcon.ShowBalloonTip(5000, Strings.AppName, text, icon);
        }
    }
}
