using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Storage;
using SkrinshotUZ.Storage;

namespace SkrinshotUZ.UI;

/// <summary>
/// "Oxirgi skrinshotlar" oynasi: saqlangan rasmlar rasmchalari bilan. Ikki marta bosish — ochish.
/// Rasmchalar fonda yuklanadi (eski kompyuterda ham oyna darhol ochiladi).
/// </summary>
internal sealed class HistoryForm : Form
{
    private readonly ScreenshotHistory _history;
    private readonly string _folder;
    private readonly Action _changed;
    private readonly ImageList _thumbs;
    private readonly ListView _list;
    private readonly Label _empty;
    private readonly Button _open;
    private readonly Button _show;
    private readonly Button _copy;
    private readonly Button _remove;
    private readonly Button? _print;
    private readonly Font _font;
    private readonly Icon _icon;

    /// <summary>Yangi oyna.</summary>
    /// <param name="history">Tarix (oyna uni o'zgartirishi mumkin — keyin <paramref name="changed"/> chaqiriladi).</param>
    /// <param name="folder">Skrinshotlar papkasi ("Papkani ochish" tugmasi uchun).</param>
    /// <param name="changed">Tarix o'zgarganda (diskka yozish uchun).</param>
    /// <param name="print">"Chop etish" tugmasi uchun (fayl yo'li bilan); <c>null</c> — tugma yo'q.</param>
    public HistoryForm(ScreenshotHistory history, string folder, Action changed, Action<string>? print = null)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _folder = folder;
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        _font = new Font("Segoe UI", 10f);
        _icon = AppIcon.LoadLarge();
        Font = _font;
        Icon = _icon;
        Text = Strings.History_Title;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 560);
        MinimumSize = new Size(520, 360);
        BackColor = Color.White;

        double scale = DeviceDpi / 96.0;
        int thumbWidth = Math.Min(256, (int)Math.Round(176 * scale));
        _thumbs = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(thumbWidth, (int)Math.Round(thumbWidth * 0.625)) };

        var hint = new Label { Text = Strings.History_Hint, AutoSize = true, Padding = new Padding(4, 8, 4, 8), ForeColor = Color.FromArgb(80, 80, 80) };

        _list = new ListView
        {
            View = View.LargeIcon,
            LargeImageList = _thumbs,
            Dock = DockStyle.Fill,
            MultiSelect = false,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            ShowItemToolTips = true,
        };
        _list.ItemActivate += (_, _) => OpenSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();

        _empty = new Label
        {
            Text = Strings.History_Empty,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(100, 100, 100),
            Visible = false,
        };

        var middle = new Panel { Dock = DockStyle.Fill };
        middle.Controls.Add(_list);
        middle.Controls.Add(_empty);

        _open = MakeButton(Strings.History_Open, OpenSelected);
        _show = MakeButton(Strings.History_ShowInFolder, () => WithSelected(Shell.ShowInFolder));
        _copy = MakeButton(Strings.History_Copy, CopySelected);
        if (print is not null)
        {
            _print = MakeButton(Strings.History_Print, () => WithSelected(print));
        }

        _remove = MakeButton(Strings.History_Remove, RemoveSelected);
        var folderButton = MakeButton(Strings.History_OpenFolder, () => Shell.OpenFolder(_folder));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        buttons.Controls.AddRange(new Control[] { _open, _show, _copy });
        if (_print is not null)
        {
            buttons.Controls.Add(_print);
        }

        buttons.Controls.AddRange(new Control[] { _remove, folderButton });

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(hint, 0, 0);
        layout.Controls.Add(middle, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        ResumeLayout(false);
        PerformLayout();

        FillList();
        Shown += async (_, _) => await LoadThumbnailsAsync();
    }

    /// <summary>Tarix tashqarida o'zgardi (yangi skrinshot saqlandi) — ro'yxatni yangilash.</summary>
    public async void Reload()
    {
        FillList();
        await LoadThumbnailsAsync();
    }

    /// <summary>Rasmchalarni darhol (sinxron) yuklash — faqat tutun testi uchun.</summary>
    internal void LoadThumbnailsNow()
    {
        foreach (ListViewItem item in _list.Items)
        {
            AddThumbnail(item, Thumbnails.Create(((HistoryEntry)item.Tag!).Path, _thumbs.ImageSize));
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // Fayllar shu orada o'chirilgan yoki ko'chirilgan bo'lishi mumkin.
        if (_history.RemoveMissing(File.Exists) > 0)
        {
            _changed();
            FillList();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbs.Dispose();
            _font.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }

    private Button MakeButton(string text, Action onClick)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(0, 36), Padding = new Padding(10, 2, 10, 2), Margin = new Padding(4), FlatStyle = FlatStyle.System };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void FillList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in _history.Entries)
        {
            var item = new ListViewItem(entry.DisplayText) { Tag = entry, ToolTipText = entry.Path };
            if (_thumbs.Images.ContainsKey(entry.Path))
            {
                item.ImageKey = entry.Path;
            }

            _list.Items.Add(item);
        }

        _list.EndUpdate();
        bool any = _list.Items.Count > 0;
        _list.Visible = any;
        _empty.Visible = !any;
        if (any)
        {
            _list.Items[0].Selected = true;
        }

        UpdateButtons();
    }

    private async Task LoadThumbnailsAsync()
    {
        var size = _thumbs.ImageSize;
        foreach (ListViewItem item in _list.Items.Cast<ListViewItem>().ToList())
        {
            var entry = (HistoryEntry)item.Tag!;
            if (_thumbs.Images.ContainsKey(entry.Path))
            {
                continue;
            }

            var thumb = await Task.Run(() => Thumbnails.Create(entry.Path, size));
            if (IsDisposed)
            {
                thumb?.Dispose();
                return;
            }

            AddThumbnail(item, thumb);
        }
    }

    private void AddThumbnail(ListViewItem item, Bitmap? thumb)
    {
        if (thumb is null)
        {
            return;
        }

        string key = ((HistoryEntry)item.Tag!).Path;
        if (!_thumbs.Images.ContainsKey(key))
        {
            _thumbs.Images.Add(key, thumb); // ImageList o'z nusxasini oladi
        }

        thumb.Dispose();
        item.ImageKey = key;
    }

    private void UpdateButtons()
    {
        bool selected = _list.SelectedItems.Count > 0;
        _open.Enabled = selected;
        _show.Enabled = selected;
        _copy.Enabled = selected;
        _remove.Enabled = selected;
        if (_print is not null)
        {
            _print.Enabled = selected;
        }
    }

    private void WithSelected(Action<string> action)
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        string path = ((HistoryEntry)_list.SelectedItems[0].Tag!).Path;
        if (!File.Exists(path))
        {
            MessageBox.Show(this, Strings.History_FileMissing, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _history.Remove(path);
            _changed();
            FillList();
            return;
        }

        action(path);
    }

    private void OpenSelected() => WithSelected(Shell.Open);

    private void CopySelected() => WithSelected(path =>
    {
        using var image = Thumbnails.Load(path);
        if (image is not null)
        {
            var data = new DataObject();
            data.SetImage(image);
            try
            {
                Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 100);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                MessageBox.Show(this, Strings.Capture_ClipboardBusy, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    });

    private void RemoveSelected()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        _history.Remove(((HistoryEntry)_list.SelectedItems[0].Tag!).Path);
        _changed();
        FillList();
    }
}
