using System.Drawing;

namespace SkrinshotUZ.Core.Capture;

/// <summary>Bitta monitor. Barcha koordinatalar — JISMONIY piksellarda (Per-Monitor DPI v2 jarayon ko'radigandek).</summary>
/// <param name="Bounds">Monitor chegarasi ekran koordinatalarida (asosiy monitorning chap-yuqori burchagi — 0,0).</param>
/// <param name="Dpi">Monitor DPI qiymati: 96 = 100%, 120 = 125%, 144 = 150%, 192 = 200%.</param>
/// <param name="IsPrimary">Asosiy monitormi.</param>
public sealed record MonitorInfo(Rectangle Bounds, double Dpi, bool IsPrimary)
{
    /// <summary>Masshtab koeffitsienti (1.0 = 100%).</summary>
    public double Scale => Dpi / 96.0;
}

/// <summary>
/// Barcha monitorlar joylashuvi. "Kanvas" — barcha monitorlarni qamrab oluvchi umumiy rasm
/// (skrinshot bitmap'i): uning (0,0) nuqtasi virtual ekranning chap-yuqori burchagi.
/// Asosiy monitordan chapda yoki yuqorida turgan monitor manfiy ekran koordinatalariga ega bo'ladi —
/// kanvasda esa hamma narsa musbat.
/// </summary>
public sealed class ScreenLayout
{
    /// <summary>Yangi joylashuv.</summary>
    /// <param name="monitors">Monitorlar (kamida bitta).</param>
    public ScreenLayout(IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("Kamida bitta monitor bo'lishi kerak.", nameof(monitors));
        }

        foreach (var m in monitors)
        {
            if (m.Bounds.Width <= 0 || m.Bounds.Height <= 0)
            {
                throw new ArgumentException($"Monitor o'lchami noto'g'ri: {m.Bounds}.", nameof(monitors));
            }

            if (double.IsNaN(m.Dpi) || m.Dpi <= 0)
            {
                throw new ArgumentException($"Monitor DPI noto'g'ri: {m.Dpi}.", nameof(monitors));
            }
        }

        Monitors = monitors;
        var union = monitors[0].Bounds;
        for (int i = 1; i < monitors.Count; i++)
        {
            union = Rectangle.Union(union, monitors[i].Bounds);
        }

        VirtualBounds = union;
    }

    /// <summary>Monitorlar.</summary>
    public IReadOnlyList<MonitorInfo> Monitors { get; }

    /// <summary>Barcha monitorlarni qamrab oluvchi to'rtburchak (ekran koordinatalarida).</summary>
    public Rectangle VirtualBounds { get; }

    /// <summary>Kanvas (skrinshot rasmi) o'lchami.</summary>
    public Size CanvasSize => VirtualBounds.Size;

    /// <summary>Ekran nuqtasini kanvas nuqtasiga o'giradi.</summary>
    /// <param name="screen">Ekran koordinatasi.</param>
    /// <returns>Kanvas koordinatasi.</returns>
    public Point ToCanvas(Point screen) => new(screen.X - VirtualBounds.X, screen.Y - VirtualBounds.Y);

    /// <summary>Ekran to'rtburchagini kanvasga o'giradi.</summary>
    /// <param name="screen">Ekran koordinatasi.</param>
    /// <returns>Kanvas koordinatasi.</returns>
    public Rectangle ToCanvas(Rectangle screen) => new(ToCanvas(screen.Location), screen.Size);

    /// <summary>Kanvas to'rtburchagini ekran koordinatalariga o'giradi.</summary>
    /// <param name="canvas">Kanvas koordinatasi.</param>
    /// <returns>Ekran koordinatasi.</returns>
    public Rectangle ToScreen(Rectangle canvas) =>
        new(canvas.X + VirtualBounds.X, canvas.Y + VirtualBounds.Y, canvas.Width, canvas.Height);

    /// <summary>
    /// Nuqta joylashgan monitor. Monitorlar orasidagi "bo'shliq"dagi nuqta uchun (L-shakl joylashuvda bo'ladi) —
    /// eng yaqin monitor.
    /// </summary>
    /// <param name="screen">Ekran koordinatasi.</param>
    /// <returns>Monitor.</returns>
    public MonitorInfo MonitorAt(Point screen)
    {
        MonitorInfo? best = null;
        long bestDistance = long.MaxValue;
        foreach (var m in Monitors)
        {
            if (m.Bounds.Contains(screen))
            {
                return m;
            }

            long d = DistanceSquared(m.Bounds, screen);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = m;
            }
        }

        return best!;
    }

    /// <summary>Kanvas nuqtasi joylashgan monitor.</summary>
    /// <param name="canvas">Kanvas koordinatasi.</param>
    /// <returns>Monitor.</returns>
    public MonitorInfo MonitorAtCanvas(Point canvas) =>
        MonitorAt(new Point(canvas.X + VirtualBounds.X, canvas.Y + VirtualBounds.Y));

    /// <summary>Monitor chegarasi kanvas koordinatalarida.</summary>
    /// <param name="monitor">Monitor.</param>
    /// <returns>Kanvasdagi to'rtburchak.</returns>
    public Rectangle CanvasBoundsOf(MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return ToCanvas(monitor.Bounds);
    }

    private static long DistanceSquared(Rectangle r, Point p)
    {
        long dx = p.X < r.Left ? r.Left - p.X : p.X >= r.Right ? p.X - (r.Right - 1) : 0;
        long dy = p.Y < r.Top ? r.Top - p.Y : p.Y >= r.Bottom ? p.Y - (r.Bottom - 1) : 0;
        return (dx * dx) + (dy * dy);
    }
}
