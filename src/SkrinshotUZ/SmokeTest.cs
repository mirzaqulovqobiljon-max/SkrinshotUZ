using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SkrinshotUZ;

/// <summary>
/// CI uchun "tutun testi" (<c>--smoke-test</c>): haqiqiy tray holati ishga tushadi, 3 soniya ishlaydi,
/// keyin xotira sarfini faylga yozib, o'zi yopiladi. Shu orqali har bir build'da
/// .exe haqiqatan ishga tushishi va xotira talabi (&lt;40 MB) tekshiriladi.
/// </summary>
internal static class SmokeTest
{
    private const int RunMilliseconds = 3000;

    /// <summary>Taymerni ulaydi.</summary>
    /// <param name="context">Ilova holati.</param>
    /// <param name="reportPath">Hisobot fayli (ixtiyoriy).</param>
    public static void Attach(ApplicationContext context, string? reportPath)
    {
        var timer = new System.Windows.Forms.Timer { Interval = RunMilliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            WriteReport(reportPath);
            context.ExitThread();
        };
        timer.Start();
    }

    private static void WriteReport(string? reportPath)
    {
        if (reportPath is null)
        {
            return;
        }

        // Tray'da kutib turgan holatdagi barqaror xotira (vaqtinchalik obyektlarsiz).
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"status=ok{Environment.NewLine}workingSetMB={process.WorkingSet64 / 1048576.0:F1}{Environment.NewLine}privateMB={process.PrivateMemorySize64 / 1048576.0:F1}{Environment.NewLine}");
        File.WriteAllText(reportPath, text, Encoding.UTF8);
    }
}
