using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using SkrinshotUZ.Ads;
using SkrinshotUZ.Capture;
using SkrinshotUZ.Core.Ads;
using SkrinshotUZ.Core.Capture;
using SkrinshotUZ.Core.Editing;
using SkrinshotUZ.Core.Hotkeys;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Permissions;
using SkrinshotUZ.Core.Printing;
using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Settings;
using SkrinshotUZ.Core.Startup;
using SkrinshotUZ.Core.Storage;
using SkrinshotUZ.Editing;
using SkrinshotUZ.Storage;
using SkrinshotUZ.Native;
using SkrinshotUZ.Printing;
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
    private readonly ToolStripMenuItem _historyItem;
    private readonly ToolStripMenuItem _folderItem;
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
    private RegisteredWaitHandle? _exitWait;
    private readonly EventWaitHandle? _exitSignal;
    private SynchronizationContext? _ui;
    private bool _dialogOpen;
    private bool _capturing;
    private string? _smokeCaptureResult;
    private string? _smokeSaveResult;
    private string? _smokeHistoryPreview;
    private string? _smokePrintResult;
    private string? _smokePrintPreview;
    private string? _smokeAdPreview;
    private readonly CancellationTokenSource _adsCts = new();
    private System.Windows.Forms.Timer? _adsTimer;
    private bool _adsRunning;
    private bool _disposed;
    private ScreenshotHistory _history = new();
    private JsonHistoryStore? _historyStore;
    private HistoryForm? _historyForm;
    private Action? _balloonClick;

    /// <summary>Tray ikonkasini yaratadi; qolgan ishlar xabar sikli boshlangach bajariladi.</summary>
    /// <param name="options">Ishga tushirish parametrlari.</param>
    /// <param name="activateSignal">Ikkinchi nusxa ochilganda signal beradigan hodisa.</param>
    /// <param name="exitSignal">O'rnatuvchi "yopil" deganda signal beradigan hodisa.</param>
    public TrayApplicationContext(StartupOptions options, EventWaitHandle? activateSignal, EventWaitHandle? exitSignal = null)
    {
        _options = options;
        _activateSignal = activateSignal;
        _exitSignal = exitSignal;
        _icon = AppIcon.LoadSmall();

        // O'qituvchilar uchun menyu shrifti odatdagidan biroz kattaroq.
        _menuFont = new Font("Segoe UI", 10.5f);
        _menu = new ContextMenuStrip { Font = _menuFont, ShowImageMargin = false, ShowCheckMargin = true };

        _captureItem = new ToolStripMenuItem(Strings.Menu_Capture, null, (_, _) => OnAction(HotkeyAction.CaptureRegion));
        _captureItem.Font = new Font(_menuFont, FontStyle.Bold);
        _printScreenItem = new ToolStripMenuItem(Strings.Menu_PrintScreen, null, (_, _) => RunDialog(TogglePrintScreen));
        _autostartItem = new ToolStripMenuItem(Strings.Menu_Autostart, null, (_, _) => RunDialog(ToggleAutostart));
        _snippingItem = new ToolStripMenuItem(Strings.Menu_SnippingTool, null, (_, _) => RunDialog(FixSnippingTool));
        _statusItem = new ToolStripMenuItem(Strings.Menu_HotkeyStatus, null, (_, _) => RunDialog(ShowHotkeyStatus));
        _historyItem = new ToolStripMenuItem(Strings.Menu_History, null, (_, _) => ShowHistory());
        _folderItem = new ToolStripMenuItem(Strings.Menu_OpenFolder, null, (_, _) => Shell.OpenFolder(CurrentSaveFolder()));

        _menu.Items.Add(_captureItem);
        _menu.Items.Add(_historyItem);
        _menu.Items.Add(_folderItem);
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
        _trayIcon.BalloonTipClicked += (_, _) =>
        {
            var action = _balloonClick;
            _balloonClick = null;
            action?.Invoke();
        };
        _trayIcon.BalloonTipClosed += (_, _) => _balloonClick = null;
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OnAction(HotkeyAction.CaptureRegion);
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
            lines.Add("save=" + (_smokeSaveResult ?? "not-run"));
            if (_smokeHistoryPreview is not null)
            {
                lines.Add("history=" + _smokeHistoryPreview);
            }

            lines.Add("print=" + (_smokePrintResult ?? "not-run"));
            lines.Add("ads=" + (AdsConfig.Endpoint is null ? "off" : "configured " + AdsConfig.Endpoint.BaseUrl.Host));
            if (_smokeAdPreview is not null)
            {
                lines.Add("adview=" + _smokeAdPreview);
            }

            if (_smokePrintPreview is not null)
            {
                lines.Add("printview=" + _smokePrintPreview);
            }

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
        // Windows Forms kontekstni yopilishda ikki marta tozalaydi (Application.Run va using) — ikkinchisi jim o'tadi.
        if (disposing && !_disposed)
        {
            _disposed = true;
            _startupTimer.Dispose();
            _adsCts.Cancel();
            _adsTimer?.Dispose();
            _adsCts.Dispose();
            _activateWait?.Unregister(null);
            _exitWait?.Unregister(null);
            _hotkeys?.Dispose();
            _keyboardHook?.Dispose();
            _hotkeyService?.Dispose();
            _historyForm?.Dispose();
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
        _hotkeys.ActionRequested += (_, a) => OnAction(a.Action);

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

        _historyStore = new JsonHistoryStore(AppPaths.HistoryFile(appData));
        _history = _historyStore.Load();

        // Signallar ruxsat oynalaridan OLDIN tinglanadi: o'rnatuvchi (yangilash/o'chirish) dastur savol oynasida
        // turgan paytda ham uni yopa olsin.
        ListenForSecondInstance();
        RunDialog(_permissions.RunAtStartup);
        ApplyHotkeys();

        if (!_options.Autostart)
        {
            ShowBalloon(Strings.Tray_Started, ToolTipIcon.Info);
        }

        StartAds();

        // Ishga tushish tugadi — endi dastur tray'da jim kutadi.
        MemoryTrimmer.Trim();
    }

    // ---------- Reklama ----------

    /// <summary>
    /// Ishga tushgandan 20 soniya keyin (ruxsat oynalari va birinchi xabardan so'ng), keyin har soatda tekshiriladi —
    /// reklama kuniga ko'pi bilan bir marta ko'rsatiladi. Sozlanmagan bo'lsa dastur internetga umuman chiqmaydi.
    /// </summary>
    private void StartAds()
    {
        if (AdsConfig.Endpoint is null)
        {
            return;
        }

        _adsTimer = new System.Windows.Forms.Timer { Interval = 20_000 };
        _adsTimer.Tick += async (_, _) => await RunAdsAsync();
        _adsTimer.Start();
    }

    private async Task RunAdsAsync()
    {
        if (_adsRunning || _permissions is null || _adsTimer is null || AdsConfig.Endpoint is not AdsEndpoint endpoint)
        {
            return;
        }

        if (IsBusyForAds())
        {
            _adsTimer.Interval = 60_000; // skrinshot yoki chop etish oynasi ochiq — bir daqiqadan keyin
            return;
        }

        _adsRunning = true;
        _adsTimer.Interval = 60 * 60 * 1000;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var coordinator = new AdCoordinator(
                new SupabaseAdClient(http, endpoint),
                new WindowsAdPresenter(),
                new SettingsAdState(_permissions.Settings, _permissions.SaveSettings),
                () => DateOnly.FromDateTime(DateTime.Now));
            await coordinator.RunOnceAsync(_adsCts.Token);
        }
        catch (OperationCanceledException)
        {
            // dastur yopilmoqda
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or ObjectDisposedException
                                       or Win32Exception or ExternalException or ArgumentException)
        {
            // Reklama hech qachon dastur ishiga xalaqit bermaydi.
        }
        finally
        {
            _adsRunning = false;
            MemoryTrimmer.Trim();
        }
    }

    private bool IsBusyForAds() =>
        _capturing || Application.OpenForms.Cast<Form>().Any(f => f is CaptureOverlayForm or PrintForm);

    /// <summary>Skrinshot olishdan oldin: ochiq reklama oynasi rasmga tushmasin.</summary>
    private static void HideAdWindows()
    {
        var open = Application.OpenForms.OfType<AdWindow>().ToList();
        if (open.Count == 0)
        {
            return;
        }

        foreach (var w in open)
        {
            w.CloseSilently();
        }

        Application.DoEvents();
        Thread.Sleep(80); // ekran (DWM) yangilanishiga ulgursin
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

    private void OnAction(HotkeyAction action)
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
            case HotkeyAction.CaptureFullScreenSave:
                SaveFullScreen();
                break;
        }
    }

    // ---------- Saqlash ----------

    /// <summary>Saqlash papkasi: oxirgi tanlangani (mavjud bo'lsa) yoki "Rasmlar\Skrinshot UZ".</summary>
    private string CurrentSaveFolder() => SaveOptions.ResolveFolder(
        _permissions?.Settings.LastSaveFolder,
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Directory.Exists);

    private ImageFileFormat CurrentFormat => _permissions?.Settings.SaveFormat ?? ImageFileFormat.Png;

    /// <summary>
    /// Saqlash oynasi: papka va nom (skrinshot_2026-10-27_20-38-15.png) oldindan tayyor — odatda faqat "Saqlash"ni bosish kifoya.
    /// Tanlangan papka va format eslab qolinadi.
    /// </summary>
    private string? AskSavePath(IWin32Window owner)
    {
        string folder = CurrentSaveFolder();
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Papkani yaratib bo'lmasa — oyna baribir ochiladi, foydalanuvchi boshqa joy tanlaydi.
        }

        var format = CurrentFormat;
        using var dialog = new SaveFileDialog
        {
            Title = Strings.Save_DialogTitle,
            Filter = SaveOptions.DialogFilter,
            FilterIndex = SaveOptions.FilterIndexFor(format),
            InitialDirectory = folder,
            FileName = Path.GetFileName(ScreenshotFileNamer.CreateUniquePath(folder, DateTime.Now, format, File.Exists)),
            AddExtension = true,
            DefaultExt = ScreenshotFileNamer.GetExtension(format).TrimStart('.'),
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        var chosen = SaveOptions.FormatFor(dialog.FileName, dialog.FilterIndex);
        string path = SaveOptions.EnsureExtension(dialog.FileName, chosen);
        if (_permissions is not null)
        {
            _permissions.Settings.LastSaveFolder = Path.GetDirectoryName(path);
            _permissions.Settings.SaveFormat = chosen;
            _permissions.SaveSettings();
        }

        return path;
    }

    /// <summary>Rasmni saqlaydi, tarixga yozadi va xabar beradi (xabarni bosish — papkada ko'rsatish).</summary>
    private bool SaveImage(Bitmap image, string path)
    {
        try
        {
            ImageSaver.Save(image, path, SaveOptions.FormatFor(path, SaveOptions.FilterIndexFor(CurrentFormat)));
        }
        catch (Exception ex) when (ImageSaver.IsSaveError(ex))
        {
            ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Save_Failed, ex.Message), ToolTipIcon.Warning);
            return false;
        }

        _history.Add(new HistoryEntry(path, DateTime.Now, image.Width, image.Height));
        SaveHistory();
        _historyForm?.Reload();
        ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Save_Done, Path.GetFileName(path)), ToolTipIcon.Info, () => Shell.ShowInFolder(path));
        return true;
    }

    /// <summary>Shift+PrintScreen: barcha monitorlar hech qanday oynasiz darhol saqlanadi.</summary>
    private void SaveFullScreen()
    {
        if (_capturing)
        {
            return;
        }

        _capturing = true;
        HideAdWindows();
        try
        {
            var layout = WindowsScreenInfo.GetLayout();
            using var screen = ScreenCapture.CaptureVirtualScreen(layout);
            using var image = ScreenCapture.Crop(screen, new Rectangle(Point.Empty, screen.Size));
            ImageDpi.Set(image, layout.Monitors.FirstOrDefault(m => m.IsPrimary)?.Dpi ?? Units.BaseDpi);
            string folder = CurrentSaveFolder();
            Directory.CreateDirectory(folder);
            SaveImage(image, ScreenshotFileNamer.CreateUniquePath(folder, DateTime.Now, CurrentFormat, File.Exists));
        }
        catch (Exception ex) when (ex is Win32Exception or OutOfMemoryException || ImageSaver.IsSaveError(ex))
        {
            ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Save_Failed, ex.Message), ToolTipIcon.Warning);
        }
        finally
        {
            _capturing = false;
            MemoryTrimmer.Trim();
        }
    }

    private void SaveHistory()
    {
        try
        {
            _historyStore?.Save(_history);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tarixni yozib bo'lmasa — rasm baribir saqlangan; keyingi safar qayta urinamiz.
        }
    }

    private void ShowHistory()
    {
        if (_historyForm is { IsDisposed: false })
        {
            _historyForm.Activate();
            return;
        }

        _historyForm = new HistoryForm(_history, CurrentSaveFolder(), SaveHistory, PrintFile);
        _historyForm.FormClosed += (_, _) =>
        {
            _historyForm?.Dispose();
            _historyForm = null;
            MemoryTrimmer.Trim();
        };
        _historyForm.Show();
        WindowsScreenInfo.ForceForeground(_historyForm.Handle);
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
        HideAdWindows();
        Bitmap? toPrint = null;
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
            using (var overlay = new CaptureOverlayForm(layout, screen, presetScreenRect) { SavePathProvider = AskSavePath })
            {
                if (overlay.ShowDialog() == DialogResult.OK && overlay.Result is Rectangle selection)
                {
                    var cropped = ImageComposer.Compose(screen, selection, overlay.Annotations);

                    // Rasm qaysi monitorda olingan — o'sha masshtab "Asl o'lchamda" chop etish uchun rasmga yoziladi
                    // (PNG/JPG faylda ham saqlanadi).
                    var center = new Point(selection.X + (selection.Width / 2), selection.Y + (selection.Height / 2));
                    ImageDpi.Set(cropped, layout.MonitorAtCanvas(center).Dpi);

                    if (overlay.Action == OverlayAction.Print)
                    {
                        toPrint = cropped; // chop etish oynasi overlay yopilgandan keyin ochiladi
                    }
                    else
                    {
                        using (cropped)
                        {
                            FinishCapture(overlay, cropped);
                        }
                    }
                }
            }
        }
        finally
        {
            _capturing = false;
            MemoryTrimmer.Trim(); // katta ekran rasmlari endi kerak emas
        }

        if (toPrint is not null)
        {
            using (toPrint)
            {
                ShowPrint(toPrint);
            }
        }
    }

    /// <summary>Overlay natijasi: saqlash yoki buferga nusxa.</summary>
    private void FinishCapture(CaptureOverlayForm overlay, Bitmap cropped)
    {
        if (overlay.Action == OverlayAction.Save && overlay.SavePath is string savePath)
        {
            SaveImage(cropped, savePath);
        }
        else if (CopyToClipboard(cropped))
        {
            ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Capture_Copied, OverlayLayout.SizeText(cropped.Size)), ToolTipIcon.Info);
        }
        else
        {
            ShowBalloon(Strings.Capture_ClipboardBusy, ToolTipIcon.Warning);
        }
    }

    // ---------- Chop etish ----------

    /// <summary>Chop etish oynasi (modal). Sozlamalar eslab qolinadi; PDF saqlansa — xabarni bosib papkada ko'rish mumkin.</summary>
    private void ShowPrint(Bitmap image)
    {
        var prefs = _permissions?.Settings.Print ?? new PrintPreferences();
        using var form = new PrintForm(image, prefs, _history.Entries.ToList(), CurrentSaveFolder());
        bool done = form.ShowDialog() == DialogResult.OK;
        _permissions?.SaveSettings(); // oxirgi sozlamalar (printer, rejim…) keyingi safar ham shunday bo'lsin
        if (!done || form.Outcome is not PrintOutcome outcome)
        {
            return;
        }

        if (outcome.PdfPath is string pdf)
        {
            ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Print_PdfSaved, Path.GetFileName(pdf)), ToolTipIcon.Info, () => Shell.ShowInFolder(pdf));
        }
        else
        {
            string what = string.Format(CultureInfo.InvariantCulture, Strings.Print_PagesCount, outcome.Pages) +
                (outcome.Copies > 1 ? string.Format(CultureInfo.InvariantCulture, Strings.Print_CopiesSuffix, outcome.Copies) : string.Empty);
            ShowBalloon(string.Format(CultureInfo.CurrentCulture, Strings.Print_Sent, what), ToolTipIcon.Info);
        }
    }

    /// <summary>Tarix oynasidan: saqlangan faylni chop etish.</summary>
    private void PrintFile(string path)
    {
        using var image = Thumbnails.Load(path);
        if (image is null)
        {
            MessageBox.Show(Strings.Print_LoadFailed, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ShowPrint(image);
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

            // Soha ustiga qizil to'rtburchak chizamiz va u natija rasmida ham borligini tekshiramiz.
            var mark = new RectangleAnnotation(Rectangle.Inflate(expected, -10, -10), Color.FromArgb(255, 0, 0), 4);
            using var overlay = new CaptureOverlayForm(layout, screen, null);
            overlay.Shown += (_, _) => overlay.AcceptForTest(expected, new Annotation[] { mark });
            if (overlay.ShowDialog() != DialogResult.OK || overlay.Result is not Rectangle selection || selection != expected
                || overlay.Annotations.Count != 1)
            {
                return "error: overlay natijasi kutilganidek emas";
            }

            using var cropped = ImageComposer.Compose(screen, selection, overlay.Annotations);
            var edge = cropped.GetPixel(10, cropped.Height / 2); // to'rtburchakning chap cheti
            if (edge.R < 200 || edge.G > 60 || edge.B > 60)
            {
                return "error: chizma natija rasmida yo'q (" + edge + ")";
            }
            if (!CopyToClipboard(cropped))
            {
                return "error: bufer band";
            }

            using var fromClipboard = Clipboard.GetImage();
            (_smokeSaveResult, _smokeHistoryPreview) = RunSmokeSave(cropped);
            _smokeAdPreview = RunSmokeAdPreview();
            (_smokePrintResult, _smokePrintPreview) = RunSmokePrint(cropped, layout.MonitorAtCanvas(selection.Location).Dpi);
            string monitors = string.Join(";", layout.Monitors.Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Bounds.Width}x{m.Bounds.Height}@{m.Dpi}")));
            return fromClipboard is not null && fromClipboard.Size == cropped.Size
                ? string.Create(CultureInfo.InvariantCulture, $"ok {cropped.Width}x{cropped.Height} annotated canvas={layout.CanvasSize.Width}x{layout.CanvasSize.Height} monitors={monitors}")
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

    /// <summary>CI tutun testi: reklama oynasini (internetsiz, namunaviy reklama bilan) ochib rasmga oladi.</summary>
    private static string RunSmokeAdPreview()
    {
        try
        {
            byte[] png;
            using (var banner = new Bitmap(640, 280))
            {
                using (var g = Graphics.FromImage(banner))
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 640, 280), Color.FromArgb(0, 103, 192), Color.FromArgb(0, 168, 140), 20f))
                using (var font = new Font("Segoe UI", 34f, FontStyle.Bold))
                {
                    g.FillRectangle(brush, 0, 0, 640, 280);
                    g.DrawString("Namuna banner", font, Brushes.White, 40, 100);
                }

                using var ms = new MemoryStream();
                banner.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                png = ms.ToArray();
            }

            var ad = new AdItem(Guid.NewGuid(), "Ingliz tili kursi — maktab o'quvchilari uchun 20% chegirma", null, "https://example.com", 0);
            using var window = new AdWindow(ad, png);
            window.PlaceInCorner(Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea);
            window.Show();
            window.Refresh();
            using var shot = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(shot, new Rectangle(Point.Empty, window.Size));
            bool inCorner = window.Right <= SystemInformation.WorkingArea.Right && window.Bottom <= SystemInformation.WorkingArea.Bottom && window.Left > SystemInformation.WorkingArea.Width / 2;
            string info = string.Create(CultureInfo.InvariantCulture, $"ok image={window.HasImage} size={window.Width}x{window.Height} corner={inCorner}");
            window.CloseSilently();
            return info + " png=" + SmokeTest.ToJpegBase64(shot, 1.0, 70);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or InvalidOperationException)
        {
            return "error: " + ex.GetType().Name + ": " + ex.Message.Replace('\r', ' ').Replace('\n', ' ');
        }
    }

    /// <summary>
    /// CI tutun testi: chop etish oynasini ochadi ("Bir varaqqa 2 ta", sarlavha + sana), rasmini oladi va
    /// "Microsoft Print to PDF" orqali haqiqiy PDF chiqaradi (o'rnatilgan bo'lsa). Hech qanday oyna/xabar chiqmaydi.
    /// </summary>
    private static (string Result, string? Preview) RunSmokePrint(Bitmap source, double dpi)
    {
        string dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.print." + Guid.NewGuid().ToString("N"));
        try
        {
            using var image = new Bitmap(source);
            ImageDpi.Set(image, dpi);
            var prefs = new PrintPreferences();
            string? preview;
            string pdfResult;
            using (var form = new PrintForm(image, prefs, Array.Empty<HistoryEntry>(), dir) { StartPosition = FormStartPosition.Manual, Location = Point.Empty })
            {
                form.Show();
                form.SetModeForTest(PrintMode.TwoPerPage, "7-B sinf", showDate: true);
                form.Refresh();
                form.RenderPreviewNow();
                form.Refresh();
                using (var shot = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(shot, new Rectangle(Point.Empty, form.Size));
                    preview = "ok png=" + SmokeTest.ToJpegBase64(shot, 0.5, 45);
                }

                var plan = form.Plan;
                if (plan is null || plan.Pages.Count != 1 || plan.Pages[0].Images.Count != 2 || plan.Pages[0].Captions.Count != 2)
                {
                    return ("error: 'Bir varaqqa 2 ta' rejasi noto'g'ri", preview);
                }

                if (!Printers.HasPdfPrinter())
                {
                    pdfResult = "pdf=skip (Microsoft Print to PDF yo'q; printerlar: " + Printers.Installed().Count + ")";
                }
                else
                {
                    Directory.CreateDirectory(dir);
                    string pdf = Path.Combine(dir, "smoke.pdf");
                    string? error = form.PrintToPdfForTest(pdf);
                    if (error is not null)
                    {
                        return ("error: PDF: " + error.Replace('\r', ' ').Replace('\n', ' '), preview);
                    }

                    // Spooler faylni biroz kechikib yozishi mumkin.
                    long size = 0;
                    for (int i = 0; i < 100 && size == 0; i++)
                    {
                        if (File.Exists(pdf))
                        {
                            size = new FileInfo(pdf).Length;
                        }

                        if (size == 0)
                        {
                            Thread.Sleep(150);
                        }
                    }

                    if (size == 0)
                    {
                        return ("error: PDF fayl paydo bo'lmadi", preview);
                    }

                    Thread.Sleep(300);
                    byte[] head = new byte[5];
                    using (var stream = new FileStream(pdf, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        stream.ReadExactly(head);
                    }

                    if (System.Text.Encoding.ASCII.GetString(head) != "%PDF-")
                    {
                        return ("error: fayl PDF emas", preview);
                    }

                    pdfResult = string.Create(CultureInfo.InvariantCulture, $"pdf=ok {new FileInfo(pdf).Length / 1024} KB");
                }

                form.Close();
            }

            return (string.Create(CultureInfo.InvariantCulture, $"ok 2-per-page dpi={dpi} {pdfResult} printers={Printers.Installed().Count}"), preview);
        }
        catch (Exception ex) when (ex is Win32Exception or ExternalException or IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            return ("error: " + ex.GetType().Name + ": " + ex.Message.Replace('\r', ' ').Replace('\n', ' '), null);
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // vaqtinchalik papka
            }
        }
    }

    /// <summary>
    /// CI tutun testi: PNG va JPG'ga haqiqatan saqlab, qayta o'qib tekshiradi (vaqtinchalik papkada — foydalanuvchi
    /// papkalariga tegmaydi), so'ng tarix oynasini ochib rasmga oladi.
    /// </summary>
    private static (string Result, string? Preview) RunSmokeSave(Bitmap image)
    {
        string dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.smoke." + Guid.NewGuid().ToString("N"));
        try
        {
            var history = new ScreenshotHistory();
            var now = DateTime.Now;
            foreach (var format in new[] { ImageFileFormat.Png, ImageFileFormat.Jpg })
            {
                string path = ScreenshotFileNamer.CreateUniquePath(dir, now, format, File.Exists);
                ImageSaver.Save(image, path, format);
                using var back = Thumbnails.Load(path);
                if (back is null || back.Size != image.Size)
                {
                    return ("error: " + format + " fayl o'qilmadi yoki o'lchami mos emas", null);
                }

                var edge = back.GetPixel(10, back.Height / 2);
                if (edge.R < 170 || edge.G > 100 || edge.B > 100)
                {
                    return ("error: " + format + " faylda chizma yo'q (" + edge + ")", null);
                }

                history.Add(new HistoryEntry(path, now, image.Width, image.Height));
            }

            string preview;
            using (var form = new HistoryForm(history, dir, () => { }) { StartPosition = FormStartPosition.Manual, Location = Point.Empty })
            {
                form.Show();
                form.LoadThumbnailsNow();
                form.Refresh();
                using var shot = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(shot, new Rectangle(Point.Empty, form.Size));
                preview = "ok png=" + SmokeTest.ToJpegBase64(shot, 0.5, 45);
                form.Close();
            }

            return (string.Create(CultureInfo.InvariantCulture, $"ok png+jpg tarix={history.Entries.Count}"), preview);
        }
        catch (Exception ex) when (ImageSaver.IsSaveError(ex) || ex is InvalidOperationException)
        {
            return ("error: " + ex.GetType().Name + ": " + ex.Message.Replace('\r', ' ').Replace('\n', ' '), null);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // vaqtinchalik papka
            }
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

        if (_exitSignal is not null)
        {
            // O'rnatuvchi yangilash yoki o'chirishdan oldin yopilishni so'raydi.
            _exitWait = ThreadPool.RegisterWaitForSingleObject(
                _exitSignal,
                (_, _) => _ui?.Post(_ => ExitThread(), null),
                null,
                Timeout.Infinite,
                executeOnlyOnce: true);
        }
    }

    private void ShowBalloon(string text, ToolTipIcon icon, Action? onClick = null)
    {
        if (!_options.SmokeTest)
        {
            _balloonClick = onClick;
            _trayIcon.ShowBalloonTip(5000, Strings.AppName, text, icon);
        }
    }
}
