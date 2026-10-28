namespace SkrinshotUZ.Core.Printing;

/// <summary>Bitta varaqdagi rasm joylashuvi.</summary>
/// <param name="Orientation">Varaq yo'nalishi.</param>
/// <param name="Page">Varaq o'lchami (yo'nalishni hisobga olgan holda), mm.</param>
/// <param name="PrintableArea">Rasm uchun ajratilgan soha (maydonlar ayirilgan), mm.</param>
/// <param name="ImageRect">Rasm chiziladigan to'rtburchak (varaq koordinatalarida), mm.</param>
/// <param name="Source">Manba rasmdan olinadigan qism, px.</param>
/// <param name="FitsOnPage">Rasm <see cref="PrintableArea"/> ichiga to'liq sig'adimi.</param>
public sealed record PageLayout(
    PageOrientation Orientation,
    SizeMm Page,
    RectMm PrintableArea,
    RectMm ImageRect,
    PixelRect Source,
    bool FitsOnPage)
{
    /// <summary>Bitta pikselning qog'ozdagi o'lchami, mm.</summary>
    public double MmPerPixel => ImageRect.Width / Source.Width;

    /// <summary>
    /// Printer Graphics koordinatalariga o'tkazish: PrintDocument'da (0,0) nuqta qog'oz chetida emas,
    /// printerning bosilmaydigan cheti boshida joylashadi. Shuning uchun shu chetni ayiramiz.
    /// </summary>
    /// <param name="hard">Printerning bosilmaydigan cheti.</param>
    /// <returns>Graphics uchun to'rtburchak, mm.</returns>
    public RectMm ToDeviceCoordinates(HardMargins hard) => ImageRect.Offset(-hard.Left, -hard.Top);
}

/// <summary>Ko'p varaqqa bo'lingan rasmning bitta bo'lagi.</summary>
/// <param name="PageNumber">Varaq tartib raqami (1 dan).</param>
/// <param name="Row">Qator (0 dan, yuqoridan pastga).</param>
/// <param name="Column">Ustun (0 dan, chapdan o'ngga).</param>
/// <param name="Layout">Shu varaqdagi joylashuv.</param>
public sealed record TileLayout(int PageNumber, int Row, int Column, PageLayout Layout);

/// <summary>"Bir varaqqa 2 ta" rejimi: yuqori va pastki yarim, o'rtada kesish chizig'i.</summary>
/// <param name="Top">Yuqori yarmidagi rasm.</param>
/// <param name="Bottom">Pastki yarmidagi rasm.</param>
/// <param name="CutLineYMm">Punktir kesish chizig'ining Y koordinatasi, mm.</param>
public sealed record TwoPerPageLayout(PageLayout Top, PageLayout Bottom, double CutLineYMm);

/// <summary>
/// Chop etish maketi hisoblari (A4 to'liq, A4 yarmi, asl o'lcham, ko'p varaqqa bo'lish).
/// Barcha natijalar millimetrda, varaqning yuqori-chap burchagidan. Graphics'ga hech narsa chizmaydi —
/// shuning uchun to'liq unit-test qilinadi.
/// </summary>
public static class PrintLayoutCalculator
{
    /// <summary>A4 kengligi, mm.</summary>
    public const double A4WidthMm = 210.0;

    /// <summary>A4 balandligi, mm.</summary>
    public const double A4HeightMm = 297.0;

    /// <summary>A4 yarmining balandligi, mm (= kesish chizig'i joyi).</summary>
    public const double HalfA4HeightMm = A4HeightMm / 2.0;

    /// <summary>Ko'p varaqqa bo'lishda "sig'adi" deb hisoblash uchun ruxsat etilgan xatolik, mm.</summary>
    private const double FitTolerance = 1e-6;

    /// <summary>Avtomatik yo'nalish: keng rasm → albom, baland yoki kvadrat → kitob.</summary>
    /// <param name="widthPx">Rasm kengligi, px.</param>
    /// <param name="heightPx">Rasm balandligi, px.</param>
    /// <returns>Tavsiya etilgan yo'nalish.</returns>
    public static PageOrientation AutoOrientation(int widthPx, int heightPx)
    {
        Guard.Positive(widthPx, nameof(widthPx));
        Guard.Positive(heightPx, nameof(heightPx));
        return widthPx > heightPx ? PageOrientation.Landscape : PageOrientation.Portrait;
    }

    /// <summary>A4 varaq o'lchami berilgan yo'nalishda.</summary>
    /// <param name="orientation">Yo'nalish.</param>
    /// <returns>O'lcham, mm.</returns>
    public static SizeMm A4(PageOrientation orientation) => orientation switch
    {
        PageOrientation.Portrait => new SizeMm(A4WidthMm, A4HeightMm),
        PageOrientation.Landscape => new SizeMm(A4HeightMm, A4WidthMm),
        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Noma'lum yo'nalish."),
    };

    /// <summary>A) Rasmni A4 to'liq varaqqa proporsiyasini buzmasdan maksimal kattalikda joylash (markazda).</summary>
    /// <param name="widthPx">Rasm kengligi, px.</param>
    /// <param name="heightPx">Rasm balandligi, px.</param>
    /// <param name="orientation">Varaq yo'nalishi (avtomatik uchun <see cref="AutoOrientation"/>).</param>
    /// <param name="margins">Maydonlar.</param>
    /// <param name="bottomReserveMm">Rasm ostida qoldiriladigan joy (sarlavha/sana uchun), mm.</param>
    /// <returns>Joylashuv.</returns>
    public static PageLayout FitToA4(int widthPx, int heightPx, PageOrientation orientation, PrintMargins margins, double bottomReserveMm = 0)
    {
        Guard.Positive(widthPx, nameof(widthPx));
        Guard.Positive(heightPx, nameof(heightPx));
        ArgumentNullException.ThrowIfNull(margins);

        Guard.NonNegative(bottomReserveMm, nameof(bottomReserveMm));

        var page = A4(orientation);
        var area = PrintableArea(new RectMm(0, 0, page.Width, page.Height), margins.EffectiveLeft, margins.EffectiveTop, margins.EffectiveRight, margins.EffectiveBottom + bottomReserveMm);
        var image = FitCentered(area, widthPx, heightPx);
        return new PageLayout(orientation, page, area, image, new PixelRect(0, 0, widthPx, heightPx), FitsOnPage: true);
    }

    /// <summary>
    /// B) Rasmni A4 yarmiga (210×148.5 mm) joylash. Varaq doim kitob yo'nalishida.
    /// Kesish chizig'i tomonida faqat foydalanuvchi maydoni qo'llanadi (printer cheti u yerda yo'q).
    /// </summary>
    /// <param name="widthPx">Rasm kengligi, px.</param>
    /// <param name="heightPx">Rasm balandligi, px.</param>
    /// <param name="slot">Yuqori yoki pastki yarim.</param>
    /// <param name="margins">Maydonlar.</param>
    /// <param name="bottomReserveMm">Rasm ostida qoldiriladigan joy (sarlavha/sana uchun), mm.</param>
    /// <returns>Joylashuv.</returns>
    public static PageLayout FitToHalfA4(int widthPx, int heightPx, HalfPageSlot slot, PrintMargins margins, double bottomReserveMm = 0)
    {
        Guard.Positive(widthPx, nameof(widthPx));
        Guard.Positive(heightPx, nameof(heightPx));
        ArgumentNullException.ThrowIfNull(margins);
        Guard.NonNegative(bottomReserveMm, nameof(bottomReserveMm));

        var page = A4(PageOrientation.Portrait);
        RectMm area = slot switch
        {
            HalfPageSlot.Top => PrintableArea(
                new RectMm(0, 0, A4WidthMm, HalfA4HeightMm),
                margins.EffectiveLeft, margins.EffectiveTop, margins.EffectiveRight, margins.UserMarginMm + bottomReserveMm),
            HalfPageSlot.Bottom => PrintableArea(
                new RectMm(0, HalfA4HeightMm, A4WidthMm, HalfA4HeightMm),
                margins.EffectiveLeft, margins.UserMarginMm, margins.EffectiveRight, margins.EffectiveBottom + bottomReserveMm),
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Noma'lum yarim."),
        };

        var image = FitCentered(area, widthPx, heightPx);
        return new PageLayout(PageOrientation.Portrait, page, area, image, new PixelRect(0, 0, widthPx, heightPx), FitsOnPage: true);
    }

    /// <summary>B qo'shimcha) "Bir varaqqa 2 ta": yuqori va pastki yarimga ikki rasm (bir xil bo'lishi ham mumkin).</summary>
    /// <param name="topWidthPx">Yuqori rasm kengligi, px.</param>
    /// <param name="topHeightPx">Yuqori rasm balandligi, px.</param>
    /// <param name="bottomWidthPx">Pastki rasm kengligi, px.</param>
    /// <param name="bottomHeightPx">Pastki rasm balandligi, px.</param>
    /// <param name="margins">Maydonlar.</param>
    /// <param name="bottomReserveMm">Rasm ostida qoldiriladigan joy (sarlavha/sana uchun), mm.</param>
    /// <returns>Ikki joylashuv va kesish chizig'i.</returns>
    public static TwoPerPageLayout TwoPerPage(int topWidthPx, int topHeightPx, int bottomWidthPx, int bottomHeightPx, PrintMargins margins, double bottomReserveMm = 0) =>
        new(
            FitToHalfA4(topWidthPx, topHeightPx, HalfPageSlot.Top, margins, bottomReserveMm),
            FitToHalfA4(bottomWidthPx, bottomHeightPx, HalfPageSlot.Bottom, margins, bottomReserveMm),
            HalfA4HeightMm);

    /// <summary>Rasmning ekrandagi haqiqiy o'lchami, mm.</summary>
    /// <param name="widthPx">Kenglik, px.</param>
    /// <param name="heightPx">Balandlik, px.</param>
    /// <param name="monitorDpi">Monitor DPI qiymati.</param>
    /// <returns>O'lcham, mm.</returns>
    public static SizeMm ActualSizeMm(int widthPx, int heightPx, double monitorDpi)
    {
        Guard.Positive(widthPx, nameof(widthPx));
        Guard.Positive(heightPx, nameof(heightPx));
        return new SizeMm(Units.PixelsToMm(widthPx, monitorDpi), Units.PixelsToMm(heightPx, monitorDpi));
    }

    /// <summary>
    /// C) Asl o'lchamda: rasm masshtablanmaydi, chop etiladigan sohaning yuqori-chap burchagiga qo'yiladi.
    /// Varaqdan katta bo'lsa <see cref="PageLayout.FitsOnPage"/> = <c>false</c> — UI ogohlantiradi va
    /// "Varaqqa sig'dirish" (<see cref="FitToA4"/>) yoki "Bir nechta varaqqa bo'lish" (<see cref="SplitIntoPages"/>) taklif qiladi.
    /// </summary>
    /// <param name="widthPx">Kenglik, px.</param>
    /// <param name="heightPx">Balandlik, px.</param>
    /// <param name="monitorDpi">Monitor DPI qiymati.</param>
    /// <param name="orientation">Varaq yo'nalishi.</param>
    /// <param name="margins">Maydonlar.</param>
    /// <param name="bottomReserveMm">Rasm ostida qoldiriladigan joy (sarlavha/sana uchun), mm.</param>
    /// <returns>Joylashuv.</returns>
    public static PageLayout ActualSize(int widthPx, int heightPx, double monitorDpi, PageOrientation orientation, PrintMargins margins, double bottomReserveMm = 0)
    {
        ArgumentNullException.ThrowIfNull(margins);
        Guard.NonNegative(bottomReserveMm, nameof(bottomReserveMm));
        var size = ActualSizeMm(widthPx, heightPx, monitorDpi);
        var page = A4(orientation);
        var area = PrintableArea(new RectMm(0, 0, page.Width, page.Height), margins.EffectiveLeft, margins.EffectiveTop, margins.EffectiveRight, margins.EffectiveBottom + bottomReserveMm);
        var image = new RectMm(area.X, area.Y, size.Width, size.Height);
        bool fits = image.IsInside(area, FitTolerance);
        return new PageLayout(orientation, page, area, image, new PixelRect(0, 0, widthPx, heightPx), fits);
    }

    /// <summary>
    /// Asl o'lchamdagi rasmni bir nechta varaqqa bo'lish (qatorma-qator, chapdan o'ngga).
    /// Bo'laklar butun piksellarda kesiladi: ular orasida bo'shliq ham, ustma-ust tushish ham yo'q,
    /// har bir bo'lak chop etiladigan sohaga sig'adi.
    /// </summary>
    /// <param name="widthPx">Kenglik, px.</param>
    /// <param name="heightPx">Balandlik, px.</param>
    /// <param name="monitorDpi">Monitor DPI qiymati.</param>
    /// <param name="orientation">Varaq yo'nalishi.</param>
    /// <param name="margins">Maydonlar.</param>
    /// <param name="bottomReserveMm">Rasm ostida qoldiriladigan joy (sarlavha/sana uchun), mm.</param>
    /// <returns>Varaqlar ro'yxati (kamida bitta).</returns>
    public static IReadOnlyList<TileLayout> SplitIntoPages(int widthPx, int heightPx, double monitorDpi, PageOrientation orientation, PrintMargins margins, double bottomReserveMm = 0)
    {
        Guard.Positive(widthPx, nameof(widthPx));
        Guard.Positive(heightPx, nameof(heightPx));
        Guard.Positive(monitorDpi, nameof(monitorDpi));
        ArgumentNullException.ThrowIfNull(margins);
        Guard.NonNegative(bottomReserveMm, nameof(bottomReserveMm));

        var page = A4(orientation);
        var area = PrintableArea(new RectMm(0, 0, page.Width, page.Height), margins.EffectiveLeft, margins.EffectiveTop, margins.EffectiveRight, margins.EffectiveBottom + bottomReserveMm);

        // Bir varaqqa sig'adigan butun piksellar soni (kichik epsilon — 2.0 ning 1.9999999 bo'lib qolmasligi uchun).
        int tileW = (int)Math.Floor(Units.MmToPixels(area.Width, monitorDpi) + 1e-9);
        int tileH = (int)Math.Floor(Units.MmToPixels(area.Height, monitorDpi) + 1e-9);
        if (tileW < 1 || tileH < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(monitorDpi), monitorDpi, "Bu DPI va maydonlar bilan varaqqa hatto bitta piksel ham sig'maydi.");
        }

        int columns = (widthPx + tileW - 1) / tileW;
        int rows = (heightPx + tileH - 1) / tileH;
        var result = new List<TileLayout>(rows * columns);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int x = col * tileW;
                int y = row * tileH;
                int w = Math.Min(tileW, widthPx - x);
                int h = Math.Min(tileH, heightPx - y);
                var image = new RectMm(area.X, area.Y, Units.PixelsToMm(w, monitorDpi), Units.PixelsToMm(h, monitorDpi));
                var layout = new PageLayout(orientation, page, area, image, new PixelRect(x, y, w, h), image.IsInside(area, FitTolerance));
                result.Add(new TileLayout(result.Count + 1, row, col, layout));
            }
        }

        return result;
    }

    /// <summary>Sohadan maydonlarni ayiradi; joy qolmasa xato beradi.</summary>
    private static RectMm PrintableArea(RectMm region, double left, double top, double right, double bottom)
    {
        double w = region.Width - left - right;
        double h = region.Height - top - bottom;
        if (w <= 0 || h <= 0)
        {
            throw new ArgumentException("Maydonlar juda katta: rasm uchun joy qolmadi.", nameof(region));
        }

        return new RectMm(region.X + left, region.Y + top, w, h);
    }

    /// <summary>Proporsiyani saqlab, sohaga maksimal kattalikda markazlab joylaydi.</summary>
    private static RectMm FitCentered(RectMm area, int widthPx, int heightPx)
    {
        double scale = Math.Min(area.Width / widthPx, area.Height / heightPx);
        double w = widthPx * scale;
        double h = heightPx * scale;
        return new RectMm(area.X + ((area.Width - w) / 2.0), area.Y + ((area.Height - h) / 2.0), w, h);
    }
}
