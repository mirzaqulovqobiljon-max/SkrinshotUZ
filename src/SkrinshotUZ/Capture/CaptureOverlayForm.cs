using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SkrinshotUZ.Core.Capture;
using SkrinshotUZ.Core.Editing;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Editing;
using SkrinshotUZ.Native;

namespace SkrinshotUZ.Capture;

/// <summary>Overlay qanday yakunlandi.</summary>
internal enum OverlayAction
{
    /// <summary>Buferga nusxa (Enter, Ctrl+C, "Nusxa olish").</summary>
    Copy,

    /// <summary>Faylga saqlash (Ctrl+S, "Saqlash") — yo'l <see cref="CaptureOverlayForm.SavePath"/> da.</summary>
    Save,

    /// <summary>Chop etish (Ctrl+P, "Chop etish") — overlay yopilgach chop etish oynasi ochiladi.</summary>
    Print,
}

/// <summary>
/// Soha tanlash va tahrirlash oynasi: barcha monitorlarni qoplaydi, ekranning "muzlatilgan" rasmini xiralashtirib
/// ko'rsatadi, tanlangan soha asl yorug'likda; soha yonida asboblar va amallar paneli.
/// Barcha koordinatalar — jismoniy piksellar (oyna klient sohasi = kanvas).
/// Mantiq Core'da (<see cref="SelectionController"/>, <see cref="EditorController"/>, <see cref="AnnotationHistory"/>) —
/// bu sinf faqat chizadi va kiritishni uzatadi.
/// </summary>
internal sealed class CaptureOverlayForm : Form
{
    private const int WM_DPICHANGED = 0x02E0;
    private const int WM_GETDPISCALEDSIZE = 0x02E4;

    private static readonly Color Accent = Color.FromArgb(0, 153, 181);   // bayroqdagi ko'k
    private static readonly Color LabelBack = Color.FromArgb(215, 24, 24, 24);

    private readonly ScreenLayout _layout;
    private readonly Bitmap _screen;
    private readonly Bitmap _dimmed;
    private readonly SelectionController _selection;
    private readonly AnnotationHistory _history = new();
    private readonly EditorController _editor;
    private readonly EditorToolbars _toolbars = new();
    private readonly Dictionary<int, Font> _fonts = new();
    private Rectangle _lastDirty;
    private Rectangle _lastPreview;
    private Rectangle _lastEditBounds;
    private Point _mouse;
    private int _editingTextIndex = -1;

    private TextBox? _textBox;
    private Font? _textFont;
    private float _textFontSize;

    /// <summary>Yangi oyna.</summary>
    /// <param name="layout">Monitorlar.</param>
    /// <param name="screen">Ekran rasmi (oyna egalik qilmaydi — chaqiruvchi dispose qiladi).</param>
    /// <param name="presetScreenRect">Oldindan belgilangan soha, ekran koordinatalarida (Alt+PrintScreen — faol oyna).</param>
    public CaptureOverlayForm(ScreenLayout layout, Bitmap screen, Rectangle? presetScreenRect)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        _selection = new SelectionController(layout.CanvasSize);
        _editor = new EditorController(_history);
        if (presetScreenRect is Rectangle preset)
        {
            _selection.SetSelection(layout.ToCanvas(preset));
        }

        _dimmed = CreateDimmed(screen);

        // Masshtablash o'chiq: biz piksellarda chizamiz, Windows/WinForms hech narsani kattalashtirmasin.
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        Text = Strings.AppName;
        Cursor = Cursors.Cross;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        Bounds = layout.VirtualBounds;
        _mouse = layout.ToCanvas(Cursor.Position);

        Controls.Add(_toolbars.Tools);
        Controls.Add(_toolbars.Actions);
        WireToolbars();
        _history.Changed += (_, _) => OnHistoryChanged();
    }

    /// <summary>Tanlangan soha (kanvas koordinatalarida) — faqat <see cref="DialogResult.OK"/> bo'lsa.</summary>
    public Rectangle? Result { get; private set; }

    /// <summary>Qanday yakunlandi (nusxa yoki saqlash).</summary>
    public OverlayAction Action { get; private set; } = OverlayAction.Copy;

    /// <summary>Saqlash yo'li (<see cref="OverlayAction.Save"/> bo'lsa).</summary>
    public string? SavePath { get; private set; }

    /// <summary>
    /// Saqlash oynasini ko'rsatuvchi (tray konteksti beradi — u sozlamalardagi papka va formatni biladi).
    /// Foydalanuvchi bekor qilsa — <c>null</c>, overlay ochiq qoladi.
    /// </summary>
    public Func<IWin32Window, string?>? SavePathProvider { get; set; }

    /// <summary>Chizmalar (kanvas koordinatalarida).</summary>
    public IReadOnlyList<Annotation> Annotations => _history.Items;

    /// <summary>Faqat tutun testi uchun: sohani va chizmalarni o'rnatib, darhol qabul qiladi.</summary>
    internal void AcceptForTest(Rectangle canvasRect, IEnumerable<Annotation> annotations)
    {
        _selection.SetSelection(canvasRect);
        foreach (var a in annotations)
        {
            _history.Add(a);
        }

        Accept();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Aniq jismoniy chegaralar (aralash DPI'li monitorlarda WinForms o'lchamni o'zgartirmasligi uchun).
        var vb = _layout.VirtualBounds;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, vb.X, vb.Y, vb.Width, vb.Height, NativeMethods.SWP_SHOWWINDOW);
        WindowsScreenInfo.ForceForeground(Handle);
        Activate();
        UpdateView();
    }

    protected override void WndProc(ref Message m)
    {
        // Oyna bir nechta monitorni qoplaydi: DPI o'zgarishi xabarlari e'tiborsiz — o'lcham doim virtual ekranga teng.
        if (m.Msg is WM_DPICHANGED or WM_GETDPISCALEDSIZE)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var clip = e.ClipRectangle;

        // 1:1 nusxa — eng tez usul.
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_dimmed, clip, clip, GraphicsUnit.Pixel);

        var sel = _selection.Selection;
        bool hasArea = _selection.HasSelection && sel.Width > 0 && sel.Height > 0;
        if (hasArea)
        {
            var bright = Rectangle.Intersect(sel, clip);
            if (bright.Width > 0 && bright.Height > 0)
            {
                g.DrawImage(_screen, bright, bright, GraphicsUnit.Pixel);
            }
        }

        g.CompositingMode = CompositingMode.SourceOver;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        if (hasArea && (_history.Count > 0 || _editor.Preview is not null))
        {
            // Chizmalar faqat soha ichida ko'rinadi (natija rasmidagidek).
            var state = g.Save();
            g.SetClip(Rectangle.Intersect(sel, clip));
            for (int i = 0; i < _history.Count; i++)
            {
                // Qayta tahrirlanayotgan matn TextBox ostida — chizilmaydi.
                if (i == _editingTextIndex)
                {
                    continue;
                }

                var a = _editor.IsEditing && i == _editor.SelectedIndex && _editor.EditPreview is { } live ? live : _history.Items[i];
                if (a.Bounds.IntersectsWith(clip))
                {
                    AnnotationRenderer.Draw(g, a, Point.Empty);
                }
            }

            if (_editor.Preview is { } preview)
            {
                AnnotationRenderer.Draw(g, preview, Point.Empty);
            }

            DrawAnnotationHandles(g);
            g.Restore(state);
        }

        var d = ComputeDecorations();
        if (hasArea)
        {
            DrawFrame(g, sel, d.Scale);
            DrawLabel(g, d.LabelRect, d.LabelText, d.LabelFont);
        }

        if (!d.HintRect.IsEmpty)
        {
            DrawLabel(g, d.HintRect, d.HintText, d.HintFont);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _mouse = e.Location;

        if (_textBox is not null)
        {
            // Matn kiritilayotganda boshqa joyga bosish — matnni tugatadi.
            CommitText();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            // Chizilgan shaklni ushlash: siljitish yoki uchlaridan/burchaklaridan cho'zish.
            if (_selection.State == SelectionState.Selected && _selection.Selection.Contains(e.Location))
            {
                int before = _editor.SelectedIndex;
                if (_editor.TryBeginEdit(e.Location, Grip()))
                {
                    _lastEditBounds = EditBounds();
                    if (before != _editor.SelectedIndex)
                    {
                        InvalidateSelectionArea();
                    }

                    return;
                }

                if (_editor.SelectedIndex >= 0)
                {
                    _editor.Deselect();
                    InvalidateSelectionArea();
                }
            }

            var handle = _selection.HitTest(e.Location, Grip());
            bool drawing = _editor.Tool != EditorTool.None && _selection.State == SelectionState.Selected && handle == SelectionHandle.Inside;
            if (drawing && _editor.Tool == EditorTool.Text)
            {
                BeginText(e.Location);
            }
            else if (drawing)
            {
                _editor.Begin(e.Location, ScaleAt(e.Location));
                InvalidatePreview();
            }
            else
            {
                _selection.BeginDrag(e.Location, Grip());
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            // O'ng tugma: asbob tanlangan bo'lsa — asbobni o'chirish; chizma yo'q soha — olib tashlash; aks holda chiqish.
            if (_editor.Tool != EditorTool.None)
            {
                SetTool(EditorTool.None);
            }
            else if (_selection.HasSelection && !_selection.IsDragging && _history.Count == 0)
            {
                _selection.Clear();
            }
            else if (!_selection.HasSelection)
            {
                Cancel();
                return;
            }
        }

        UpdateView();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;

        if (_editor.IsDrawing)
        {
            _editor.Move(e.Location, ShiftDown());
            InvalidatePreview();
            return;
        }

        if (_editor.IsEditing)
        {
            _editor.Edit(e.Location, ShiftDown());
            InvalidateEdit();
            return;
        }

        if (_selection.IsDragging)
        {
            _selection.Drag(e.Location);
        }

        Cursor = CursorAt(e.Location);
        UpdateView();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_editor.IsDrawing)
        {
            _editor.End(e.Location, ShiftDown());
            InvalidatePreview();
            InvalidateSelectionArea(); // yangi shakl tanlangan — tutqichlari ko'rinsin
            return;
        }

        if (_editor.IsEditing)
        {
            _editor.EndEdit(e.Location, ShiftDown());
            InvalidateSelectionArea();
            return;
        }

        _selection.EndDrag(e.Location);
        UpdateView();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        // Matn ustida ikki marta bosish — matnni qayta tahrirlash.
        if (e.Button == MouseButtons.Left && _textBox is null)
        {
            var (index, _) = _editor.HitTest(e.Location, Grip());
            if (index >= 0 && _history.Items[index] is TextAnnotation text)
            {
                // Ikkinchi bosish matnni "ushlab" olgan — tahrir bekor, matn maydoni ochiladi.
                _editor.Deselect();
                BeginTextEdit(index, text);
                return;
            }
        }

        // Asbob tanlanmagan bo'lsa, soha ichida ikki marta bosish — tayyor (nusxa olish).
        if (e.Button == MouseButtons.Left && _editor.Tool == EditorTool.None && _selection.HasSelection && _selection.Selection.Contains(e.Location))
        {
            Accept();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool shift = (keyData & Keys.Shift) != 0;
        bool ctrl = (keyData & Keys.Control) != 0;
        bool alt = (keyData & Keys.Alt) != 0;

        // Matn kiritilayotganda klaviatura TextBox'niki; faqat Esc va Ctrl+Enter matnni tugatadi.
        if (_textBox is not null)
        {
            if (key == Keys.Escape || (key == Keys.Enter && ctrl))
            {
                CommitText();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        int step = ctrl ? 10 : 1;
        switch (key)
        {
            case Keys.Escape:
                // Chizma tanlangan bo'lsa — avval faqat tanlovni olib tashlash (ish tasodifan yo'qolmasin).
                if (_editor.SelectedIndex >= 0)
                {
                    _editor.Deselect();
                    InvalidateSelectionArea();
                }
                else
                {
                    Cancel();
                }

                return true;
            case Keys.Delete:
                if (_editor.DeleteSelected())
                {
                    InvalidateSelectionArea();
                }

                return true;
            case Keys.Enter:
                Accept();
                return true;
            case Keys.C when ctrl:
                Accept();
                return true;
            case Keys.S when ctrl:
                SaveAs();
                return true;
            case Keys.P when ctrl:
                Accept(OverlayAction.Print);
                return true;
            case Keys.Z when ctrl && shift:
            case Keys.Y when ctrl:
                Redo();
                return true;
            case Keys.Z when ctrl:
                Undo();
                return true;
            case Keys.A when ctrl:
                // Ctrl+A — sichqoncha turgan monitorni to'liq tanlash.
                _selection.SetSelection(_layout.CanvasBoundsOf(_layout.MonitorAtCanvas(_mouse)));
                UpdateView();
                return true;
            case Keys.Left or Keys.Right or Keys.Up or Keys.Down:
                int dx = key == Keys.Left ? -step : key == Keys.Right ? step : 0;
                int dy = key == Keys.Up ? -step : key == Keys.Down ? step : 0;
                if (shift)
                {
                    _selection.ResizeBy(dx, dy);
                }
                else
                {
                    _selection.Nudge(dx, dy);
                }

                UpdateView();
                return true;
        }

        // P, L, A, R, M, T — asboblar (soha tanlangan bo'lsa).
        if (!ctrl && !alt && _selection.State == SelectionState.Selected && key is >= Keys.A and <= Keys.Z
            && EditorUi.ToolForKey((char)key) is EditorTool tool)
        {
            SetTool(_editor.Tool == tool ? EditorTool.None : tool);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dimmed.Dispose();
            _toolbars.Dispose();
            _textBox?.Dispose();
            _textFont?.Dispose();
            foreach (var f in _fonts.Values)
            {
                f.Dispose();
            }

            _fonts.Clear();
        }

        base.Dispose(disposing);
    }

    private static Bitmap CreateDimmed(Bitmap screen)
    {
        var dimmed = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(dimmed);
        var all = new Rectangle(Point.Empty, screen.Size);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(screen, all, all, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;
        using var shade = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
        g.FillRectangle(shade, all);
        return dimmed;
    }

    private static bool ShiftDown() => (ModifierKeys & Keys.Shift) != 0;

    private void WireToolbars()
    {
        _toolbars.ToolClicked += (_, tool) => SetTool(_editor.Tool == tool ? EditorTool.None : tool);
        _toolbars.ColorChosen += (_, color) => SetColor(color);
        _toolbars.ThicknessChosen += (_, t) =>
        {
            _editor.Thickness = t;
            RefreshToolbarState();
        };
        _toolbars.MoreColorsClicked += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = _editor.Color, FullOpen = true, AnyColor = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SetColor(dialog.Color);
            }
        };
        _toolbars.UndoClicked += (_, _) => Undo();
        _toolbars.RedoClicked += (_, _) => Redo();
        _toolbars.CopyClicked += (_, _) => Accept();
        _toolbars.SaveClicked += (_, _) => SaveAs();
        _toolbars.PrintClicked += (_, _) => Accept(OverlayAction.Print);
        _toolbars.CloseClicked += (_, _) => Cancel();
        RefreshToolbarState();
    }

    private void SetTool(EditorTool tool)
    {
        _editor.Tool = tool;
        RefreshToolbarState();
        Cursor = CursorAt(_mouse);
        InvalidatePreview();
    }

    private void Undo()
    {
        // Tarix o'rinlarni o'zgartirishi mumkin — tanlov olib tashlanadi.
        _editor.Deselect();
        _history.Undo();
    }

    private void Redo()
    {
        _editor.Deselect();
        _history.Redo();
    }

    private void SetColor(Color color)
    {
        _editor.Color = Color.FromArgb(255, color);
        RefreshToolbarState();
    }

    private void RefreshToolbarState() =>
        _toolbars.UpdateState(_editor.Tool, _editor.Color, _editor.Thickness, _history.CanUndo, _history.CanRedo);

    private void OnHistoryChanged()
    {
        // Chizma bor bo'lsa, tashqariga tasodifiy bosish yangi soha boshlamaydi (chizmalar yo'qolmasin).
        _selection.CanStartNew = _history.Count == 0;
        RefreshToolbarState();
        if (_selection.HasSelection)
        {
            Invalidate(Rectangle.Inflate(_selection.Selection, 2, 2));
        }
    }

    // ---------- Matn ----------

    private void BeginTextEdit(int index, TextAnnotation text)
    {
        _editingTextIndex = index;
        BeginText(new Point(text.Location.X - 2, text.Location.Y - 1), text.Width, text.Color, text.Text.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        InvalidateSelectionArea();
    }

    private void BeginText(Point location) =>
        BeginText(location, EditorController.FontSize(_editor.Thickness, ScaleAt(location)), _editor.Color, string.Empty);

    private void BeginText(Point location, float fontSize, Color color, string initialText)
    {
        _textFontSize = fontSize;
        _textFont?.Dispose();
        _textFont = AnnotationRenderer.CreateTextFont(_textFontSize);

        // Yorqin rang (sariq, oq) oq fonda ko'rinmaydi — fon rangga qarab tanlanadi.
        bool light = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B) > 170;

        var box = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = _textFont,
            ForeColor = color,
            BackColor = light ? Color.FromArgb(50, 50, 50) : Color.White,
            Location = location,
            Text = initialText,
        };
        box.TextChanged += (_, _) => FitTextBox(box);
        _textBox = box;
        FitTextBox(box);
        Controls.Add(box);
        box.BringToFront();
        box.Focus();
        box.SelectionStart = box.TextLength;
    }

    private void FitTextBox(TextBox box)
    {
        if (_textFont is null)
        {
            return;
        }

        string sample = string.IsNullOrEmpty(box.Text) ? "Wg" : box.Text + "W";
        var size = TextRenderer.MeasureText(sample, _textFont, Size.Empty, AnnotationRenderer.TextFlags);
        int pad = (int)Math.Ceiling(_textFontSize / 3);
        var sel = _selection.Selection;
        int maxWidth = Math.Max(40, sel.Right - box.Left);
        box.Size = new Size(Math.Min(maxWidth, size.Width + pad), size.Height + pad);
    }

    private void CommitText()
    {
        var box = _textBox;
        if (box is null || _textFont is null)
        {
            return;
        }

        _textBox = null;
        string text = box.Text;
        var measured = TextRenderer.MeasureText(text.TrimEnd(), _textFont, Size.Empty, AnnotationRenderer.TextFlags);

        // TextBox ichidagi matn ramkadan 1–2 px ichkarida — chizma o'sha joyga tushsin.
        var location = new Point(box.Left + 2, box.Top + 1);
        Controls.Remove(box);
        box.Dispose();

        int editing = _editingTextIndex;
        _editingTextIndex = -1;
        if (editing >= 0 && editing < _history.Count)
        {
            // Mavjud matn: o'rni saqlanadi; bo'shatilsa — o'chiriladi (Ctrl+Z bilan qaytadi).
            _editor.ReplaceText(editing, text, measured);
        }
        else
        {
            _editor.CommitText(location, text, measured, _textFontSize);
        }

        Focus();
        InvalidateSelectionArea();
        UpdateView();
    }

    // ---------- Ko'rinish ----------

    private void SaveAs()
    {
        if (_textBox is not null)
        {
            CommitText();
        }

        if (!_selection.HasSelection || _selection.IsDragging || _editor.IsDrawing || SavePathProvider is null)
        {
            return;
        }

        string? path = SavePathProvider(this);
        if (path is null)
        {
            return; // saqlash oynasida "Bekor qilish" — tahrirlash davom etadi
        }

        SavePath = path;
        Accept(OverlayAction.Save);
    }

    private void Accept(OverlayAction action = OverlayAction.Copy)
    {
        if (_textBox is not null)
        {
            CommitText();
        }

        if (!_selection.HasSelection || _selection.IsDragging || _editor.IsDrawing)
        {
            return;
        }

        Result = _selection.Selection;
        Action = action;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Cancel()
    {
        Result = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private double ScaleAt(Point canvas) => _layout.MonitorAtCanvas(canvas).Scale;

    /// <summary>Sichqoncha turgan monitorning masshtabiga qarab "yopishish" radiusi.</summary>
    private int Grip() => (int)Math.Round(6 * ScaleAt(_mouse));

    private Cursor CursorAt(Point p)
    {
        if (_editor.IsEditing)
        {
            return Cursors.SizeAll;
        }

        if (_selection.State == SelectionState.Selected && _selection.Selection.Contains(p))
        {
            var (index, part) = _editor.HitTest(p, Grip());
            if (index >= 0)
            {
                return part == AnnotationHandle.Body ? Cursors.SizeAll : Cursors.Hand;
            }
        }

        var handle = _selection.IsDragging ? _selection.ActiveHandle : _selection.HitTest(p, Grip());
        if (_editor.Tool != EditorTool.None && handle == SelectionHandle.Inside)
        {
            return _editor.Tool == EditorTool.Text ? Cursors.IBeam : Cursors.Cross;
        }

        return handle switch
        {
            SelectionHandle.TopLeft or SelectionHandle.BottomRight => Cursors.SizeNWSE,
            SelectionHandle.TopRight or SelectionHandle.BottomLeft => Cursors.SizeNESW,
            SelectionHandle.Left or SelectionHandle.Right => Cursors.SizeWE,
            SelectionHandle.Top or SelectionHandle.Bottom => Cursors.SizeNS,
            SelectionHandle.Inside => Cursors.SizeAll,
            _ => _selection.State == SelectionState.Moving ? Cursors.SizeAll : Cursors.Cross,
        };
    }

    /// <summary>Tutqich o'lchami (tanlangan chizma uchun), px.</summary>
    private int HandleSize() => Math.Max(7, (int)Math.Round(9 * ScaleAt(_selection.Selection.Location)));

    private Rectangle EditBounds() => (_editor.EditPreview ?? _editor.Selected)?.Bounds ?? Rectangle.Empty;

    /// <summary>Tahrirlanayotgan chizma atrofini (eski va yangi joyi, tutqichlari bilan) qayta chizish.</summary>
    private void InvalidateEdit()
    {
        var current = EditBounds();
        var dirty = _lastEditBounds.IsEmpty ? current : current.IsEmpty ? _lastEditBounds : Rectangle.Union(_lastEditBounds, current);
        if (!dirty.IsEmpty)
        {
            int pad = HandleSize() + 4;
            Invalidate(Rectangle.Inflate(dirty, pad, pad));
        }

        _lastEditBounds = current;
    }

    /// <summary>Butun sohani qayta chizish (tanlov o'zgarganda — kam uchraydigan hodisa).</summary>
    private void InvalidateSelectionArea()
    {
        if (_selection.HasSelection)
        {
            int pad = HandleSize() + 4;
            Invalidate(Rectangle.Inflate(_selection.Selection, pad, pad));
        }

        _lastEditBounds = Rectangle.Empty;
    }

    /// <summary>
    /// Tanlangan chizmaning tutqichlari: strelka/chiziq uchlarida — doiralar, to'rtburchak burchaklarida — kvadratlar,
    /// qalam izi va matn atrofida — punktir ramka (ular faqat siljitiladi).
    /// </summary>
    private void DrawAnnotationHandles(Graphics g)
    {
        var a = _editor.EditPreview ?? _editor.Selected;
        if (a is null || _editingTextIndex >= 0)
        {
            return;
        }

        int h = HandleSize();
        using var fill = new SolidBrush(Color.White);
        using var border = new Pen(Accent, Math.Max(1.5f, h / 6f));
        var handles = AnnotationGeometry.Handles(a);
        if (handles.Count == 0)
        {
            using var dashed = new Pen(Accent, Math.Max(1f, h / 8f)) { DashStyle = DashStyle.Dash };
            var b = a.Bounds;
            g.DrawRectangle(dashed, b.X, b.Y, b.Width - 1, b.Height - 1);
            return;
        }

        foreach (var (kind, p) in handles)
        {
            var r = new Rectangle(p.X - (h / 2), p.Y - (h / 2), h, h);
            if (kind is AnnotationHandle.Start or AnnotationHandle.End)
            {
                g.FillEllipse(fill, r);
                g.DrawEllipse(border, r);
            }
            else
            {
                g.FillRectangle(fill, r);
                g.DrawRectangle(border, r);
            }
        }
    }

    /// <summary>Chizilayotgan shakl atrofini qayta chizish (butun sohani emas — katta ekranda ham tez).</summary>
    private void InvalidatePreview()
    {
        var current = _editor.Preview?.Bounds ?? Rectangle.Empty;
        var dirty = _lastPreview.IsEmpty ? current : current.IsEmpty ? _lastPreview : Rectangle.Union(_lastPreview, current);
        if (!dirty.IsEmpty)
        {
            Invalidate(Rectangle.Inflate(dirty, 2, 2));
        }

        _lastPreview = current;
    }

    /// <summary>Faqat o'zgargan qismni qayta chizish va panellarni joylashtirish.</summary>
    private void UpdateView()
    {
        var d = ComputeDecorations();
        var dirty = d.HintRect;
        if (_selection.HasSelection)
        {
            int pad = (int)Math.Ceiling(8 * d.Scale) + 2;
            var frame = Rectangle.Inflate(_selection.Selection, pad, pad);
            dirty = dirty.IsEmpty ? frame : Rectangle.Union(dirty, frame);
            dirty = Rectangle.Union(dirty, Rectangle.Inflate(d.LabelRect, 2, 2));
        }

        var invalid = _lastDirty.IsEmpty ? dirty : dirty.IsEmpty ? _lastDirty : Rectangle.Union(_lastDirty, dirty);
        if (!invalid.IsEmpty)
        {
            Invalidate(invalid);
        }

        _lastDirty = dirty;
        PlaceToolbars();
    }

    private void PlaceToolbars()
    {
        var sel = _selection.Selection;
        bool show = _selection.State is SelectionState.Selected or SelectionState.Moving or SelectionState.Resizing
                    && sel.Width > 0 && sel.Height > 0;
        if (!show)
        {
            _toolbars.Tools.Visible = false;
            _toolbars.Actions.Visible = false;
            return;
        }

        // Panellar soha o'ng-pastki burchagi joylashgan monitorga moslanadi.
        var monitor = _layout.MonitorAtCanvas(new Point(sel.Right - 1, sel.Bottom - 1));
        var monitorBounds = _layout.CanvasBoundsOf(monitor);
        _toolbars.SetScale(monitor.Scale);
        int gap = (int)Math.Round(8 * monitor.Scale);

        var tools = _toolbars.Tools;
        var actions = _toolbars.Actions;
        var toolsAt = EditorUi.PlaceToolsBar(sel, tools.PreferredSize, monitorBounds, gap);
        var actionsAt = EditorUi.PlaceActionsBar(sel, actions.PreferredSize, monitorBounds, gap);
        if (tools.Location != toolsAt)
        {
            tools.Location = toolsAt;
        }

        if (actions.Location != actionsAt)
        {
            actions.Location = actionsAt;
        }

        tools.Visible = true;
        actions.Visible = true;
    }

    private Decorations ComputeDecorations()
    {
        var sel = _selection.Selection;
        bool has = _selection.HasSelection;
        var anchorMonitor = _layout.MonitorAtCanvas(has ? sel.Location : _mouse);
        double scale = anchorMonitor.Scale;

        var labelFont = FontFor(12 * scale);
        string labelText = has ? OverlayLayout.SizeText(sel.Size) : string.Empty;
        var labelRect = Rectangle.Empty;
        if (has)
        {
            var size = Padded(TextRenderer.MeasureText(labelText, labelFont), scale);
            var at = OverlayLayout.PlaceSizeLabel(sel, size, _layout.CanvasBoundsOf(anchorMonitor), (int)Math.Round(6 * scale));
            labelRect = new Rectangle(at, size);
        }

        // Maslahat — sichqoncha turgan monitorda; sudrash/chizish paytida emas, chizma boshlangach — ko'rsatilmaydi.
        var hintMonitor = _layout.MonitorAtCanvas(_mouse);
        double hintScale = hintMonitor.Scale;
        var hintFont = FontFor(15 * hintScale);
        string hintText = _selection.State == SelectionState.Selected ? Strings.Overlay_HintSelected : Strings.Overlay_Hint;
        var hintRect = Rectangle.Empty;
        if (!_selection.IsDragging && !_editor.IsDrawing && _history.Count == 0)
        {
            var size = Padded(TextRenderer.MeasureText(hintText, hintFont), hintScale);
            var at = OverlayLayout.PlaceHint(size, _layout.CanvasBoundsOf(hintMonitor), (int)Math.Round(24 * hintScale));
            hintRect = new Rectangle(at, size);
            if (has && hintRect.IntersectsWith(Rectangle.Inflate(sel, 4, 4)))
            {
                hintRect = Rectangle.Empty;
            }
        }

        return new Decorations(scale, labelRect, labelText, labelFont, hintRect, hintText, hintFont);
    }

    private static Size Padded(Size text, double scale) =>
        new(text.Width + (int)Math.Round(12 * scale), text.Height + (int)Math.Round(6 * scale));

    private Font FontFor(double pixels)
    {
        int key = (int)Math.Round(pixels);
        if (!_fonts.TryGetValue(key, out var font))
        {
            font = new Font("Segoe UI", key, FontStyle.Regular, GraphicsUnit.Pixel);
            _fonts[key] = font;
        }

        return font;
    }

    private void DrawFrame(Graphics g, Rectangle sel, double scale)
    {
        int width = Math.Max(1, (int)Math.Round(scale));
        using var pen = new Pen(Accent, width) { Alignment = PenAlignment.Inset };
        g.DrawRectangle(pen, sel.X, sel.Y, Math.Max(1, sel.Width), Math.Max(1, sel.Height));

        if (_selection.State == SelectionState.Creating)
        {
            return;
        }

        // 8 ta tutqich: burchaklar va chetlarning o'rtasi.
        int h = Math.Max(5, (int)Math.Round(7 * scale));
        int midX = sel.Left + (sel.Width / 2);
        int midY = sel.Top + (sel.Height / 2);
        Point[] points =
        {
            new(sel.Left, sel.Top), new(midX, sel.Top), new(sel.Right, sel.Top), new(sel.Right, midY),
            new(sel.Right, sel.Bottom), new(midX, sel.Bottom), new(sel.Left, sel.Bottom), new(sel.Left, midY),
        };
        using var fill = new SolidBrush(Color.White);
        using var border = new Pen(Accent, Math.Max(1, (int)Math.Round(scale)));
        foreach (var p in points)
        {
            var r = new Rectangle(p.X - (h / 2), p.Y - (h / 2), h, h);
            g.FillRectangle(fill, r);
            g.DrawRectangle(border, r);
        }
    }

    private static void DrawLabel(Graphics g, Rectangle rect, string text, Font font)
    {
        using var back = new SolidBrush(LabelBack);
        g.FillRectangle(back, rect);
        TextRenderer.DrawText(g, text, font, rect, Color.White, LabelBack,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    private readonly record struct Decorations(
        double Scale,
        Rectangle LabelRect,
        string LabelText,
        Font LabelFont,
        Rectangle HintRect,
        string HintText,
        Font HintFont);
}
