using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SkrinshotUZ;

/// <summary>
/// CI uchun "tutun testi" (<c>--smoke-test</c>): haqiqiy tray holati ishga tushadi (tugmalar ro'yxatdan o'tadi,
/// klaviatura hook'i o'rnatiladi), 3 soniya ishlaydi, keyin xotira sarfi va tugmalar holatini faylga yozib,
/// o'zi yopiladi. Hech qanday oyna chiqmaydi, sozlamalar va reyestr o'zgarmaydi.
/// </summary>
internal static class SmokeTest
{
    private const int RunMilliseconds = 3000;

    /// <summary>Taymerni ulaydi.</summary>
    /// <param name="context">Ilova holati.</param>
    /// <param name="reportPath">Hisobot fayli (ixtiyoriy).</param>
    public static void Attach(TrayApplicationContext context, string? reportPath)
    {
        var timer = new System.Windows.Forms.Timer { Interval = RunMilliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            WriteReport(reportPath, context.DiagnosticLines);
            context.ExitThread();
        };
        timer.Start();
    }

    private static void WriteReport(string? reportPath, IEnumerable<string> extraLines)
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
        var sb = new StringBuilder();
        sb.AppendLine("status=ok");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"workingSetMB={process.WorkingSet64 / 1048576.0:F1}"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"privateMB={process.PrivateMemorySize64 / 1048576.0:F1}"));
        foreach (string line in extraLines)
        {
            sb.AppendLine(line);
        }

        File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
    }
}
