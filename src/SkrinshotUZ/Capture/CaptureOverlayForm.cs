using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SkrinshotUZ.Core.Capture;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Native;

namespace SkrinshotUZ.Capture;

/// <summary>
/// Soha tanlash oynasi: barcha monitorlarni qoplaydi, ekranning "muzlatilgan" rasmini xiralashtirib ko'rsatadi,
/// tanlangan soha esa asl yorug'likda. Barcha koordinatalar — jismoniy piksellar (oyna klient sohasi = kanvas).
/// Mantiq <see cref="SelectionController"/> da (unit-testlangan); bu sinf faqat chizadi va kiritishni uzatadi.
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
    private readonly Dictionary<int, Font> _fonts = new();
    private Rectangle _lastDirty;
    private Point _mouse;

    /// <summary>Yangi oyna.</summary>
    /// <param name="layout">Monitorlar.</param>
    /// <param name="screen">Ekran rasmi (oyna egalik qilmaydi — chaqiruvchi dispose qiladi).</param>
    /// <param name="presetScreenRect">Oldindan belgilangan soha, ekran koordinatalarida (Alt+PrintScreen — faol oyna).</param>
    public CaptureOverlayForm(ScreenLayout layout, Bitmap screen, Rectangle? presetScreenRect)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        _selection = new SelectionController(layout.CanvasSize);
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
    }

    /// <summary>Tanlangan soha (kanvas koordinatalarida) — faqat <see cref="DialogResult.OK"/> bo'lsa.</summary>
    public Rectangle? Result { get; private set; }

    /// <summary>Faqat tutun testi uchun: sohani o'rnatib, darhol qabul qiladi.</summary>
    internal void AcceptForTest(Rectangle canvasRect)
    {
        _selection.SetSelection(canvasRect);
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
        if (_selection.HasSelection)
        {
            var bright = Rectangle.Intersect(sel, clip);
            if (bright.Width > 0 && bright.Height > 0)
            {
                g.DrawImage(_screen, bright, bright, GraphicsUnit.Pixel);
            }
        }

        g.CompositingMode = CompositingMode.SourceOver;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        var d = ComputeDecorations();
        if (_selection.HasSelection && sel.Width > 0 && sel.Height > 0)
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
        if (e.Button == MouseButtons.Left)
        {
            _selection.BeginDrag(e.Location, Grip());
        }
        else if (e.Button == MouseButtons.Right)
        {
            // O'ng tugma: soha bo'lsa — uni olib tashlash, bo'lmasa — chiqish.
            if (_selection.HasSelection && !_selection.IsDragging)
            {
                _selection.Clear();
            }
            else
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
        if (_selection.IsDragging)
        {
            _selection.Drag(e.Location);
        }

        Cursor = CursorFor(_selection.IsDragging ? _selection.ActiveHandle : _selection.HitTest(e.Location, Grip()), _selection.State);
        UpdateView();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            _selection.EndDrag(e.Location);
            UpdateView();
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && _selection.HasSelection && _selection.Selection.Contains(e.Location))
        {
            Accept();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool shift = (keyData & Keys.Shift) != 0;
        bool ctrl = (keyData & Keys.Control) != 0;
        int step = ctrl ? 10 : 1;

        switch (key)
        {
            case Keys.Escape:
                Cancel();
                return true;
            case Keys.Enter:
                Accept();
                return true;
            case Keys.C when ctrl:
                Accept();
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

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dimmed.Dispose();
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

    private static Cursor CursorFor(SelectionHandle handle, SelectionState state) => handle switch
    {
        SelectionHandle.TopLeft or SelectionHandle.BottomRight => Cursors.SizeNWSE,
        SelectionHandle.TopRight or SelectionHandle.BottomLeft => Cursors.SizeNESW,
        SelectionHandle.Left or SelectionHandle.Right => Cursors.SizeWE,
        SelectionHandle.Top or SelectionHandle.Bottom => Cursors.SizeNS,
        SelectionHandle.Inside => Cursors.SizeAll,
        _ => state == SelectionState.Moving ? Cursors.SizeAll : Cursors.Cross,
    };

    private void Accept()
    {
        if (!_selection.HasSelection || _selection.IsDragging)
        {
            return;
        }

        Result = _selection.Selection;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Cancel()
    {
        Result = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>Sichqoncha turgan monitorning masshtabiga qarab "yopishish" radiusi.</summary>
    private int Grip() => (int)Math.Round(6 * _layout.MonitorAtCanvas(_mouse).Scale);

    /// <summary>Faqat o'zgargan qismni qayta chizish (4K ekranda ham silliq bo'lishi uchun).</summary>
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

        // Maslahat — sichqoncha turgan monitorda, faqat sudrash paytida emas va soha bilan ustma-ust tushmasa.
        var hintMonitor = _layout.MonitorAtCanvas(_mouse);
        double hintScale = hintMonitor.Scale;
        var hintFont = FontFor(15 * hintScale);
        string hintText = _selection.State == SelectionState.Selected ? Strings.Overlay_HintSelected : Strings.Overlay_Hint;
        var hintRect = Rectangle.Empty;
        if (!_selection.IsDragging)
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
