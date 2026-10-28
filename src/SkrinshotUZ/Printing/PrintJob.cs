using System.ComponentModel;
using System.Drawing.Printing;
using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Printing;

/// <summary>Kompyuterdagi printerlar haqida.</summary>
internal static class Printers
{
    /// <summary>Windows'dagi PDF printer nomi.</summary>
    public const string PdfPrinterName = "Microsoft Print to PDF";

    /// <summary>O'rnatilgan printerlar (o'qib bo'lmasa — bo'sh ro'yxat).</summary>
    public static IReadOnlyList<string> Installed()
    {
        try
        {
            return PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Windows'dagi standart printer (yo'q bo'lsa — <c>null</c>).</summary>
    public static string? Default()
    {
        try
        {
            var settings = new PrinterSettings();
            return settings.IsValid ? settings.PrinterName : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidPrinterException)
        {
            return null;
        }
    }

    /// <summary>PDF printer o'rnatilganmi.</summary>
    public static bool HasPdfPrinter() => Installed().Contains(PdfPrinterName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Printerning bosilmaydigan chetlari A4 kitob yo'nalishida, mm. Printer javob bermasa — 0
    /// (maydon baribir kamida foydalanuvchi tanlagan qiymat bo'ladi).
    /// </summary>
    /// <param name="printerName">Printer.</param>
    /// <returns>Chetlar.</returns>
    public static HardMargins HardMarginsOf(string? printerName)
    {
        if (string.IsNullOrEmpty(printerName))
        {
            return HardMargins.None;
        }

        try
        {
            var settings = new PrinterSettings { PrinterName = printerName };
            if (!settings.IsValid)
            {
                return HardMargins.None;
            }

            var page = (PageSettings)settings.DefaultPageSettings.Clone();
            page.Landscape = false;
            page.PaperSize = A4For(settings);
            return FromHundredths(page.HardMarginX, page.HardMarginY, page.PrintableArea, page.PaperSize);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidPrinterException or ArgumentException)
        {
            return HardMargins.None;
        }
    }

    /// <summary>
    /// Printer chetlarini (1/100 dyuym) millimetrga o'tkazadi: o'ng/past chet = qog'oz − chap/yuqori chet − bosiladigan soha.
    /// </summary>
    /// <param name="hardX">HardMarginX.</param>
    /// <param name="hardY">HardMarginY.</param>
    /// <param name="printable">PrintableArea.</param>
    /// <param name="paper">Qog'oz o'lchami.</param>
    /// <returns>Chetlar, mm.</returns>
    public static HardMargins FromHundredths(float hardX, float hardY, RectangleF printable, PaperSize paper)
    {
        ArgumentNullException.ThrowIfNull(paper);
        static double Mm(double h) => Math.Max(0, Units.HundredthsInchToMm(h));
        return new HardMargins(
            Mm(hardX),
            Mm(hardY),
            Mm(paper.Width - hardX - printable.Width),
            Mm(paper.Height - hardY - printable.Height));
    }

    /// <summary>Printerning A4 qog'ozi (ro'yxatda bo'lmasa — 210×297 mm o'zimiz).</summary>
    /// <param name="settings">Printer.</param>
    /// <returns>A4.</returns>
    public static PaperSize A4For(PrinterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            foreach (PaperSize size in settings.PaperSizes)
            {
                if (size.Kind == PaperKind.A4)
                {
                    return size;
                }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidPrinterException)
        {
            // printer qog'ozlar ro'yxatini bermadi — o'zimizning A4
        }

        return new PaperSize("A4", 827, 1169); // 1/100 dyuym
    }
}

/// <summary>
/// Bitta chop etish ishi: reja + rasmlar → <see cref="PrintDocument"/>. Har bir varaq yo'nalishi alohida beriladi,
/// chizishda printerning haqiqiy bosilmaydigan cheti (Graphics boshlanish nuqtasi) ayiriladi.
/// </summary>
internal sealed class PrintJob : IDisposable
{
    private readonly PrintPlan _plan;
    private readonly IReadOnlyList<Image> _images;
    private readonly bool _grayscale;
    private readonly PrintDocument _document;
    private int _next;

    /// <summary>Yangi ish.</summary>
    /// <param name="plan">Reja.</param>
    /// <param name="images">Rasmlar (egasi chaqiruvchi — bu yerda yopilmaydi).</param>
    /// <param name="grayscale">Oq-qora.</param>
    /// <param name="printerName">Printer (<c>null</c> — standart).</param>
    /// <param name="copies">Nusxalar soni.</param>
    /// <param name="pdfPath">Berilsa — "Microsoft Print to PDF" orqali shu faylga.</param>
    public PrintJob(PrintPlan plan, IReadOnlyList<Image> images, bool grayscale, string? printerName, int copies, string? pdfPath = null)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _images = images ?? throw new ArgumentNullException(nameof(images));
        _grayscale = grayscale;

        _document = new PrintDocument
        {
            DocumentName = "Skrinshot UZ",
            OriginAtMargins = false,
            PrintController = new StandardPrintController(), // "Chop etilmoqda…" oynasi chiqmasin
        };

        var settings = _document.PrinterSettings;
        if (pdfPath is not null)
        {
            settings.PrinterName = Printers.PdfPrinterName;
            settings.PrintToFile = true;
            settings.PrintFileName = pdfPath;
            copies = 1;
        }
        else if (!string.IsNullOrEmpty(printerName))
        {
            settings.PrinterName = printerName;
        }

        if (!settings.IsValid)
        {
            throw new InvalidPrinterException(settings);
        }

        settings.Copies = (short)Math.Clamp(copies, 1, PrintPreferences.MaxCopies);
        settings.Collate = true;

        var a4 = Printers.A4For(settings);
        _document.DefaultPageSettings.PaperSize = a4;
        _document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        _document.DefaultPageSettings.Landscape = plan.Pages[0].Orientation == PageOrientation.Landscape;
        if (grayscale)
        {
            _document.DefaultPageSettings.Color = false;
        }

        _document.BeginPrint += (_, _) => _next = 0;
        _document.QueryPageSettings += (_, e) =>
        {
            e.PageSettings.PaperSize = a4;
            e.PageSettings.Landscape = _plan.Pages[Math.Min(_next, _plan.Pages.Count - 1)].Orientation == PageOrientation.Landscape;
        };
        _document.PrintPage += OnPrintPage;
    }

    /// <summary>Hujjat (oldindan ko'rish yoki test uchun).</summary>
    public PrintDocument Document => _document;

    /// <summary>Printerga yuboradi (spooler'ga; qog'oz chiqishi printerga bog'liq).</summary>
    public void Print() => _document.Print();

    public void Dispose() => _document.Dispose();

    private void OnPrintPage(object? sender, PrintPageEventArgs e)
    {
        var page = _plan.Pages[_next];
        var g = e.Graphics!;

        // Printerda Graphics (0,0) — qog'oz chetida emas, bosiladigan soha boshida. Oldindan ko'rishda esa qog'oz chetida.
        bool preview = _document.PrintController?.IsPreview ?? false;
        var origin = preview
            ? PointF.Empty
            : new PointF((float)Units.HundredthsInchToMm(e.PageSettings.HardMarginX), (float)Units.HundredthsInchToMm(e.PageSettings.HardMarginY));

        PrintRenderer.DrawPage(g, page, _images, origin, _grayscale, showMargins: false);

        _next++;
        e.HasMorePages = _next < _plan.Pages.Count;
    }
}
