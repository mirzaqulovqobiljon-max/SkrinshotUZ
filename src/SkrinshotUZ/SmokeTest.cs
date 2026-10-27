using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using SkrinshotUZ.Core.Editing;
using SkrinshotUZ.Editing;

namespace SkrinshotUZ;

/// <summary>
/// CI uchun "tutun testi" (<c>--smoke-test</c>): haqiqiy tray holati ishga tushadi (tugmalar ro'yxatdan o'tadi,
/// klaviatura hook'i o'rnatiladi, xotira bo'shatiladi), 5 soniya ishlaydi (shu orada overlay orqali to'liq skrinshot yo'li sinaladi), keyin xotira sarfi va tugmalar holatini faylga yozib,
/// o'zi yopiladi. Hech qanday oyna chiqmaydi, sozlamalar va reyestr o'zgarmaydi.
/// </summary>
internal static class SmokeTest
{
    private const int RunMilliseconds = 5000;

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
            // Avval xotira o'lchanadi, keyin qo'shimcha diagnostika (panel rasmi o'lchovga ta'sir qilmasin).
            WriteReport(reportPath, () => context.DiagnosticLines.Append("toolbars=" + RenderToolbarsPreview()));
            context.ExitThread();
        };
        timer.Start();
    }

    /// <summary>
    /// Panellarni haqiqiy Windows'da 100% va 150% masshtabda rasmga oladi (PNG, base64) — CI annotatsiyasi orqali
    /// ikonkalarning ko'rinishini ko'zdan kechirish uchun.
    /// </summary>
    internal static string RenderToolbarsPreview()
    {
        try
        {
            var parts = new List<Bitmap>();
            try
            {
                foreach (double scale in new[] { 1.0, 1.5 })
                {
                    using var bars = new EditorToolbars();
                    bars.UpdateState(EditorTool.Arrow, EditorController.DefaultColor, Thickness.Medium, canUndo: true, canRedo: false);
                    bars.SetScale(scale);
                    var tools = bars.Tools;
                    var actions = bars.Actions;
                    tools.Visible = true;
                    actions.Visible = true;
                    tools.Size = tools.PreferredSize;
                    actions.Size = actions.PreferredSize;

                    var part = new Bitmap(tools.Width + 10 + actions.Width, Math.Max(tools.Height, actions.Height), PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(part))
                    {
                        g.Clear(Color.FromArgb(120, 120, 120));
                    }

                    tools.DrawToBitmap(part, new Rectangle(0, 0, tools.Width, tools.Height));
                    actions.DrawToBitmap(part, new Rectangle(tools.Width + 10, 0, actions.Width, actions.Height));
                    parts.Add(part);
                }

                using var all = new Bitmap(parts.Sum(p => p.Width) + 20, parts.Max(p => p.Height), PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(all))
                {
                    g.Clear(Color.FromArgb(120, 120, 120));
                    int x = 0;
                    foreach (var p in parts)
                    {
                        g.DrawImageUnscaled(p, x, 0);
                        x += p.Width + 20;
                    }
                }

                using var stream = new MemoryStream();
                all.Save(stream, ImageFormat.Png);
                return "glyphFont=" + IconPainter.HasGlyphFont + " png=" + Convert.ToBase64String(stream.ToArray());
            }
            finally
            {
                foreach (var p in parts)
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ExternalException)
        {
            return "error " + ex.Message;
        }
    }

    private static void WriteReport(string? reportPath, Func<IEnumerable<string>> extraLines)
    {
        if (reportPath is null)
        {
            return;
        }

        // Xotira haqiqiy holatdagidek o'lchanadi: dastur ishga tushgach o'zi xotirani bo'shatgan,
        // so'ng 5 soniya tray'da ishlagan (ichida overlay sinovi ham bo'ladi). Bu yerda qo'shimcha tozalash QILINMAYDI.
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var sb = new StringBuilder();
        sb.AppendLine("status=ok");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"workingSetMB={process.WorkingSet64 / 1048576.0:F1}"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"privateMB={process.PrivateMemorySize64 / 1048576.0:F1}"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"peakWorkingSetMB={process.PeakWorkingSet64 / 1048576.0:F1}"));
        foreach (string line in extraLines())
        {
            sb.AppendLine(line);
        }

        File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
    }
}
