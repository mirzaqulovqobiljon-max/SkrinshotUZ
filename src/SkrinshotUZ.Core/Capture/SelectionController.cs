using System.Drawing;

namespace SkrinshotUZ.Core.Capture;

/// <summary>Sichqoncha ramkaning qaysi qismida.</summary>
public enum SelectionHandle
{
    /// <summary>Ramkadan tashqarida (yangi soha chiziladi).</summary>
    None,

    /// <summary>Ramka ichida (surish).</summary>
    Inside,

    /// <summary>Chap-yuqori burchak.</summary>
    TopLeft,

    /// <summary>Yuqori chet.</summary>
    Top,

    /// <summary>O'ng-yuqori burchak.</summary>
    TopRight,

    /// <summary>O'ng chet.</summary>
    Right,

    /// <summary>O'ng-pastki burchak.</summary>
    BottomRight,

    /// <summary>Pastki chet.</summary>
    Bottom,

    /// <summary>Chap-pastki burchak.</summary>
    BottomLeft,

    /// <summary>Chap chet.</summary>
    Left,
}

/// <summary>Tanlash holati.</summary>
public enum SelectionState
{
    /// <summary>Hali hech narsa tanlanmagan.</summary>
    Empty,

    /// <summary>Yangi soha chizilmoqda (tugma bosib turilibdi).</summary>
    Creating,

    /// <summary>Soha tanlangan.</summary>
    Selected,

    /// <summary>Soha surilmoqda.</summary>
    Moving,

    /// <summary>Soha burchak yoki chetdan o'zgartirilmoqda.</summary>
    Resizing,
}

/// <summary>
/// Soha tanlash holat mashinasi (Lightshot kabi). WinForms'ga bog'liq emas — to'liq unit-test qilinadi.
/// Barcha koordinatalar kanvas (skrinshot rasmi) pikselarida; soha doim kanvas ichida qoladi.
/// To'rtburchak "yarim ochiq": sichqoncha (10,10) dan (110,60) gacha sudralsa — 100×50 px.
/// </summary>
public sealed class SelectionController
{
    /// <summary>Bundan kichik sudrash — oddiy bosish hisoblanadi (soha yaratilmaydi), px.</summary>
    public const int MinSize = 2;

    private Rectangle _selection;
    private Point _anchor;
    private Point _grab;
    private Rectangle _original;

    /// <summary>Yangi holat mashinasi.</summary>
    /// <param name="canvasSize">Kanvas o'lchami.</param>
    public SelectionController(Size canvasSize)
    {
        if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(canvasSize), canvasSize, "Kanvas o'lchami musbat bo'lishi kerak.");
        }

        Bounds = new Rectangle(Point.Empty, canvasSize);
    }

    /// <summary>Kanvas chegarasi.</summary>
    public Rectangle Bounds { get; }

    /// <summary>Joriy holat.</summary>
    public SelectionState State { get; private set; } = SelectionState.Empty;

    /// <summary>Hozir qaysi tutqich bilan ishlanmoqda (Resizing/Moving paytida).</summary>
    public SelectionHandle ActiveHandle { get; private set; }

    /// <summary>Soha bormi (Creating paytida ham — chizilayotgan soha).</summary>
    public bool HasSelection => State != SelectionState.Empty;

    /// <summary>Joriy soha (holat <see cref="SelectionState.Empty"/> bo'lsa — <see cref="Rectangle.Empty"/>).</summary>
    public Rectangle Selection => HasSelection ? _selection : Rectangle.Empty;

    /// <summary>
    /// Soha tashqarisiga bosilganda yangi soha boshlansinmi. Soha ustida chizma bo'lsa — <c>false</c>
    /// (tasodifiy bosish chizmalarni "yo'qotib" qo'ymasligi uchun); surish va o'zgartirish ishlayveradi.
    /// </summary>
    public bool CanStartNew { get; set; } = true;

    /// <summary>Sichqoncha tugmasi bosilganmi (sudrash davom etmoqda).</summary>
    public bool IsDragging => State is SelectionState.Creating or SelectionState.Moving or SelectionState.Resizing;

    /// <summary>Nuqta ramkaning qaysi qismida.</summary>
    /// <param name="p">Kanvas nuqtasi.</param>
    /// <param name="grip">Tutqich "yopishish" radiusi, px (DPI'ga qarab kattaroq).</param>
    /// <returns>Qism.</returns>
    public SelectionHandle HitTest(Point p, int grip)
    {
        if (State is not (SelectionState.Selected or SelectionState.Moving or SelectionState.Resizing))
        {
            return SelectionHandle.None;
        }

        var r = _selection;
        int dl = Math.Abs(p.X - r.Left);
        int dr = Math.Abs(p.X - r.Right);
        int dt = Math.Abs(p.Y - r.Top);
        int db = Math.Abs(p.Y - r.Bottom);

        // Kichik sohada qarama-qarshi chetlar ikkalasi ham yaqin bo'ladi — eng yaqini tanlanadi.
        bool nearX = Math.Min(dl, dr) <= grip;
        bool nearY = Math.Min(dt, db) <= grip;
        bool left = dl < dr;
        bool top = dt < db;
        bool withinX = p.X >= r.Left - grip && p.X <= r.Right + grip;
        bool withinY = p.Y >= r.Top - grip && p.Y <= r.Bottom + grip;

        // Burchaklar chetlardan ustun.
        if (nearX && nearY)
        {
            return (top, left) switch
            {
                (true, true) => SelectionHandle.TopLeft,
                (true, false) => SelectionHandle.TopRight,
                (false, false) => SelectionHandle.BottomRight,
                _ => SelectionHandle.BottomLeft,
            };
        }

        if (nearY && withinX)
        {
            return top ? SelectionHandle.Top : SelectionHandle.Bottom;
        }

        if (nearX && withinY)
        {
            return left ? SelectionHandle.Left : SelectionHandle.Right;
        }

        return r.Contains(p) ? SelectionHandle.Inside : SelectionHandle.None;
    }

    /// <summary>Sichqoncha tugmasi bosildi.</summary>
    /// <param name="p">Kanvas nuqtasi.</param>
    /// <param name="grip">Tutqich radiusi, px.</param>
    public void BeginDrag(Point p, int grip)
    {
        var handle = HitTest(p, grip);
        if (handle == SelectionHandle.Inside)
        {
            State = SelectionState.Moving;
            ActiveHandle = SelectionHandle.Inside;
            _grab = p;
            _original = _selection;
        }
        else if (handle != SelectionHandle.None)
        {
            State = SelectionState.Resizing;
            ActiveHandle = handle;
            _original = _selection;
        }
        else if (CanStartNew || State == SelectionState.Empty)
        {
            State = SelectionState.Creating;
            ActiveHandle = SelectionHandle.None;
            _anchor = Clamp(p);
            _selection = new Rectangle(_anchor, Size.Empty);
        }
    }

    /// <summary>Sichqoncha tugma bosilgan holda harakatlandi.</summary>
    /// <param name="p">Kanvas nuqtasi.</param>
    public void Drag(Point p)
    {
        switch (State)
        {
            case SelectionState.Creating:
                _selection = FromCorners(_anchor, Clamp(p));
                break;
            case SelectionState.Moving:
                _selection = ClampMove(new Rectangle(
                    _original.X + (p.X - _grab.X),
                    _original.Y + (p.Y - _grab.Y),
                    _original.Width,
                    _original.Height));
                break;
            case SelectionState.Resizing:
                _selection = Resize(_original, ActiveHandle, Clamp(p));
                break;
        }
    }

    /// <summary>Sichqoncha tugmasi qo'yib yuborildi.</summary>
    /// <param name="p">Kanvas nuqtasi.</param>
    public void EndDrag(Point p)
    {
        if (!IsDragging)
        {
            return;
        }

        Drag(p);
        bool tooSmall = _selection.Width < MinSize || _selection.Height < MinSize;
        if (State == SelectionState.Creating && tooSmall)
        {
            // Oddiy bosish — soha yaratilmaydi.
            Clear();
            return;
        }

        if (State == SelectionState.Resizing && tooSmall)
        {
            // O'zgartirish natijasida soha "yo'qolib" qolmasin — avvalgisi qaytariladi.
            _selection = _original;
        }

        State = SelectionState.Selected;
        ActiveHandle = SelectionHandle.None;
    }

    /// <summary>Sohani o'rnatadi (masalan, Alt+PrintScreen — faol oyna). Kanvasdan chiqqan qismi kesiladi.</summary>
    /// <param name="rect">Kanvas to'rtburchagi.</param>
    /// <returns>Kanvas bilan kesishma bo'sh bo'lmasa <c>true</c>.</returns>
    public bool SetSelection(Rectangle rect)
    {
        var r = Rectangle.Intersect(rect, Bounds);
        if (r.Width < MinSize || r.Height < MinSize)
        {
            return false;
        }

        _selection = r;
        State = SelectionState.Selected;
        ActiveHandle = SelectionHandle.None;
        return true;
    }

    /// <summary>Sohani olib tashlaydi.</summary>
    public void Clear()
    {
        _selection = Rectangle.Empty;
        State = SelectionState.Empty;
        ActiveHandle = SelectionHandle.None;
    }

    /// <summary>Klaviatura strelkalari: sohani suradi (kanvasdan chiqmaydi).</summary>
    /// <param name="dx">X bo'yicha, px.</param>
    /// <param name="dy">Y bo'yicha, px.</param>
    public void Nudge(int dx, int dy)
    {
        if (State == SelectionState.Selected)
        {
            _selection = ClampMove(new Rectangle(_selection.X + dx, _selection.Y + dy, _selection.Width, _selection.Height));
        }
    }

    /// <summary>Shift+strelkalar: o'ng-pastki burchakni suradi (o'lchamni o'zgartiradi).</summary>
    /// <param name="dw">Kenglik o'zgarishi, px.</param>
    /// <param name="dh">Balandlik o'zgarishi, px.</param>
    public void ResizeBy(int dw, int dh)
    {
        if (State != SelectionState.Selected)
        {
            return;
        }

        int right = Math.Clamp(_selection.Right + dw, _selection.Left + MinSize, Bounds.Right);
        int bottom = Math.Clamp(_selection.Bottom + dh, _selection.Top + MinSize, Bounds.Bottom);
        _selection = Rectangle.FromLTRB(_selection.Left, _selection.Top, right, bottom);
    }

    private static Rectangle FromCorners(Point a, Point b) =>
        Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    private static Rectangle Resize(Rectangle r, SelectionHandle handle, Point p)
    {
        int left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
        switch (handle)
        {
            case SelectionHandle.TopLeft: left = p.X; top = p.Y; break;
            case SelectionHandle.Top: top = p.Y; break;
            case SelectionHandle.TopRight: right = p.X; top = p.Y; break;
            case SelectionHandle.Right: right = p.X; break;
            case SelectionHandle.BottomRight: right = p.X; bottom = p.Y; break;
            case SelectionHandle.Bottom: bottom = p.Y; break;
            case SelectionHandle.BottomLeft: left = p.X; bottom = p.Y; break;
            case SelectionHandle.Left: left = p.X; break;
        }

        // Qarama-qarshi chetdan o'tib ketsa — soha "ag'dariladi" (normallashtiriladi).
        return FromCorners(new Point(left, top), new Point(right, bottom));
    }

    /// <summary>Nuqta kanvasning [0, W] × [0, H] oralig'ida (o'ng/pastki chet — oxirgi piksel ham tanlanishi uchun).</summary>
    private Point Clamp(Point p) =>
        new(Math.Clamp(p.X, Bounds.Left, Bounds.Right), Math.Clamp(p.Y, Bounds.Top, Bounds.Bottom));

    private Rectangle ClampMove(Rectangle r) =>
        new(Math.Clamp(r.X, Bounds.Left, Bounds.Right - r.Width), Math.Clamp(r.Y, Bounds.Top, Bounds.Bottom - r.Height), r.Width, r.Height);
}
