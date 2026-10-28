using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Core.Tests.Printing;

/// <summary>To'liq chop etish rejasi: rejimlar, yo'nalish, yozuv, printer chetlari, ko'p varaq.</summary>
public class PrintPlannerTests
{
    private const double Tol = 1e-9;

    private static readonly PrintImage FullHd = new(1920, 1080, 96);

    private static PrintPlan Plan(PrintImage image, PrintOptions options, HardMargins hard = default, PrintImage? second = null) =>
        PrintPlanner.Create(image, second, options, hard);

    private static void AssertInside(RectMm inner, RectMm outer)
    {
        Assert.True(inner.IsInside(outer, 1e-6), $"{inner} sohadan chiqib ketgan: {outer}");
    }

    // ---------- A) A4 to'liq ----------

    [Fact]
    public void FullPage_Auto_WideImage_LandscapeWidthLimited()
    {
        var plan = Plan(FullHd, new PrintOptions());

        var page = Assert.Single(plan.Pages);
        Assert.Equal(PageOrientation.Landscape, page.Orientation);
        Assert.Equal(new SizeMm(297, 210), page.Size);
        var img = Assert.Single(page.Images);
        Assert.Equal(0, img.ImageIndex);
        Assert.Equal(new PixelRect(0, 0, 1920, 1080), img.Source);
        Assert.Equal(277, img.Dest.Width, Tol);
        Assert.Equal(277.0 * 1080 / 1920, img.Dest.Height, Tol);
        Assert.Equal(10, img.Dest.X, Tol);
        Assert.Equal((210 - img.Dest.Height) / 2, img.Dest.Y, Tol); // vertikal markazda
        Assert.Empty(page.Captions);
        Assert.Null(page.CutLineYMm);
        Assert.False(plan.ActualSizeOverflows);
        Assert.Equal(img.Dest.Width, plan.ImageSizeMm.Width, Tol);
    }

    [Fact]
    public void FullPage_ManualPortrait_OverridesAuto()
    {
        var plan = Plan(FullHd, new PrintOptions { Orientation = OrientationChoice.Portrait });

        var page = Assert.Single(plan.Pages);
        Assert.Equal(PageOrientation.Portrait, page.Orientation);
        Assert.Equal(190, page.Images[0].Dest.Width, Tol);
    }

    [Fact]
    public void FullPage_ManualLandscape_ForTallImage()
    {
        var plan = Plan(new PrintImage(500, 2000, 96), new PrintOptions { Orientation = OrientationChoice.Landscape });

        Assert.Equal(PageOrientation.Landscape, plan.Pages[0].Orientation);
        Assert.Equal(190, plan.Pages[0].Images[0].Dest.Height, Tol); // 210 - 2×10
    }

    [Fact]
    public void FullPage_OnePixel_FillsWidthAsSquare()
    {
        var plan = Plan(new PrintImage(1, 1, 96), new PrintOptions());

        var dest = plan.Pages[0].Images[0].Dest;
        Assert.Equal(PageOrientation.Portrait, plan.Pages[0].Orientation);
        Assert.Equal(190, dest.Width, Tol);
        Assert.Equal(190, dest.Height, Tol);
    }

    [Fact]
    public void FullPage_WithCaption_ImageShrinks_CaptionBelowAndInsideMargins()
    {
        var plan = Plan(new PrintImage(1000, 5000, 96), new PrintOptions { Caption = "  7-sinf · 28.10.2026  " });

        var page = plan.Pages[0];
        var dest = page.Images[0].Dest;
        Assert.Equal(297 - 20 - PrintPlanner.CaptionReserveMm, dest.Height, Tol);
        var caption = Assert.Single(page.Captions);
        Assert.Equal("7-sinf · 28.10.2026", caption.Text); // chetdagi bo'shliqlar olib tashlanadi
        Assert.Equal(dest.Bottom + PrintPlanner.CaptionGapMm, caption.Rect.Y, Tol);
        Assert.Equal(297 - 10, caption.Rect.Bottom, Tol); // pastki maydonga aynan tegadi
        Assert.Equal(10, caption.Rect.X, Tol);
        Assert.Equal(190, caption.Rect.Width, Tol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyCaption_ReservesNoSpace(string? caption)
    {
        var withCaption = Plan(new PrintImage(1000, 5000, 96), new PrintOptions { Caption = caption });

        Assert.Empty(withCaption.Pages[0].Captions);
        Assert.Equal(277, withCaption.Pages[0].Images[0].Dest.Height, Tol);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(15.0)]
    public void FullPage_EveryMargin_ImageInsideArea_AndAreaMatchesMargin(double margin)
    {
        var plan = Plan(FullHd, new PrintOptions { MarginMm = margin, Caption = "x" });

        var img = plan.Pages[0].Images[0];
        Assert.Equal(margin, img.Area.X, Tol);
        Assert.Equal(297 - (2 * margin), img.Area.Width, Tol);
        AssertInside(img.Dest, img.Area);
        AssertInside(plan.Pages[0].Captions[0].Rect, new RectMm(margin, margin, 297 - (2 * margin), 210 - (2 * margin)));
    }

    // ---------- Printer chetlari ----------

    [Fact]
    public void HardMargins_LargerThanUserMargin_Win()
    {
        var plan = Plan(FullHd, new PrintOptions { Orientation = OrientationChoice.Portrait, MarginMm = 0 }, new HardMargins(4.2, 6, 3, 12.5));

        var area = plan.Pages[0].Images[0].Area;
        Assert.Equal(4.2, area.X, Tol);
        Assert.Equal(6, area.Y, Tol);
        Assert.Equal(210 - 4.2 - 3, area.Width, Tol);
        Assert.Equal(297 - 6 - 12.5, area.Height, Tol);
    }

    [Fact]
    public void ForOrientation_Landscape_UsesLargerOfOppositeEdges()
    {
        var hard = new HardMargins(Left: 3, Top: 5, Right: 4, Bottom: 12);

        Assert.Equal(hard, PrintPlanner.ForOrientation(hard, PageOrientation.Portrait));
        Assert.Equal(new HardMargins(12, 4, 12, 4), PrintPlanner.ForOrientation(hard, PageOrientation.Landscape));
    }

    [Fact]
    public void Landscape_WithAsymmetricHardMargins_SafeForBothRotations()
    {
        var hard = new HardMargins(3, 5, 4, 12);
        var plan = Plan(FullHd, new PrintOptions { MarginMm = 0 }, hard);

        var dest = plan.Pages[0].Images[0].Dest;
        Assert.Equal(PageOrientation.Landscape, plan.Pages[0].Orientation);

        // Varaq 90° yoki 270° burilganda ham rasm bosilmaydigan chetga tushmasligi kerak.
        var rotated90 = new RectMm(hard.Bottom, hard.Left, 297 - hard.Bottom - hard.Top, 210 - hard.Left - hard.Right);
        var rotated270 = new RectMm(hard.Top, hard.Right, 297 - hard.Top - hard.Bottom, 210 - hard.Right - hard.Left);
        AssertInside(dest, rotated90);
        AssertInside(dest, rotated270);
    }

    // ---------- B) A4 yarmi ----------

    [Fact]
    public void HalfPage_StaysInTopHalf_Portrait()
    {
        var plan = Plan(FullHd, new PrintOptions { Mode = PrintMode.HalfPage, Orientation = OrientationChoice.Landscape });

        var page = Assert.Single(plan.Pages);
        Assert.Equal(PageOrientation.Portrait, page.Orientation); // yarim rejimda yo'nalish e'tiborga olinmaydi
        var dest = page.Images[0].Dest;
        Assert.Equal(190, dest.Width, Tol);
        Assert.Equal(190.0 * 1080 / 1920, dest.Height, Tol);
        Assert.True(dest.Bottom <= PrintLayoutCalculator.HalfA4HeightMm - 10 + Tol);
        Assert.Null(page.CutLineYMm);
    }

    [Fact]
    public void HalfPage_WithCaption_CaptionStaysInTopHalf()
    {
        var plan = Plan(new PrintImage(400, 2000, 120), new PrintOptions { Mode = PrintMode.HalfPage, Caption = "Sarlavha", MarginMm = 15 });

        var caption = plan.Pages[0].Captions[0];
        Assert.Equal(PrintLayoutCalculator.HalfA4HeightMm - 15, caption.Rect.Bottom, Tol);
    }

    [Fact]
    public void TwoPerPage_SameImage_TwiceWithCutLine()
    {
        var plan = Plan(FullHd, new PrintOptions { Mode = PrintMode.TwoPerPage, Caption = "Test" });

        var page = Assert.Single(plan.Pages);
        Assert.Equal(PrintLayoutCalculator.HalfA4HeightMm, page.CutLineYMm);
        Assert.Equal(2, page.Images.Count);
        Assert.All(page.Images, i => Assert.Equal(0, i.ImageIndex));
        var top = page.Images[0].Dest;
        var bottom = page.Images[1].Dest;
        Assert.True(top.Bottom < page.CutLineYMm);
        Assert.True(bottom.Y > page.CutLineYMm);
        Assert.Equal(top.Width, bottom.Width, Tol);
        Assert.Equal(top.Height, bottom.Height, Tol);
        Assert.Equal(top.Y + PrintLayoutCalculator.HalfA4HeightMm, bottom.Y, Tol); // ikkala yarim bir xil — qirqilganda ham bir xil
        Assert.Equal(2, page.Captions.Count);
        Assert.True(page.Captions[0].Rect.Bottom <= page.CutLineYMm + Tol);
        Assert.True(page.Captions[1].Rect.Bottom <= 297 - 10 + Tol);
    }

    [Fact]
    public void TwoPerPage_DifferentSecondImage_UsesIndexOneAndItsProportions()
    {
        var second = new PrintImage(300, 600, 144);
        var plan = Plan(FullHd, new PrintOptions { Mode = PrintMode.TwoPerPage }, second: second);

        var bottom = plan.Pages[0].Images[1];
        Assert.Equal(1, bottom.ImageIndex);
        Assert.Equal(new PixelRect(0, 0, 300, 600), bottom.Source);
        Assert.Equal(0.5, bottom.Dest.Width / bottom.Dest.Height, 9);
        Assert.Equal(128.5, bottom.Dest.Height, Tol); // 148.5 - 10 - 10
    }

    // ---------- C) Asl o'lcham ----------

    [Theory]
    [InlineData(378, 96.0)]
    [InlineData(567, 144.0)]
    [InlineData(756, 192.0)]
    public void ActualSize_SameLogicalSize_SameMm_AtEveryScaling(int px, double dpi)
    {
        var plan = Plan(new PrintImage(px, px / 2, dpi), new PrintOptions { Mode = PrintMode.ActualSize });

        // Topshiriq formulasi: kenglik_mm = piksel / (DPI/96) × 25.4 / 96
        double expected = px / (dpi / 96.0) * 25.4 / 96.0;
        var dest = plan.Pages[0].Images[0].Dest;
        Assert.Equal(expected, dest.Width, Tol);
        Assert.Equal(100.0125, dest.Width, 9);
        Assert.Equal(100, plan.ScalePercent, 9);
        Assert.False(plan.ActualSizeOverflows);
        Assert.Equal(10, dest.X, Tol);
        Assert.Equal(10, dest.Y, Tol);
    }

    [Fact]
    public void ActualSize_Dpi120()
    {
        var plan = Plan(new PrintImage(473, 100, 120), new PrintOptions { Mode = PrintMode.ActualSize });

        Assert.Equal(473 * 25.4 / 120, plan.Pages[0].Images[0].Dest.Width, Tol);
        Assert.Equal(473 * 25.4 / 120, plan.ActualSizeMm.Width, Tol);
    }

    [Fact]
    public void ActualSize_TooBig_FitToPage_OnePageScaledDown()
    {
        var plan = Plan(new PrintImage(3000, 2000, 96), new PrintOptions { Mode = PrintMode.ActualSize, Overflow = OverflowChoice.FitToPage });

        Assert.True(plan.ActualSizeOverflows);
        var page = Assert.Single(plan.Pages);
        Assert.Equal(PageOrientation.Landscape, page.Orientation);
        Assert.Equal(277, plan.ImageSizeMm.Width, Tol);
        Assert.Equal(3000 * 25.4 / 96, plan.ActualSizeMm.Width, Tol);
        Assert.True(plan.ScalePercent < 100);
        Assert.Equal(277 / (3000 * 25.4 / 96) * 100, plan.ScalePercent, 9);
    }

    [Fact]
    public void ActualSize_TooBig_Split_KeepsSize_CoversImage_NumbersCaptions()
    {
        var plan = Plan(new PrintImage(3000, 2000, 96), new PrintOptions { Mode = PrintMode.ActualSize, Overflow = OverflowChoice.SplitPages, Caption = "Xarita" });

        Assert.True(plan.ActualSizeOverflows);
        Assert.True(plan.Pages.Count > 1);
        Assert.Equal(plan.ActualSizeMm, plan.ImageSizeMm);
        Assert.Equal(100, plan.ScalePercent, 9);

        long covered = 0;
        for (int i = 0; i < plan.Pages.Count; i++)
        {
            var page = plan.Pages[i];
            var img = Assert.Single(page.Images);
            AssertInside(img.Dest, img.Area);
            Assert.Equal(img.Source.Width * 25.4 / 96, img.Dest.Width, Tol); // har bir bo'lak ham asl o'lchamda
            covered += (long)img.Source.Width * img.Source.Height;
            var caption = Assert.Single(page.Captions);
            Assert.Equal($"Xarita ({i + 1}/{plan.Pages.Count})", caption.Text);
            Assert.True(caption.Rect.Bottom <= page.Size.Height - 10 + Tol);
        }

        Assert.Equal(3000L * 2000, covered);
    }

    [Fact]
    public void ActualSize_VeryWideStrip_SplitsIntoColumns()
    {
        var plan = Plan(new PrintImage(10000, 200, 96), new PrintOptions { Mode = PrintMode.ActualSize, Overflow = OverflowChoice.SplitPages });

        // Albom: 277 mm = 1046 px (96 DPI) → 10 ustun, 1 qator.
        Assert.Equal(10, plan.Pages.Count);
        Assert.All(plan.Pages, p => Assert.Equal(PageOrientation.Landscape, p.Orientation));
        Assert.Equal(10000, plan.Pages.Sum(p => p.Images[0].Source.Width));
    }

    [Fact]
    public void ActualSize_FitsExactly_NoOverflow()
    {
        // Kitob, maydon 0: 210 mm @ 96 DPI = 793.7 px → 793 px sig'adi.
        var plan = Plan(new PrintImage(793, 100, 96), new PrintOptions { Mode = PrintMode.ActualSize, MarginMm = 0, Orientation = OrientationChoice.Portrait });

        Assert.False(plan.ActualSizeOverflows);
        Assert.Single(plan.Pages);
    }

    // ---------- Xatolar ----------

    [Fact]
    public void InvalidInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PrintPlanner.Create(null!, null, new PrintOptions(), default));
        Assert.Throws<ArgumentNullException>(() => PrintPlanner.Create(FullHd, null, null!, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan(new PrintImage(0, 10, 96), new PrintOptions()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan(new PrintImage(10, 10, 0), new PrintOptions()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan(FullHd, new PrintOptions { Mode = (PrintMode)42 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan(FullHd, new PrintOptions { Mode = PrintMode.TwoPerPage }, second: new PrintImage(0, 5, 96)));
    }

    // ---------- Yozuv matni ----------

    [Theory]
    [InlineData(null, false, null)]
    [InlineData("  ", false, null)]
    [InlineData("7-B sinf", false, "7-B sinf")]
    [InlineData(null, true, "28.10.2026")]
    [InlineData(" Algebra ", true, "Algebra · 28.10.2026")]
    public void BuildCaption_Combinations(string? title, bool withDate, string? expected)
    {
        DateTime? date = withDate ? new DateTime(2026, 10, 28, 9, 41, 0) : null;
        Assert.Equal(expected, PrintPlanner.BuildCaption(title, date));
    }

    [Theory]
    [InlineData(OrientationChoice.Auto, 100, 50, PageOrientation.Landscape)]
    [InlineData(OrientationChoice.Auto, 50, 100, PageOrientation.Portrait)]
    [InlineData(OrientationChoice.Portrait, 100, 50, PageOrientation.Portrait)]
    [InlineData(OrientationChoice.Landscape, 50, 100, PageOrientation.Landscape)]
    public void Resolve_Orientation(OrientationChoice choice, int w, int h, PageOrientation expected)
    {
        Assert.Equal(expected, PrintPlanner.Resolve(choice, new PrintImage(w, h, 96)));
    }
}
