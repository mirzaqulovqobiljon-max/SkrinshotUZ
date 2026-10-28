using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Printing;
using SkrinshotUZ.Core.Storage;
using SkrinshotUZ.Editing;
using SkrinshotUZ.Printing;
using SkrinshotUZ.Storage;

namespace SkrinshotUZ.UI;

/// <summary>Chop etish natijasi.</summary>
/// <param name="Pages">Varaqlar soni.</param>
/// <param name="Copies">Nusxalar.</param>
/// <param name="PdfPath">PDF ga saqlangan bo'lsa — fayl yo'li.</param>
internal sealed record PrintOutcome(int Pages, int Copies, string? PdfPath);

/// <summary>
/// Chop etish oynasi: chapda sozlamalar (katta, tushunarli), o'ngda A4 varaqning jonli ko'rinishi va rasmning mm o'lchami.
/// Joylashuv <see cref="PrintPlanner"/>'da, chizish <see cref="PrintRenderer"/>'da — oynada faqat boshqaruv.
/// </summary>
internal sealed class PrintForm : Form
{
    private readonly Bitmap _image;
    private readonly PrintImage _imageInfo;
    private readonly PrintPreferences _prefs;
    private readonly IReadOnlyList<HistoryEntry> _history;
    private readonly string _pdfFolder;
    private readonly Font _font;
    private readonly Font _bold;
    private readonly Icon _icon;
    private readonly Dictionary<string, HardMargins> _hardMargins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDisposable> _owned = new();

    private readonly RadioButton[] _modeButtons;
    private readonly Panel _secondPanel;
    private readonly ComboBox _secondCombo;
    private readonly Panel _overflowPanel;
    private readonly Label _overflowLabel;
    private readonly RadioButton _overflowFit;
    private readonly RadioButton _overflowSplit;
    private readonly ComboBox _orientation;
    private readonly ComboBox _margin;
    private readonly CheckBox _grayscale;
    private readonly CheckBox _showDate;
    private readonly TextBox _title;
    private readonly NumericUpDown _copies;
    private readonly ComboBox _printer;
    private readonly PagePreview _preview;
    private readonly Label _sizeInfo;
    private readonly Label _pagesInfo;
    private readonly Button _prevPage;
    private readonly Button _nextPage;
    private readonly Label _pageNumber;
    private readonly Button _printButton;
    private readonly Button _pdfButton;

    private Bitmap? _second;
    private PrintImage? _secondInfo;
    private PrintPlan? _plan;
    private int _pageIndex;
    private bool _loading = true;

    /// <summary>Yangi oyna.</summary>
    /// <param name="image">Chop etiladigan rasm (egasi — chaqiruvchi).</param>
    /// <param name="prefs">Oxirgi sozlamalar (oyna ularni yangilaydi — chaqiruvchi saqlaydi).</param>
    /// <param name="history">Saqlangan skrinshotlar ("Bir varaqqa 2 ta"da ikkinchi rasm uchun).</param>
    /// <param name="pdfFolder">PDF uchun standart papka.</param>
    public PrintForm(Bitmap image, PrintPreferences prefs, IReadOnlyList<HistoryEntry> history, string pdfFolder)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _prefs = prefs ?? throw new ArgumentNullException(nameof(prefs));
        _history = history ?? Array.Empty<HistoryEntry>();
        _pdfFolder = pdfFolder;
        _imageInfo = new PrintImage(image.Width, image.Height, ImageDpi.Of(image));

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        _font = new Font("Segoe UI", 10f);
        _bold = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _icon = AppIcon.LoadLarge();
        Font = _font;
        Icon = _icon;
        Text = Strings.Print_Title;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 680);
        MinimumSize = new Size(760, 560);
        BackColor = Color.White;
        KeyPreview = true;

        var tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400 };
        _owned.Add(tips);

        // ---------- Chap: sozlamalar ----------
        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12, 10, 8, 8),
        };

        left.Controls.Add(new Label { Text = Strings.Print_ModeHeader, Font = _bold, AutoSize = true, Margin = new Padding(0, 0, 0, 6) });

        var modes = new[]
        {
            (PrintMode.FullPage, Strings.Print_ModeFull, Strings.Print_ModeFull_Tip),
            (PrintMode.HalfPage, Strings.Print_ModeHalf, Strings.Print_ModeHalf_Tip),
            (PrintMode.TwoPerPage, Strings.Print_ModeTwo, Strings.Print_ModeTwo_Tip),
            (PrintMode.ActualSize, Strings.Print_ModeActual, Strings.Print_ModeActual_Tip),
        };
        _modeButtons = new RadioButton[modes.Length];
        int iconH = (int)Math.Round(40 * DeviceDpi / 96.0);
        for (int i = 0; i < modes.Length; i++)
        {
            var (mode, text, tip) = modes[i];
            var icon = ModeIcon(mode, iconH);
            _owned.Add(icon);
            var rb = new RadioButton
            {
                Text = "  " + text,
                Image = icon,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Width = 290,
                Height = iconH + 10,
                Tag = mode,
                Margin = new Padding(0, 0, 0, 2),
            };
            tips.SetToolTip(rb, tip);
            rb.CheckedChanged += (_, _) =>
            {
                if (rb.Checked)
                {
                    OnOptionsChanged();
                }
            };
            _modeButtons[i] = rb;
            left.Controls.Add(rb);
        }

        // "Bir varaqqa 2 ta": pastki yarmida nima?
        _secondCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        _secondCombo.Items.Add(Strings.Print_SecondSame);
        foreach (var e in _history)
        {
            _secondCombo.Items.Add(e);
        }

        _secondCombo.Format += (_, e) => e.Value = e.ListItem is HistoryEntry h ? Path.GetFileName(h.Path) + "  ·  " + h.DisplayText : e.ListItem;
        _secondCombo.SelectedIndex = 0;
        _secondCombo.SelectedIndexChanged += (_, _) => OnSecondChanged();
        _secondPanel = Group(new Label { Text = Strings.Print_SecondImage, AutoSize = true }, _secondCombo);
        _secondPanel.Padding = new Padding(28, 0, 0, 6);
        left.Controls.Add(_secondPanel);

        // "Asl o'lchamda" sig'masa
        _overflowLabel = new Label { AutoSize = true, MaximumSize = new Size(270, 0), ForeColor = Color.FromArgb(120, 70, 0) };
        _overflowFit = new RadioButton { Text = Strings.Print_OverflowFit, AutoSize = true };
        _overflowSplit = new RadioButton { Text = Strings.Print_OverflowSplit, AutoSize = true };
        _overflowFit.CheckedChanged += (_, _) => OnOptionsChanged();
        _overflowSplit.CheckedChanged += (_, _) => OnOptionsChanged();
        _overflowPanel = Group(_overflowLabel, _overflowFit, _overflowSplit);
        _overflowPanel.BackColor = Color.FromArgb(255, 246, 214);
        _overflowPanel.Padding = new Padding(8, 6, 8, 6);
        _overflowPanel.Margin = new Padding(0, 2, 0, 8);
        left.Controls.Add(_overflowPanel);

        // Oddiy sozlamalar jadvali
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _orientation = Combo(new[] { Strings.Print_OrientationAuto, Strings.Print_OrientationPortrait, Strings.Print_OrientationLandscape }, 160);
        _margin = Combo(PrintMargins.AllowedUserMarginsMm.Select(m => string.Format(CultureInfo.InvariantCulture, Strings.Print_MarginValue, m)).ToArray(), 160);
        _copies = new NumericUpDown { Minimum = 1, Maximum = PrintPreferences.MaxCopies, Width = 80, Value = 1 };
        _copies.ValueChanged += (_, _) => OnOptionsChanged();
        AddRow(grid, Strings.Print_Orientation, _orientation);
        AddRow(grid, Strings.Print_Margin, _margin);
        AddRow(grid, Strings.Print_Copies, _copies);
        left.Controls.Add(grid);

        _grayscale = new CheckBox { Text = Strings.Print_Grayscale, AutoSize = true, Margin = new Padding(3, 8, 3, 2) };
        _showDate = new CheckBox { Text = Strings.Print_ShowDate, AutoSize = true, Margin = new Padding(3, 2, 3, 6) };
        _grayscale.CheckedChanged += (_, _) => OnOptionsChanged();
        _showDate.CheckedChanged += (_, _) => OnOptionsChanged();
        left.Controls.Add(_grayscale);
        left.Controls.Add(_showDate);

        left.Controls.Add(new Label { Text = Strings.Print_TitleLabel, AutoSize = true, Margin = new Padding(3, 4, 3, 2) });
        _title = new TextBox { Width = 280, PlaceholderText = Strings.Print_TitlePlaceholder, MaxLength = 120 };
        _title.TextChanged += (_, _) => OnOptionsChanged();
        left.Controls.Add(_title);

        left.Controls.Add(new Label { Text = Strings.Print_Printer, AutoSize = true, Margin = new Padding(3, 10, 3, 2) });
        _printer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        _printer.SelectedIndexChanged += (_, _) => OnPrinterChanged();
        left.Controls.Add(_printer);

        // ---------- O'ng: ko'rinish ----------
        _preview = new PagePreview { Dock = DockStyle.Fill };
        _sizeInfo = new Label { AutoSize = true, Font = _bold, Margin = new Padding(3, 6, 3, 0) };
        _pagesInfo = new Label { AutoSize = true, ForeColor = Color.FromArgb(90, 90, 90), Margin = new Padding(3, 2, 3, 0) };
        _prevPage = new Button { Text = "◀", Width = 40, Height = 30, FlatStyle = FlatStyle.System };
        _nextPage = new Button { Text = "▶", Width = 40, Height = 30, FlatStyle = FlatStyle.System };
        _pageNumber = new Label { AutoSize = true, Padding = new Padding(4, 7, 4, 0) };
        tips.SetToolTip(_prevPage, Strings.Print_PrevPage);
        tips.SetToolTip(_nextPage, Strings.Print_NextPage);
        _prevPage.Click += (_, _) => ShowPage(_pageIndex - 1);
        _nextPage.Click += (_, _) => ShowPage(_pageIndex + 1);
        var nav = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        nav.Controls.AddRange(new Control[] { _prevPage, _pageNumber, _nextPage });

        var info = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        info.Controls.AddRange(new Control[] { _sizeInfo, _pagesInfo, nav });

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.FromArgb(232, 234, 237), Padding = new Padding(10) };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.Controls.Add(_preview, 0, 0);
        right.Controls.Add(info, 0, 1);

        // ---------- Pastda: tugmalar ----------
        int glyph = (int)Math.Round(18 * DeviceDpi / 96.0);
        var printIcon = IconPainter.Draw(ToolbarIcon.Print, glyph);
        var saveIcon = IconPainter.Draw(ToolbarIcon.Save, glyph);
        _owned.Add(printIcon);
        _owned.Add(saveIcon);
        _printButton = BigButton(Strings.Print_Print, printIcon, Print);
        _printButton.Font = _bold;
        _pdfButton = BigButton(Strings.Print_Pdf, saveIcon, SavePdf);
        var cancel = BigButton(Strings.Print_Cancel, null, () => Close());
        cancel.DialogResult = DialogResult.Cancel;
        tips.SetToolTip(_printButton, Strings.Print_Print_Tip);
        tips.SetToolTip(_pdfButton, Strings.Print_Pdf_Tip);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.AddRange(new Control[] { cancel, _pdfButton, _printButton });
        AcceptButton = _printButton;
        CancelButton = cancel;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(left, 0, 0);
        root.Controls.Add(right, 1, 0);
        root.Controls.Add(buttons, 0, 1);
        root.SetColumnSpan(buttons, 2);
        Controls.Add(root);

        ResumeLayout(false);
        PerformLayout();

        LoadPreferences();
        LoadPrinters();
        _loading = false;
        Replan();
    }

    /// <summary>Natija (chop etilgan yoki PDF saqlangan bo'lsa).</summary>
    public PrintOutcome? Outcome { get; private set; }

    /// <summary>Joriy reja (testlar va tutun testi uchun).</summary>
    internal PrintPlan? Plan => _plan;

    /// <summary>Rejimni dasturdan tanlash (tutun testi uchun).</summary>
    internal void SetModeForTest(PrintMode mode, string? title = null, bool showDate = false)
    {
        _modeButtons.First(b => (PrintMode)b.Tag! == mode).Checked = true;
        _title.Text = title ?? string.Empty;
        _showDate.Checked = showDate;
        Replan();
    }

    /// <summary>Ko'rinishni darhol chizish (tutun testida DrawToBitmap'dan oldin).</summary>
    internal void RenderPreviewNow() => _preview.RenderNow();

    /// <summary>"Microsoft Print to PDF" orqali berilgan faylga (tutun testi uchun — xabar oynasisiz).</summary>
    /// <returns>Xato matni yoki <c>null</c>.</returns>
    internal string? PrintToPdfForTest(string path) => RunJob(path);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.P:
                Print();
                return true;
            case Keys.PageDown:
                ShowPage(_pageIndex + 1);
                return true;
            case Keys.PageUp:
                ShowPage(_pageIndex - 1);
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _second?.Dispose();
            foreach (var d in _owned)
            {
                d.Dispose();
            }

            _font.Dispose();
            _bold.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }

    // ---------- Sozlamalar ↔ boshqaruv elementlari ----------

    private PrintMode SelectedMode => (PrintMode)(_modeButtons.FirstOrDefault(b => b.Checked)?.Tag ?? PrintMode.FullPage);

    private void LoadPreferences()
    {
        _modeButtons.First(b => (PrintMode)b.Tag! == _prefs.Mode).Checked = true;
        _orientation.SelectedIndex = (int)_prefs.Orientation;
        _margin.SelectedIndex = Math.Max(0, PrintMargins.AllowedUserMarginsMm.ToList().IndexOf(_prefs.MarginMm));
        _overflowFit.Checked = _prefs.Overflow == OverflowChoice.FitToPage;
        _overflowSplit.Checked = _prefs.Overflow == OverflowChoice.SplitPages;
        _grayscale.Checked = _prefs.Grayscale;
        _showDate.Checked = _prefs.ShowDate;
        _copies.Value = Math.Clamp(_prefs.Copies, 1, PrintPreferences.MaxCopies);
        _orientation.SelectedIndexChanged += (_, _) => OnOptionsChanged();
        _margin.SelectedIndexChanged += (_, _) => OnOptionsChanged();
    }

    private void StorePreferences()
    {
        _prefs.Mode = SelectedMode;
        _prefs.Orientation = (OrientationChoice)Math.Max(0, _orientation.SelectedIndex);
        _prefs.MarginMm = PrintMargins.AllowedUserMarginsMm[Math.Max(0, _margin.SelectedIndex)];
        _prefs.Overflow = _overflowSplit.Checked ? OverflowChoice.SplitPages : OverflowChoice.FitToPage;
        _prefs.Grayscale = _grayscale.Checked;
        _prefs.ShowDate = _showDate.Checked;
        _prefs.Copies = (int)_copies.Value;
        _prefs.PrinterName = _printer.SelectedItem as string;
    }

    private void LoadPrinters()
    {
        var installed = Printers.Installed();
        _printer.Items.Clear();
        foreach (var p in installed)
        {
            _printer.Items.Add(p);
        }

        if (installed.Count == 0)
        {
            _printer.Items.Add(Strings.Print_NoPrinters);
            _printer.SelectedIndex = 0;
            _printer.Enabled = false;
            _printButton.Enabled = false;
            return;
        }

        string? wanted = _prefs.PrinterName is string saved && installed.Contains(saved, StringComparer.OrdinalIgnoreCase)
            ? saved
            : Printers.Default();
        int index = wanted is null ? -1 : _printer.Items.IndexOf(wanted);
        _printer.SelectedIndex = index >= 0 ? index : 0;
    }

    private void OnPrinterChanged()
    {
        if (_printer.SelectedItem is not string name || _hardMargins.ContainsKey(name))
        {
            Replan();
            return;
        }

        // Printer drayveri sekin javob berishi mumkin (tarmoq printeri) — oyna qotib qolmasin.
        _ = Task.Run(() => Printers.HardMarginsOf(name)).ContinueWith(
            t =>
            {
                if (IsDisposed)
                {
                    return;
                }

                _hardMargins[name] = t.Status == TaskStatus.RanToCompletion ? t.Result : HardMargins.None;
                Replan();
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void OnSecondChanged()
    {
        _second?.Dispose();
        _second = null;
        _secondInfo = null;
        if (_secondCombo.SelectedItem is HistoryEntry entry)
        {
            _second = Thumbnails.Load(entry.Path);
            if (_second is null)
            {
                MessageBox.Show(this, Strings.Print_LoadFailed, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                _secondCombo.SelectedIndex = 0;
                return;
            }

            _secondInfo = new PrintImage(_second.Width, _second.Height, ImageDpi.Of(_second));
        }

        Replan();
    }

    private void OnOptionsChanged()
    {
        if (!_loading)
        {
            Replan();
        }
    }

    private HardMargins CurrentHardMargins(string? printer) =>
        printer is not null && _hardMargins.TryGetValue(printer, out var m) ? m : HardMargins.None;

    private PrintPlan BuildPlan(HardMargins hard, OverflowChoice? overflow = null)
    {
        string? caption = PrintPlanner.BuildCaption(_title.Text, _showDate.Checked ? DateTime.Now : null);
        var options = new PrintOptions
        {
            Mode = SelectedMode,
            Orientation = (OrientationChoice)Math.Max(0, _orientation.SelectedIndex),
            MarginMm = PrintMargins.AllowedUserMarginsMm[Math.Max(0, _margin.SelectedIndex)],
            Overflow = overflow ?? (_overflowSplit.Checked ? OverflowChoice.SplitPages : OverflowChoice.FitToPage),
            Caption = caption,
        };
        return PrintPlanner.Create(_imageInfo, SelectedMode == PrintMode.TwoPerPage ? _secondInfo : null, options, hard);
    }

    private void Replan()
    {
        if (_loading)
        {
            return;
        }

        var mode = SelectedMode;
        var hard = CurrentHardMargins(_printer.SelectedItem as string);
        _plan = BuildPlan(hard);

        _secondPanel.Visible = mode == PrintMode.TwoPerPage;
        _orientation.Enabled = mode is PrintMode.FullPage or PrintMode.ActualSize;
        _overflowPanel.Visible = mode == PrintMode.ActualSize && _plan.ActualSizeOverflows;
        if (_overflowPanel.Visible)
        {
            int splitPages = BuildPlan(hard, OverflowChoice.SplitPages).Pages.Count;
            _overflowLabel.Text = string.Format(CultureInfo.InvariantCulture, Strings.Print_Overflow, PrintText.Size(_plan.ActualSizeMm));
            _overflowSplit.Text = Strings.Print_OverflowSplit + " (" + string.Format(CultureInfo.InvariantCulture, Strings.Print_PagesCount, splitPages) + ")";
        }

        _sizeInfo.Text = PrintText.SizeInfo(_plan);
        _pagesInfo.Text = PrintText.PagesInfo(_plan, (int)_copies.Value);
        ShowPage(Math.Min(_pageIndex, _plan.Pages.Count - 1));
    }

    private void ShowPage(int index)
    {
        if (_plan is null)
        {
            return;
        }

        _pageIndex = Math.Clamp(index, 0, _plan.Pages.Count - 1);
        bool many = _plan.Pages.Count > 1;
        _prevPage.Visible = _nextPage.Visible = _pageNumber.Visible = many;
        _prevPage.Enabled = _pageIndex > 0;
        _nextPage.Enabled = _pageIndex < _plan.Pages.Count - 1;
        _pageNumber.Text = string.Format(CultureInfo.InvariantCulture, Strings.Print_PageOf, _pageIndex + 1, _plan.Pages.Count);
        _preview.SetPage(_plan.Pages[_pageIndex], Images(), _grayscale.Checked);
    }

    private IReadOnlyList<Image> Images() => _second is null ? new Image[] { _image } : new Image[] { _image, _second };

    // ---------- Chop etish ----------

    private void Print()
    {
        if (!_printButton.Enabled)
        {
            return;
        }

        if (Report(RunJob(pdfPath: null)))
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void SavePdf()
    {
        if (!Printers.HasPdfPrinter())
        {
            MessageBox.Show(this, Strings.Print_PdfMissing, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(_pdfFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // foydalanuvchi boshqa papka tanlaydi
        }

        using var dialog = new SaveFileDialog
        {
            Title = Strings.Print_PdfDialogTitle,
            Filter = Strings.Print_PdfFilter + " (*.pdf)|*.pdf",
            InitialDirectory = _pdfFolder,
            FileName = Path.ChangeExtension(ScreenshotFileNamer.CreateFileName(DateTime.Now, ImageFileFormat.Png), ".pdf"),
            DefaultExt = "pdf",
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (Report(RunJob(dialog.FileName)))
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Xato bo'lsa xabar beradi (oyna ochiq qoladi — foydalanuvchi boshqa printer tanlashi mumkin).</summary>
    private bool Report(string? error)
    {
        if (error is null)
        {
            return true;
        }

        MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, Strings.Print_Failed, error), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    /// <summary>Chop etadi.</summary>
    /// <returns>Xato matni yoki <c>null</c> (muvaffaqiyat).</returns>
    private string? RunJob(string? pdfPath)
    {
        StorePreferences();
        string? printer = pdfPath is null ? _printer.SelectedItem as string : Printers.PdfPrinterName;
        var hard = pdfPath is null ? CurrentHardMargins(printer) : Printers.HardMarginsOf(Printers.PdfPrinterName);
        var plan = BuildPlan(hard);
        int copies = pdfPath is null ? (int)_copies.Value : 1;

        UseWaitCursor = true;
        try
        {
            if (pdfPath is not null && File.Exists(pdfPath))
            {
                File.Delete(pdfPath); // spooler eski faylni ustiga yozolmay qolmasin
            }

            using var job = new PrintJob(plan, Images(), _grayscale.Checked, printer, copies, pdfPath);
            job.Print();
            Outcome = new PrintOutcome(plan.Pages.Count, copies, pdfPath);
            return null;
        }
        catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception or ExternalException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ex.Message;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    // ---------- Yordamchilar ----------

    private static Panel Group(params Control[] children)
    {
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 290 };
        panel.Controls.AddRange(children);
        return panel;
    }

    private static ComboBox Combo(string[] items, int width)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
        c.Items.AddRange(items);
        c.SelectedIndex = 0;
        return c;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) }, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private static Button BigButton(string text, Image? image, Action onClick)
    {
        var b = new Button
        {
            Text = image is null ? text : "  " + text,
            Image = image,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AutoSize = true,
            MinimumSize = new Size(130, 40),
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(6),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Rejim belgisi: kichik A4 varaq va rasm qayerga tushishi (o'qituvchi bir qarashda tushunsin).</summary>
    internal static Bitmap ModeIcon(PrintMode mode, int height)
    {
        int h = Math.Max(16, height);
        int w = (int)Math.Round(h * 210.0 / 297.0);
        var bmp = new Bitmap(w + 2, h + 2);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var paper = new SolidBrush(Color.White);
        using var border = new Pen(Color.FromArgb(110, 110, 110), Math.Max(1f, h / 40f));
        using var fill = new SolidBrush(Color.FromArgb(0, 120, 215));
        var page = new RectangleF(1, 1, w - 1, h - 1);
        g.FillRectangle(paper, page);
        float m = w * 0.12f;
        switch (mode)
        {
            case PrintMode.FullPage:
                g.FillRectangle(fill, page.X + m, page.Y + (h * 0.3f), page.Width - (2 * m), h * 0.4f);
                break;
            case PrintMode.HalfPage:
                g.FillRectangle(fill, page.X + m, page.Y + m, page.Width - (2 * m), (h / 2f) - (2 * m));
                break;
            case PrintMode.TwoPerPage:
                g.FillRectangle(fill, page.X + m, page.Y + m, page.Width - (2 * m), (h / 2f) - (2 * m));
                g.FillRectangle(fill, page.X + m, page.Y + (h / 2f) + m, page.Width - (2 * m), (h / 2f) - (2 * m));
                using (var dash = new Pen(Color.FromArgb(110, 110, 110), Math.Max(1f, h / 40f)) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(dash, page.X, page.Y + (h / 2f), page.Right, page.Y + (h / 2f));
                }

                break;
            default:
                g.FillRectangle(fill, page.X + m, page.Y + m, page.Width * 0.45f, h * 0.22f);
                break;
        }

        g.DrawRectangle(border, page.X, page.Y, page.Width, page.Height);
        return bmp;
    }
}

/// <summary>
/// A4 varaqning ko'rinishi: varaq soya bilan markazda, ichida haqiqiy chop etish chizmasi (o'sha <see cref="PrintRenderer"/>).
/// O'lcham o'zgarganda qayta chizish biroz kechiktiriladi — eski kompyuterda ham oyna silliq cho'ziladi.
/// </summary>
internal sealed class PagePreview : Control
{
    private readonly System.Windows.Forms.Timer _resizeTimer = new() { Interval = 120 };
    private PrintPage? _page;
    private IReadOnlyList<Image>? _images;
    private bool _grayscale;
    private Bitmap? _rendered;

    public PagePreview()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(232, 234, 237);
        _resizeTimer.Tick += (_, _) =>
        {
            _resizeTimer.Stop();
            RenderNow();
        };
    }

    /// <summary>Ko'rsatiladigan varaq.</summary>
    public void SetPage(PrintPage page, IReadOnlyList<Image> images, bool grayscale)
    {
        _page = page;
        _images = images;
        _grayscale = grayscale;
        RenderNow();
    }

    /// <summary>Hozirgi o'lchamda qayta chizadi.</summary>
    public void RenderNow()
    {
        _rendered?.Dispose();
        _rendered = null;
        var target = PageRect();
        if (_page is not null && _images is not null && target.Width > 4 && target.Height > 4)
        {
            var bmp = new Bitmap(target.Width, target.Height);
            float pxPerMm = target.Width / (float)_page.Size.Width;
            bmp.SetResolution(pxPerMm * 25.4f, pxPerMm * 25.4f); // Graphics millimetrlari shu bitmap'ga to'g'ri tushadi
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                PrintRenderer.DrawPage(g, _page, _images, PointF.Empty, _grayscale, showMargins: true);
            }

            _rendered = bmp;
        }

        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_page is null)
        {
            return;
        }

        var r = PageRect();
        using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
        {
            e.Graphics.FillRectangle(shadow, r.X + 4, r.Y + 4, r.Width, r.Height);
        }

        if (_rendered is not null)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            e.Graphics.DrawImage(_rendered, r); // o'lcham o'zgarayotganda — vaqtincha cho'zilgan holda
        }
        else
        {
            e.Graphics.FillRectangle(Brushes.White, r);
        }

        using var border = new Pen(Color.FromArgb(160, 160, 160));
        e.Graphics.DrawRectangle(border, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resizeTimer.Dispose();
            _rendered?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Rectangle PageRect()
    {
        if (_page is null)
        {
            return Rectangle.Empty;
        }

        const int pad = 12;
        double w = Math.Max(0, ClientSize.Width - (2 * pad) - 4);
        double h = Math.Max(0, ClientSize.Height - (2 * pad) - 4);
        double scale = Math.Min(w / _page.Size.Width, h / _page.Size.Height);
        int pw = (int)(_page.Size.Width * scale);
        int ph = (int)(_page.Size.Height * scale);
        return new Rectangle((ClientSize.Width - pw) / 2, (ClientSize.Height - ph) / 2, pw, ph);
    }
}
