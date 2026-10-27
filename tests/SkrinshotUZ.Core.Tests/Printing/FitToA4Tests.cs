using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Core.Tests.Printing;

/// <summary>A) A4 to'liq varaq rejimi va avtomatik yo'nalish.</summary>
public class FitToA4Tests
{
    private const double Tol = 1e-9;

    [Theory]
    [InlineData(1920, 1080, PageOrientation.Landscape)]
    [InlineData(1080, 1920, PageOrientation.Portrait)]
    [InlineData(500, 500, PageOrientation.Portrait)] // kvadrat → kitob
    [InlineData(1, 1, PageOrientation.Portrait)]
    [InlineData(10000, 200, PageOrientation.Landscape)]
    [InlineData(200, 10000, PageOrientation.Portrait)]
    [InlineData(2, 1, PageOrientation.Landscape)]
    public void AutoOrientation_WideIsLandscape_TallOrSquareIsPortrait(int w, int h, PageOrientation expected)
    {
        Assert.Equal(expected, PrintLayoutCalculator.AutoOrientation(w, h));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void AutoOrientation_InvalidSize_Throws(int w, int h)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintLayoutCalculator.AutoOrientation(w, h));
    }

    [Fact]
    public void A4_HasCorrectDimensionsForBothOrientations()
    {
        Assert.Equal(new SizeMm(210, 297), PrintLayoutCalculator.A4(PageOrientation.Portrait));
        Assert.Equal(new SizeMm(297, 210), PrintLayoutCalculator.A4(PageOrientation.Landscape));
    }

    [Fact]
    public void FitToA4_FullHdLandscape_WidthLimitedAndCentered()
    {
        var r = PrintLayoutCalculator.FitToA4(1920, 1080, PageOrientation.Landscape, PrintMargins.Default);

        // Chop etiladigan soha: 297−20 = 277 × 210−20 = 190.
        Assert.Equal(new RectMm(10, 10, 277, 190), r.PrintableArea);
        Assert.Equal(10, r.ImageRect.X, Tol);
        Assert.Equal(277, r.ImageRect.Width, Tol);
        Assert.Equal(277.0 * 1080 / 1920, r.ImageRect.Height, Tol); // 155.8125
        Assert.Equal(10 + ((190 - 155.8125) / 2), r.ImageRect.Y, Tol);
        Assert.True(r.FitsOnPage);
    }

    [Fact]
    public void FitToA4_FullHdPortraitImage_HeightLimitedAndCentered()
    {
        var r = PrintLayoutCalculator.FitToA4(1080, 1920, PageOrientation.Portrait, PrintMargins.Default);

        Assert.Equal(277, r.ImageRect.Height, Tol);
        Assert.Equal(155.8125, r.ImageRect.Width, Tol);
        Assert.Equal(10 + ((190 - 155.8125) / 2), r.ImageRect.X, Tol);
        Assert.Equal(10, r.ImageRect.Y, Tol);
    }

    [Fact]
    public void FitToA4_OnePixel_ScaledUpToSquareAndCentered()
    {
        var r = PrintLayoutCalculator.FitToA4(1, 1, PageOrientation.Portrait, PrintMargins.Default);

        Assert.Equal(190, r.ImageRect.Width, Tol);
        Assert.Equal(190, r.ImageRect.Height, Tol);
        Assert.Equal(10, r.ImageRect.X, Tol);
        Assert.Equal(10 + ((277 - 190) / 2.0), r.ImageRect.Y, Tol);
        Assert.Equal(190, r.MmPerPixel, Tol);
    }

    [Fact]
    public void FitToA4_VeryWide10000x200_Landscape()
    {
        var r = PrintLayoutCalculator.FitToA4(10000, 200, PageOrientation.Landscape, PrintMargins.Default);

        Assert.Equal(277, r.ImageRect.Width, Tol);
        Assert.Equal(5.54, r.ImageRect.Height, Tol);
    }

    [Fact]
    public void FitToA4_VeryTall200x10000_Portrait()
    {
        var r = PrintLayoutCalculator.FitToA4(200, 10000, PageOrientation.Portrait, PrintMargins.Default);

        Assert.Equal(277, r.ImageRect.Height, Tol);
        Assert.Equal(5.54, r.ImageRect.Width, Tol);
    }

    [Theory]
    [InlineData(1, 1, PageOrientation.Portrait)]
    [InlineData(1, 1, PageOrientation.Landscape)]
    [InlineData(3, 7, PageOrientation.Portrait)]
    [InlineData(1920, 1080, PageOrientation.Portrait)] // qo'lda noqulay yo'nalish tanlangan
    [InlineData(1366, 768, PageOrientation.Landscape)]
    [InlineData(10000, 200, PageOrientation.Portrait)]
    [InlineData(200, 10000, PageOrientation.Landscape)]
    [InlineData(4096, 4095, PageOrientation.Landscape)]
    public void FitToA4_AnySize_KeepsAspectRatio_IsMaximal_InsideAndCentered(int w, int h, PageOrientation o)
    {
        var r = PrintLayoutCalculator.FitToA4(w, h, o, PrintMargins.Default);
        var area = r.PrintableArea;
        var img = r.ImageRect;

        // Proporsiya saqlanadi.
        Assert.Equal((double)w / h, img.Width / img.Height, 1e-9);

        // Sohadan chiqmaydi.
        Assert.True(img.IsInside(area), $"Rasm sohadan chiqib ketdi: {img} / {area}");

        // Maksimal: kamida bitta tomon sohaga tegadi.
        bool touches = Math.Abs(img.Width - area.Width) < 1e-9 || Math.Abs(img.Height - area.Height) < 1e-9;
        Assert.True(touches, "Rasm maksimal kattalikda emas.");

        // Markazda.
        Assert.Equal(img.X - area.X, area.Right - img.Right, 1e-9);
        Assert.Equal(img.Y - area.Y, area.Bottom - img.Bottom, 1e-9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(15.0)]
    public void FitToA4_AllowedMargins_SquareImageWidthIsPageMinusTwoMargins(double margin)
    {
        var r = PrintLayoutCalculator.FitToA4(1000, 1000, PageOrientation.Portrait, new PrintMargins(margin));

        Assert.Equal(210 - (2 * margin), r.ImageRect.Width, Tol);
        Assert.Equal(margin, r.ImageRect.X, Tol);
    }

    [Fact]
    public void FitToA4_HardMarginLargerThanUserMargin_HardMarginWins()
    {
        var m = new PrintMargins(0, HardMargins.Symmetric(4.23, 4.23));
        var r = PrintLayoutCalculator.FitToA4(1000, 1000, PageOrientation.Portrait, m);

        Assert.Equal(4.23, r.PrintableArea.X, Tol);
        Assert.Equal(210 - (2 * 4.23), r.PrintableArea.Width, Tol);
    }

    [Fact]
    public void FitToA4_HardMarginSmallerThanUserMargin_UserMarginWins()
    {
        var m = new PrintMargins(10, HardMargins.Symmetric(3, 3));
        var r = PrintLayoutCalculator.FitToA4(1000, 1000, PageOrientation.Portrait, m);

        Assert.Equal(new RectMm(10, 10, 190, 277), r.PrintableArea);
    }

    [Fact]
    public void FitToA4_AsymmetricHardMargins_EachSideUsesMaximum()
    {
        var m = new PrintMargins(5, new HardMargins(Left: 3, Top: 4, Right: 6, Bottom: 8));
        var r = PrintLayoutCalculator.FitToA4(100, 100, PageOrientation.Portrait, m);

        Assert.Equal(5, r.PrintableArea.X, Tol);
        Assert.Equal(5, r.PrintableArea.Y, Tol);
        Assert.Equal(210 - 5 - 6, r.PrintableArea.Width, Tol);
        Assert.Equal(297 - 5 - 8, r.PrintableArea.Height, Tol);
    }

    [Fact]
    public void ToDeviceCoordinates_SubtractsHardMarginOrigin()
    {
        var hard = HardMargins.Symmetric(4, 3.5);
        var r = PrintLayoutCalculator.FitToA4(1000, 1000, PageOrientation.Portrait, new PrintMargins(10, hard));
        var dev = r.ToDeviceCoordinates(hard);

        Assert.Equal(r.ImageRect.X - 4, dev.X, Tol);
        Assert.Equal(r.ImageRect.Y - 3.5, dev.Y, Tol);
        Assert.Equal(r.ImageRect.Width, dev.Width, Tol);
        Assert.Equal(r.ImageRect.Height, dev.Height, Tol);
    }

    [Fact]
    public void FitToA4_MarginsTooLarge_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PrintLayoutCalculator.FitToA4(100, 100, PageOrientation.Portrait, new PrintMargins(105)));
    }

    [Fact]
    public void PrintMargins_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintMargins(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintMargins(5, new HardMargins(0, -1, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintMargins(double.NaN));
    }

    [Fact]
    public void FitToA4_NullMargins_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PrintLayoutCalculator.FitToA4(100, 100, PageOrientation.Portrait, null!));
    }
}
