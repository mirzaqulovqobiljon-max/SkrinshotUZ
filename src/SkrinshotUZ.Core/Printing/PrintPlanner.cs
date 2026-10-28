using System.Globalization;

namespace SkrinshotUZ.Core.Printing;

/// <summary>Chop etish rejimi.</summary>
public enum PrintMode
{
    /// <summary>A) A4 to'liq varaq.</summary>
    FullPage,

    /// <summary>B) A4 yarmi (yuqori yarmi).</summary>
    HalfPage,

    /// <summary>B qo'shimcha) Bir varaqqa 2 ta: yuqori va pastki yarmi, o'rtada kesish chizig'i.</summary>
    TwoPerPage,

    /// <summary>C) Asl o'lchamda (ekrandagi kattalikda).</summary>
    ActualSize,
}

/// <summary>Varaq yo'nalishini tanlash.</summary>
public enum OrientationChoice
{
    /// <summary>Avtomatik: keng rasm → albom, baland → kitob.</summary>
    Auto,

    /// <summary>Kitob (tik).</summary>
    Portrait,

    /// <summary>Albom (yotiq).</summary>
    Landscape,
}

/// <summary>Asl o'lchamdagi rasm varaqqa sig'masa nima qilinadi.</summary>
public enum OverflowChoice
{
    /// <summary>Varaqqa sig'dirish (kichraytirish).</summary>
    FitToPage,

    /// <summary>Bir nechta varaqqa bo'lish (asl o'lcham saqlanadi).</summary>
    SplitPages,
}

/// <summary>Foydalanuvchi tanlagan chop etish sozlamalari.</summary>
public sealed record PrintOptions
{
    /// <summary>Rejim.</summary>
    public PrintMode Mode { get; init; } = PrintMode.FullPage;

    /// <summary>Yo'nalish (A4 yarmi rejimlarida varaq doim kitob).</summary>
    public OrientationChoice Orientation { get; init; } = OrientationChoice.Auto;

    /// <summary>Maydon, mm (0/5/10/15).</summary>
    public double MarginMm { get; init; } = PrintMargins.DefaultUserMarginMm;

    /// <summary>Asl o'lcham sig'masa.</summary>
    public OverflowChoice Overflow { get; init; } = OverflowChoice.FitToPage;

    /// <summary>Rasm ostidagi yozuv (sarlavha va/yoki sana); <c>null</c> yoki bo'sh — yozuv yo'q.</summary>
    public string? Caption { get; init; }
}

/// <summary>Chop etiladigan rasm haqida ma'lumot.</summary>
/// <param name="WidthPx">Kenglik, px.</param>
/// <param name="HeightPx">Balandlik, px.</param>
/// <param name="Dpi">Rasm olingan monitorning DPI'si (Windows masshtabi: 100% = 96, 150% = 144).</param>
public sealed record PrintImage(int WidthPx, int HeightPx, double Dpi);

/// <summary>Varaqdagi bitta rasm (yoki uning bo'lagi).</summary>
/// <param name="ImageIndex">Rasm raqami (0 — asosiy, 1 — "Bir varaqqa 2 ta"dagi ikkinchi rasm).</param>
/// <param name="Source">Rasmdan olinadigan qism, px.</param>
/// <param name="Dest">Varaqdagi joyi (varaq chetidan), mm.</param>
/// <param name="Area">Shu rasm uchun ajratilgan soha (maydonlar ichi), mm.</param>
public sealed record PlacedImage(int ImageIndex, PixelRect Source, RectMm Dest, RectMm Area);

/// <summary>Rasm ostidagi yozuv.</summary>
/// <param name="Text">Matn.</param>
/// <param name="Rect">Joyi, mm.</param>
public sealed record PlacedCaption(string Text, RectMm Rect);

/// <summary>Bitta varaq.</summary>
/// <param name="Orientation">Yo'nalish.</param>
/// <param name="Size">O'lcham, mm.</param>
/// <param name="Images">Rasmlar.</param>
/// <param name="Captions">Yozuvlar.</param>
/// <param name="CutLineYMm">Punktir kesish chizig'i (faqat "Bir varaqqa 2 ta"), mm.</param>
public sealed record PrintPage(
    PageOrientation Orientation,
    SizeMm Size,
    IReadOnlyList<PlacedImage> Images,
    IReadOnlyList<PlacedCaption> Captions,
    double? CutLineYMm);

/// <summary>Chop etish rejasi: varaqlar va foydalanuvchiga ko'rsatiladigan ma'lumot.</summary>
/// <param name="Pages">Varaqlar (kamida bitta).</param>
/// <param name="ImageSizeMm">Asosiy rasmning qog'ozdagi o'lchami, mm (bo'lingan bo'lsa — to'liq o'lchami).</param>
/// <param name="ActualSizeMm">Rasmning ekrandagi (asl) o'lchami, mm.</param>
/// <param name="ActualSizeOverflows">Asl o'lchamdagi rasm bitta varaqqa sig'maydimi (ogohlantirish uchun).</param>
public sealed record PrintPlan(IReadOnlyList<PrintPage> Pages, SizeMm ImageSizeMm, SizeMm ActualSizeMm, bool ActualSizeOverflows)
{
    /// <summary>Qog'ozdagi o'lchamning asl o'lchamga nisbati, % (100 — asl o'lchamda).</summary>
    public double ScalePercent => ActualSizeMm.Width <= 0 ? 0 : ImageSizeMm.Width / ActualSizeMm.Width * 100.0;
}

/// <summary>
/// Sozlamalar + rasm(lar) + printer chetlaridan to'liq chop etish rejasini tuzadi. Graphics'ga hech narsa chizmaydi
/// (chizish — ilovadagi renderer), shuning uchun to'liq unit-test qilinadi.
/// </summary>
public static class PrintPlanner
{
    /// <summary>Rasm ostida yozuv uchun ajratiladigan joy, mm.</summary>
    public const double CaptionReserveMm = 8.0;

    /// <summary>Rasm bilan yozuv orasidagi bo'shliq, mm.</summary>
    public const double CaptionGapMm = 1.5;

    /// <summary>Yozuv qatorining balandligi, mm.</summary>
    public const double CaptionHeightMm = CaptionReserveMm - CaptionGapMm;

    /// <summary>Rejani tuzadi.</summary>
    /// <param name="main">Asosiy rasm.</param>
    /// <param name="second">"Bir varaqqa 2 ta"da pastki yarmi uchun boshqa rasm; <c>null</c> — asosiy rasm takrorlanadi.</param>
    /// <param name="options">Sozlamalar.</param>
    /// <param name="hardPortrait">Printerning bosilmaydigan chetlari, kitob yo'nalishida, mm.</param>
    /// <returns>Reja.</returns>
    public static PrintPlan Create(PrintImage main, PrintImage? second, PrintOptions options, HardMargins hardPortrait)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(options);
        Guard.Positive(main.WidthPx, nameof(main) + ".WidthPx");
        Guard.Positive(main.HeightPx, nameof(main) + ".HeightPx");
        Guard.Positive(main.Dpi, nameof(main) + ".Dpi");

        string? caption = string.IsNullOrWhiteSpace(options.Caption) ? null : options.Caption.Trim();
        double reserve = caption is null ? 0 : CaptionReserveMm;
        var actual = PrintLayoutCalculator.ActualSizeMm(main.WidthPx, main.HeightPx, main.Dpi);

        switch (options.Mode)
        {
            case PrintMode.FullPage:
            {
                var orientation = Resolve(options.Orientation, main);
                var layout = PrintLayoutCalculator.FitToA4(main.WidthPx, main.HeightPx, orientation, Margins(options, hardPortrait, orientation), reserve);
                return Single(layout, caption, actual, overflows: false);
            }

            case PrintMode.HalfPage:
            {
                var layout = PrintLayoutCalculator.FitToHalfA4(main.WidthPx, main.HeightPx, HalfPageSlot.Top, Margins(options, hardPortrait, PageOrientation.Portrait), reserve);
                return Single(layout, caption, actual, overflows: false);
            }

            case PrintMode.TwoPerPage:
            {
                var bottomImage = second ?? main;
                Guard.Positive(bottomImage.WidthPx, nameof(second) + ".WidthPx");
                Guard.Positive(bottomImage.HeightPx, nameof(second) + ".HeightPx");
                var two = PrintLayoutCalculator.TwoPerPage(
                    main.WidthPx, main.HeightPx, bottomImage.WidthPx, bottomImage.HeightPx, Margins(options, hardPortrait, PageOrientation.Portrait), reserve);
                var images = new[]
                {
                    Place(0, two.Top),
                    Place(second is null ? 0 : 1, two.Bottom),
                };
                var captions = caption is null
                    ? Array.Empty<PlacedCaption>()
                    : new[] { CaptionUnder(caption, two.Top), CaptionUnder(caption, two.Bottom) };
                var page = new PrintPage(PageOrientation.Portrait, two.Top.Page, images, captions, two.CutLineYMm);
                return new PrintPlan(new[] { page }, Size(two.Top.ImageRect), actual, ActualSizeOverflows: false);
            }

            case PrintMode.ActualSize:
            {
                var orientation = Resolve(options.Orientation, main);
                var margins = Margins(options, hardPortrait, orientation);
                var layout = PrintLayoutCalculator.ActualSize(main.WidthPx, main.HeightPx, main.Dpi, orientation, margins, reserve);
                if (layout.FitsOnPage)
                {
                    return Single(layout, caption, actual, overflows: false);
                }

                if (options.Overflow == OverflowChoice.FitToPage)
                {
                    var fitted = PrintLayoutCalculator.FitToA4(main.WidthPx, main.HeightPx, orientation, margins, reserve);
                    return Single(fitted, caption, actual, overflows: true);
                }

                var tiles = PrintLayoutCalculator.SplitIntoPages(main.WidthPx, main.HeightPx, main.Dpi, orientation, margins, reserve);
                var pages = tiles.Select(t => new PrintPage(
                        orientation,
                        t.Layout.Page,
                        new[] { Place(0, t.Layout) },
                        caption is null ? Array.Empty<PlacedCaption>() : new[] { CaptionUnder(PageCaption(caption, t.PageNumber, tiles.Count), t.Layout) },
                        null))
                    .ToList();
                return new PrintPlan(pages, actual, actual, ActualSizeOverflows: true);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Noma'lum chop etish rejimi.");
        }
    }

    /// <summary>
    /// Printer chetlari kitob yo'nalishida o'lchanadi. Albomda printer varaqni 90° yoki 270° buradi (drayverga bog'liq),
    /// shuning uchun ikkala holatda ham xavfsiz bo'lishi uchun qarama-qarshi chetlarning kattasi olinadi.
    /// </summary>
    /// <param name="hardPortrait">Kitob yo'nalishidagi chetlar.</param>
    /// <param name="orientation">Varaq yo'nalishi.</param>
    /// <returns>Shu yo'nalish uchun chetlar.</returns>
    public static HardMargins ForOrientation(HardMargins hardPortrait, PageOrientation orientation)
    {
        if (orientation == PageOrientation.Portrait)
        {
            return hardPortrait;
        }

        double sides = Math.Max(hardPortrait.Top, hardPortrait.Bottom);
        double ends = Math.Max(hardPortrait.Left, hardPortrait.Right);
        return new HardMargins(sides, ends, sides, ends);
    }

    /// <summary>Rasm ostidagi yozuv matni: "Sarlavha · 28.10.2026".</summary>
    /// <param name="title">Sarlavha (ixtiyoriy).</param>
    /// <param name="date">Sana (<c>null</c> — sana qo'yilmaydi).</param>
    /// <returns>Matn yoki <c>null</c> (hech narsa yo'q bo'lsa).</returns>
    public static string? BuildCaption(string? title, DateTime? date)
    {
        string? t = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        string? d = date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        return (t, d) switch
        {
            (null, null) => null,
            (null, _) => d,
            (_, null) => t,
            _ => t + " · " + d,
        };
    }

    /// <summary>Varaq yo'nalishi: tanlov yoki avtomatik.</summary>
    /// <param name="choice">Tanlov.</param>
    /// <param name="image">Rasm.</param>
    /// <returns>Yo'nalish.</returns>
    public static PageOrientation Resolve(OrientationChoice choice, PrintImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return choice switch
        {
            OrientationChoice.Portrait => PageOrientation.Portrait,
            OrientationChoice.Landscape => PageOrientation.Landscape,
            _ => PrintLayoutCalculator.AutoOrientation(image.WidthPx, image.HeightPx),
        };
    }

    private static PrintMargins Margins(PrintOptions options, HardMargins hardPortrait, PageOrientation orientation) =>
        new(options.MarginMm, ForOrientation(hardPortrait, orientation));

    private static PrintPlan Single(PageLayout layout, string? caption, SizeMm actual, bool overflows)
    {
        var captions = caption is null ? Array.Empty<PlacedCaption>() : new[] { CaptionUnder(caption, layout) };
        var page = new PrintPage(layout.Orientation, layout.Page, new[] { Place(0, layout) }, captions, null);
        return new PrintPlan(new[] { page }, Size(layout.ImageRect), actual, overflows);
    }

    private static PlacedImage Place(int index, PageLayout layout) => new(index, layout.Source, layout.ImageRect, layout.PrintableArea);

    /// <summary>Yozuv rasm ostida, soha kengligida, markazda (tor rasmda ham matn sig'adi).</summary>
    private static PlacedCaption CaptionUnder(string text, PageLayout layout) =>
        new(text, new RectMm(layout.PrintableArea.X, layout.ImageRect.Bottom + CaptionGapMm, layout.PrintableArea.Width, CaptionHeightMm));

    private static string PageCaption(string caption, int page, int total) =>
        total <= 1 ? caption : string.Create(CultureInfo.InvariantCulture, $"{caption} ({page}/{total})");

    private static SizeMm Size(RectMm r) => new(r.Width, r.Height);
}
