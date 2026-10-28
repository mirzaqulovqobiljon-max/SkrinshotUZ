using SkrinshotUZ.Core.Editing;
using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Editing;

/// <summary>
/// Soha yonidagi ikki panel: tik — asboblar, gorizontal — amallar. Katta tugmalar, har birida ikonka va o'zbekcha tooltip.
/// Panellar overlay'ning ichki boshqaruvlari; ular fokusni olmaydi (klaviatura overlay'da qoladi).
/// </summary>
internal sealed class EditorToolbars : IDisposable
{
    private const int BaseButton = 32;   // 100% da tugma o'lchami, px
    private const int BaseIcon = 18;

    private readonly Dictionary<EditorTool, ToolStripButton> _toolButtons = new();
    private readonly ToolStripDropDownButton _colorButton;
    private readonly ToolStripDropDownButton _thicknessButton;
    private readonly ToolStripButton _undo;
    private readonly ToolStripButton _redo;
    private readonly ToolStripButton _copy;
    private readonly ToolStripButton _save;
    private readonly ToolStripButton _print;
    private readonly ToolStripButton _close;
    private readonly List<ToolStripMenuItem> _colorItems = new();
    private readonly List<ToolStripMenuItem> _thicknessItems = new();
    private readonly Font _font;
    private double _scale = -1;
    private Color _color = EditorController.DefaultColor;
    private Thickness _thickness = Thickness.Medium;

    public EditorToolbars()
    {
        _font = new Font("Segoe UI", 10.5f);
        Tools = CreateStrip(vertical: true);
        Actions = CreateStrip(vertical: false);

        foreach (var tool in EditorUi.Tools)
        {
            var button = new ToolStripButton { ToolTipText = EditorUi.ToolName(tool), DisplayStyle = ToolStripItemDisplayStyle.Image, AutoSize = false };
            var t = tool;
            button.Click += (_, _) => ToolClicked?.Invoke(this, t);
            _toolButtons[tool] = button;
            Tools.Items.Add(button);
        }

        Tools.Items.Add(new ToolStripSeparator());

        _colorButton = new ToolStripDropDownButton { ToolTipText = Strings.Toolbar_Color, DisplayStyle = ToolStripItemDisplayStyle.Image, AutoSize = false, ShowDropDownArrow = false };
        foreach (var entry in EditorUi.Palette)
        {
            var item = new ToolStripMenuItem(entry.Name) { Tag = entry.Color, Font = _font };
            item.Click += (_, _) => ColorChosen?.Invoke(this, (Color)item.Tag!);
            _colorItems.Add(item);
            _colorButton.DropDownItems.Add(item);
        }

        _colorButton.DropDownItems.Add(new ToolStripSeparator());
        var more = new ToolStripMenuItem(Strings.Toolbar_MoreColors) { Font = _font };
        more.Click += (_, _) => MoreColorsClicked?.Invoke(this, EventArgs.Empty);
        _colorButton.DropDownItems.Add(more);
        Tools.Items.Add(_colorButton);

        _thicknessButton = new ToolStripDropDownButton { ToolTipText = Strings.Toolbar_Thickness, DisplayStyle = ToolStripItemDisplayStyle.Image, AutoSize = false, ShowDropDownArrow = false };
        foreach (var level in Enum.GetValues<Thickness>())
        {
            var item = new ToolStripMenuItem(EditorUi.ThicknessName(level)) { Tag = level, Font = _font, ImageScaling = ToolStripItemImageScaling.None };
            item.Click += (_, _) => ThicknessChosen?.Invoke(this, (Thickness)item.Tag!);
            _thicknessItems.Add(item);
            _thicknessButton.DropDownItems.Add(item);
        }

        Tools.Items.Add(_thicknessButton);
        Tools.Items.Add(new ToolStripSeparator());

        _undo = ActionButton(Strings.Toolbar_Undo, () => UndoClicked?.Invoke(this, EventArgs.Empty));
        _redo = ActionButton(Strings.Toolbar_Redo, () => RedoClicked?.Invoke(this, EventArgs.Empty));
        Tools.Items.Add(_undo);
        Tools.Items.Add(_redo);

        _copy = ActionButton(Strings.Action_Copy, () => CopyClicked?.Invoke(this, EventArgs.Empty));
        _save = ActionButton(Strings.Action_Save, () => SaveClicked?.Invoke(this, EventArgs.Empty));
        _print = ActionButton(Strings.Action_Print, () => PrintClicked?.Invoke(this, EventArgs.Empty));
        _close = ActionButton(Strings.Action_Close, () => CloseClicked?.Invoke(this, EventArgs.Empty));
        Actions.Items.AddRange(new ToolStripItem[] { _copy, _save, _print, new ToolStripSeparator(), _close });
    }

    public event EventHandler<EditorTool>? ToolClicked;

    public event EventHandler<Color>? ColorChosen;

    public event EventHandler? MoreColorsClicked;

    public event EventHandler<Thickness>? ThicknessChosen;

    public event EventHandler? UndoClicked;

    public event EventHandler? RedoClicked;

    public event EventHandler? CopyClicked;

    public event EventHandler? SaveClicked;

    public event EventHandler? PrintClicked;

    public event EventHandler? CloseClicked;

    /// <summary>Tik asboblar paneli.</summary>
    public ToolStrip Tools { get; }

    /// <summary>Gorizontal amallar paneli.</summary>
    public ToolStrip Actions { get; }

    /// <summary>
    /// Monitor masshtabiga moslash: ikonkalar shu o'lchamda qayta chiziladi (xira bo'lmaydi).
    /// Faqat masshtab o'zgarganda ishlaydi.
    /// </summary>
    public void SetScale(double scale)
    {
        if (Math.Abs(scale - _scale) < 0.001)
        {
            return;
        }

        _scale = scale;
        int button = (int)Math.Round(BaseButton * scale);
        int icon = (int)Math.Round(BaseIcon * scale);
        var size = new Size(button, button);

        foreach (var (tool, b) in _toolButtons)
        {
            ReplaceImage(b, IconPainter.Draw(IconPainter.ForTool(tool), icon));
            b.Size = size;
        }

        ReplaceImage(_undo, IconPainter.Draw(ToolbarIcon.Undo, icon));
        ReplaceImage(_redo, IconPainter.Draw(ToolbarIcon.Redo, icon));
        ReplaceImage(_copy, IconPainter.Draw(ToolbarIcon.Copy, icon));
        ReplaceImage(_save, IconPainter.Draw(ToolbarIcon.Save, icon));
        ReplaceImage(_print, IconPainter.Draw(ToolbarIcon.Print, icon));
        ReplaceImage(_close, IconPainter.Draw(ToolbarIcon.Close, icon));
        foreach (var b in new ToolStripItem[] { _undo, _redo, _copy, _save, _print, _close, _colorButton, _thicknessButton })
        {
            b.Size = size;
        }

        foreach (var item in _colorItems)
        {
            ReplaceImage(item, IconPainter.Swatch((Color)item.Tag!, icon));
        }

        foreach (var item in _thicknessItems)
        {
            ReplaceImage(item, IconPainter.ThicknessSample(EditorController.StrokeWidth((Thickness)item.Tag!, scale), icon * 2, icon));
        }

        foreach (var strip in new[] { Tools, Actions })
        {
            strip.ImageScalingSize = new Size(icon, icon);
            strip.Padding = new Padding((int)Math.Round(2 * scale));
        }

        UpdateSwatches();
    }

    /// <summary>Holatni ko'rsatish: tanlangan asbob, rang, qalinlik, Ctrl+Z/Ctrl+Y mavjudligi.</summary>
    public void UpdateState(EditorTool tool, Color color, Thickness thickness, bool canUndo, bool canRedo)
    {
        foreach (var (t, b) in _toolButtons)
        {
            b.Checked = t == tool;
        }

        _undo.Enabled = canUndo;
        _redo.Enabled = canRedo;
        if (color != _color || thickness != _thickness)
        {
            _color = color;
            _thickness = thickness;
            UpdateSwatches();
        }

        foreach (var item in _thicknessItems)
        {
            item.Checked = (Thickness)item.Tag! == thickness;
        }

        foreach (var item in _colorItems)
        {
            item.Checked = ((Color)item.Tag!).ToArgb() == color.ToArgb();
        }
    }

    public void Dispose()
    {
        foreach (var item in AllItems())
        {
            item.Image?.Dispose();
        }

        Tools.Dispose();
        Actions.Dispose();
        _font.Dispose();
    }

    private static void ReplaceImage(ToolStripItem item, Image image)
    {
        var old = item.Image;
        item.Image = image;
        old?.Dispose();
    }

    private IEnumerable<ToolStripItem> AllItems() =>
        Tools.Items.Cast<ToolStripItem>()
            .Concat(Actions.Items.Cast<ToolStripItem>())
            .Concat(_colorItems)
            .Concat(_thicknessItems);

    private void UpdateSwatches()
    {
        if (_scale <= 0)
        {
            return;
        }

        int icon = (int)Math.Round(BaseIcon * _scale);
        ReplaceImage(_colorButton, IconPainter.Swatch(_color, icon));
        ReplaceImage(_thicknessButton, IconPainter.ThicknessSample(EditorController.StrokeWidth(_thickness, _scale), icon, icon));
    }

    private ToolStripButton ActionButton(string tooltip, Action onClick)
    {
        var b = new ToolStripButton { ToolTipText = tooltip, DisplayStyle = ToolStripItemDisplayStyle.Image, AutoSize = false };
        b.Click += (_, _) => onClick();
        return b;
    }

    private ToolStrip CreateStrip(bool vertical) => new()
    {
        LayoutStyle = vertical ? ToolStripLayoutStyle.VerticalStackWithOverflow : ToolStripLayoutStyle.HorizontalStackWithOverflow,
        GripStyle = ToolStripGripStyle.Hidden,
        Dock = DockStyle.None,
        AutoSize = true,
        CanOverflow = false,
        BackColor = Color.White,
        RenderMode = ToolStripRenderMode.System,
        ShowItemToolTips = true,
        Visible = false,
        Font = _font,

        // Aniq belgilanadi: aks holda ToolStrip ota oynaning kursorini meros oladi va overlay'ning
        // "+" yoki "↔" kursori tugmalar ustida ham qolib ketadi.
        Cursor = Cursors.Default,
    };
}
