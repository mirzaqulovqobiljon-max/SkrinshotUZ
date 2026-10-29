using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using SkrinshotUZ.Core.Editing;
using SkrinshotUZ.Core.Ocr;
using SkrinshotUZ.Editing;
using SkrinshotUZ.Ocr;

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
            WriteReport(reportPath, () => context.DiagnosticLines.Append("toolbars=" + RenderToolbarsPreview()).Append("ocr=" + RunOcrCheck()));
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

    /// <summary>Sinov uchun matnli rasm: lotin va kirill qatorlari, oq fonda qora, ClearType'siz.</summary>
    internal static Bitmap RenderSampleText(float fontPx = 20f)
    {
        var bmp = new Bitmap(760, 150, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Arial", fontPx, FontStyle.Regular, GraphicsUnit.Pixel);
        g.DrawString(OcrSampleLatin, font, Brushes.Black, 20, 25);
        g.DrawString(OcrSampleCyrillic, font, Brushes.Black, 20, 85);
        return bmp;
    }

    /// <summary>Sinov matni (lotin).</summary>
    internal const string OcrSampleLatin = "Skrinshot UZ dasturi 2026 yil";

    /// <summary>Sinov matni (kirill).</summary>
    internal const string OcrSampleCyrillic = "Ўзбекистон Республикаси";

    /// <summary>
    /// OCR'ni haqiqiy .exe ichida tekshiradi: fayllarni chiqarish → kutubxonalarni yuklash (shu arxitekturada) →
    /// lotin va kirill matnni tanish → .docx yozib, qayta o'qish. Xotira o'lchovidan keyin ishlaydi.
    /// </summary>
    internal static string RunOcrCheck()
    {
        if (!OcrService.IsAvailable)
        {
            return "missing " + string.Join(",", OcrFiles.Missing(OcrService.EmbeddedFiles.ToList()));
        }

        string dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.ocr." + Guid.NewGuid().ToString("N"));
        try
        {
            var watch = Stopwatch.StartNew();
            var engine = OcrService.Prepare();
            long prepareMs = watch.ElapsedMilliseconds;
            using var sample = RenderSampleText();
            var result = OcrService.Recognize(sample, 96);
            long totalMs = watch.ElapsedMilliseconds;
            string text = string.Join(" | ", result.Paragraphs.SelectMany(p => p.Lines));
            bool latin = text.Contains("Skrinshot", StringComparison.Ordinal) && text.Contains("2026", StringComparison.Ordinal);
            bool cyrillic = text.Contains("Республик", StringComparison.Ordinal);

            string path = OcrService.SaveDocument(result, dir);
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            bool inDocx = reader.ReadToEnd().Contains("Skrinshot", StringComparison.Ordinal);

            string info = string.Create(CultureInfo.InvariantCulture,
                $"tesseract={engine.Version} arch={RuntimeInformation.ProcessArchitecture} files={OcrService.EmbeddedFiles.Count} prepareMs={prepareMs} totalMs={totalMs} latin={latin} cyrillic={cyrillic} docx={inDocx} text={text.Replace('\n', ' ')}");
            return (latin && cyrillic && inDocx ? "ok " : "error ") + info;
        }
        catch (Exception ex) when (ex is OcrUnavailableException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or ExternalException or BadImageFormatException or EntryPointNotFoundException)
        {
            return "error " + ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (IOException)
            {
                // vaqtinchalik papka — muhim emas
            }
        }
    }

    /// <summary>Rasmni kichraytirib JPG (base64) qiladi — CI annotatsiyasiga sig'ishi uchun.</summary>
    internal static string ToJpegBase64(Bitmap image, double scale, long quality)
    {
        int w = Math.Max(1, (int)(image.Width * scale));
        int h = Math.Max(1, (int)(image.Height * scale));
        using var small = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, 0, 0, w, h);
        }

        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        using var stream = new MemoryStream();
        small.Save(stream, codec, parameters);
        return Convert.ToBase64String(stream.ToArray());
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
