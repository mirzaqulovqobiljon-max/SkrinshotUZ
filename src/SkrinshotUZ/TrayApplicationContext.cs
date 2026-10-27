using System.Globalization;
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
    public IEnumerable<string> DiagnosticLines =>
        _hotkeys?.Status.Select(s => string.Create(CultureInfo.InvariantCulture, $"hotkey.{s.Hotkey}={s.Mode}")) ?? Enumerable.Empty<string>();

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

    /// <summary>
    /// 2-bosqich: amal hali yo'q — qaysi tugma qabul qilinganini ko'rsatamiz (qo'lda tekshirish uchun).
    /// 3-bosqichda bu yerda soha tanlash oynasi ochiladi.
    /// </summary>
    private void OnAction(HotkeyAction action, string source)
    {
        ShowBalloon(
            string.Format(CultureInfo.CurrentCulture, Strings.Placeholder_ActionTriggered, UiText.ActionName(action), source),
            ToolTipIcon.Info);
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
