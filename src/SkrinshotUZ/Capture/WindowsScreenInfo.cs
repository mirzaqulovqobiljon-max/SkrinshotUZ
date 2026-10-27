using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Capture;
using SkrinshotUZ.Native;

namespace SkrinshotUZ.Capture;

/// <summary>Windows'dan monitorlar, ularning DPI'si va faol oyna haqida ma'lumot olish.</summary>
internal static class WindowsScreenInfo
{
    /// <summary>
    /// Barcha monitorlar (jismoniy piksellarda — jarayon Per-Monitor DPI v2 bo'lgani uchun).
    /// Har bir monitorning o'z DPI'si alohida olinadi (masalan, noutbuk 150% + proyektor 100%).
    /// </summary>
    public static ScreenLayout GetLayout()
    {
        var monitors = new List<MonitorInfo>();
        NativeMethods.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
        {
            var info = new NativeMethods.MONITORINFO { CbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                double dpi = 96;
                if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                {
                    dpi = dpiX;
                }

                var r = info.RcMonitor;
                var bounds = System.Drawing.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    monitors.Add(new MonitorInfo(bounds, dpi, (info.DwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
                }
            }

            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        if (monitors.Count == 0)
        {
            // Zaxira yo'l: WinForms ma'lumoti (DPI'siz).
            monitors.AddRange(Screen.AllScreens.Select(s => new MonitorInfo(s.Bounds, 96, s.Primary)));
        }

        return new ScreenLayout(monitors);
    }

    /// <summary>
    /// Faol (oldingi plandagi) oynaning ko'rinadigan chegarasi ekran koordinatalarida.
    /// Ish stoli, vazifalar paneli yoki yig'ilgan oyna bo'lsa — <c>null</c>.
    /// </summary>
    public static Rectangle? GetForegroundWindowBounds()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == NativeMethods.GetShellWindow() || NativeMethods.IsIconic(hwnd))
        {
            return null;
        }

        var buffer = new char[64];
        int len = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        string className = new(buffer, 0, Math.Max(0, len));
        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return null;
        }

        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var r, Marshal.SizeOf<NativeMethods.RECT>()) != 0
            && !NativeMethods.GetWindowRect(hwnd, out r))
        {
            return null;
        }

        var bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        return bounds.Width > 0 && bounds.Height > 0 ? bounds : null;
    }

    /// <summary>
    /// Oynani oldingi planga chiqaradi. Windows fon dasturlariga buni cheklaydi, shuning uchun
    /// joriy faol oynaning kiritish navbatiga vaqtincha ulanamiz (standart usul).
    /// </summary>
    public static void ForceForeground(IntPtr hwnd)
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        uint foregroundThread = foreground == IntPtr.Zero ? 0 : NativeMethods.GetWindowThreadProcessId(foreground, IntPtr.Zero);
        uint thisThread = NativeMethods.GetCurrentThreadId();

        if (foregroundThread != 0 && foregroundThread != thisThread)
        {
            NativeMethods.AttachThreadInput(thisThread, foregroundThread, true);
            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
            NativeMethods.AttachThreadInput(thisThread, foregroundThread, false);
        }
        else
        {
            NativeMethods.SetForegroundWindow(hwnd);
        }
    }
}
