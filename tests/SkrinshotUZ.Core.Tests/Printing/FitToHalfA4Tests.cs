using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Core.Tests.Printing;

/// <summary>B) A4 yarmi va "Bir varaqqa 2 ta" rejimi.</summary>
public class FitToHalfA4Tests
{
    private const double Tol = 1e-9;

    [Fact]
    public void TopSlot_FullHd_WidthLimitedInsideTopHalf()
    {
        var r = PrintLayoutCalculator.FitToHalfA4(1920, 1080, HalfPageSlot.Top, PrintMargins.Default);

        Assert.Equal(PageOrientation.Portrait, r.Orientation);
        Assert.Equal(new RectMm(10, 10, 190, 128.5), r.PrintableArea);
        Assert.Equal(190, r.ImageRect.Width, Tol);
        Assert.Equal(106.875, r.ImageRect.Height, Tol);
        Assert.Equal(10 + ((128.5 - 106.875) / 2), r.ImageRect.Y, Tol);
    }

    [Fact]
    public void BottomSlot_FullHd_IsShiftedByHalfPage()
    {
        var top = PrintLayoutCalculator.FitToHalfA4(1920, 1080, HalfPageSlot.Top, PrintMargins.Default);
        var bottom = PrintLayoutCalculator.FitToHalfA4(1920, 1080, HalfPageSlot.Bottom, PrintMargins.Default);

        Assert.Equal(158.5, bottom.PrintableArea.Y, Tol);
        Assert.Equal(top.ImageRect.Y + 148.5, bottom.ImageRect.Y, Tol);
        Assert.Equal(top.ImageRect.Width, bottom.ImageRect.Width, Tol);
        Assert.True(bottom.ImageRect.Bottom <= 297 - 10 + Tol);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1920, 1080)]
    [InlineData(10000, 200)]
    [InlineData(200, 10000)]
    [InlineData(800, 600)]
    public void BothSlots_NeverCrossCutLine_AndKeepAspect(int w, int h)
    {
        var top = PrintLayoutCalculator.FitToHalfA4(w, h, HalfPageSlot.Top, PrintMargins.Default);
        var bottom = PrintLayoutCalculator.FitToHalfA4(w, h, HalfPageSlot.Bottom, PrintMargins.Default);

        Assert.True(top.ImageRect.Bottom <= PrintLayoutCalculator.HalfA4HeightMm + Tol, "Yuqori rasm kesish chizig'idan oshdi.");
        Assert.True(bottom.ImageRect.Y >= PrintLayoutCalculator.HalfA4HeightMm - Tol, "Pastki rasm kesish chizig'idan yuqorida.");
        Assert.True(top.ImageRect.IsInside(top.PrintableArea));
        Assert.True(bottom.ImageRect.IsInside(bottom.PrintableArea));
        Assert.Equal((double)w / h, top.ImageRect.Width / top.ImageRect.Height, 1e-9);
    }

    [Fact]
    public void VeryTall_IsLimitedByHalfPageHeight()
    {
        var r = PrintLayoutCalculator.FitToHalfA4(200, 10000, HalfPageSlot.Top, PrintMargins.Default);

        Assert.Equal(128.5, r.ImageRect.Height, Tol);
        Assert.Equal(2.57, r.ImageRect.Width, Tol);
    }

    [Fact]
    public void ZeroMargin_TopSlotIsWholeHalfPage()
    {
        var r = PrintLayoutCalculator.FitToHalfA4(210, 148, HalfPageSlot.Top, new PrintMargins(0));

        Assert.Equal(new RectMm(0, 0, 210, 148.5), r.PrintableArea);
    }

    [Fact]
    public void HardMargins_ApplyOnlyAtPaperEdges_NotAtCutLine()
    {
        var m = new PrintMargins(5, new HardMargins(Left: 6, Top: 7, Right: 6, Bottom: 12));
        var top = PrintLayoutCalculator.FitToHalfA4(100, 100, HalfPageSlot.Top, m);
        var bottom = PrintLayoutCalculator.FitToHalfA4(100, 100, HalfPageSlot.Bottom, m);

        // Yuqori yarim: tepada printer cheti (7), kesish chizig'i tomonida foydalanuvchi maydoni (5).
        Assert.Equal(7, top.PrintableArea.Y, Tol);
        Assert.Equal(148.5 - 5, top.PrintableArea.Bottom, Tol);

        // Pastki yarim: kesish chizig'idan 5, pastda printer cheti 12.
        Assert.Equal(148.5 + 5, bottom.PrintableArea.Y, Tol);
        Assert.Equal(297 - 12, bottom.PrintableArea.Bottom, Tol);

        Assert.Equal(6, top.PrintableArea.X, Tol);
        Assert.Equal(210 - 12, top.PrintableArea.Width, Tol);
    }

    [Fact]
    public void TwoPerPage_HasCutLineInMiddle_AndAcceptsDifferentImages()
    {
        var two = PrintLayoutCalculator.TwoPerPage(1920, 1080, 600, 800, PrintMargins.Default);

        Assert.Equal(148.5, two.CutLineYMm, Tol);
        Assert.True(two.Top.ImageRect.Bottom <= two.CutLineYMm);
        Assert.True(two.Bottom.ImageRect.Y >= two.CutLineYMm);
        Assert.Equal(1920.0 / 1080, two.Top.ImageRect.Width / two.Top.ImageRect.Height, 1e-9);
        Assert.Equal(600.0 / 800, two.Bottom.ImageRect.Width / two.Bottom.ImageRect.Height, 1e-9);
    }

    [Fact]
    public void HalfA4_MarginTooLarge_Throws()
    {
        // 148.5 − 2×75 < 0
        Assert.Throws<ArgumentException>(() =>
            PrintLayoutCalculator.FitToHalfA4(100, 100, HalfPageSlot.Top, new PrintMargins(75)));
    }
}
