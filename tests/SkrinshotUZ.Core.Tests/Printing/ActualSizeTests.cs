using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Core.Tests.Printing;

/// <summary>C) Asl o'lchamda chop etish va birliklar.</summary>
public class ActualSizeTests
{
    private const double Tol = 1e-9;

    [Theory]
    [InlineData(96.0)]
    [InlineData(120.0)]
    [InlineData(144.0)]
    [InlineData(192.0)]
    public void OneInchOfPixels_IsExactly25_4Mm_AtAnyDpi(double dpi)
    {
        int px = (int)dpi;
        var s = PrintLayoutCalculator.ActualSizeMm(px, px, dpi);

        Assert.Equal(25.4, s.Width, Tol);
        Assert.Equal(25.4, s.Height, Tol);
    }

    [Theory]
    [InlineData(1, 96.0)]
    [InlineData(1920, 96.0)]
    [InlineData(1920, 120.0)]
    [InlineData(1920, 144.0)]
    [InlineData(1920, 192.0)]
    [InlineData(10000, 96.0)]
    [InlineData(333, 141.21)] // haqiqiy fizik DPI (15.6" Full HD) ham kasr bo'lishi mumkin
    public void ActualSizeMm_MatchesFormulaFromSpecification(int px, double dpi)
    {
        // Vazifadagi formula: kenglik_mm = piksel / (monitorDPI/96) × 25.4 / 96
        double expected = px / (dpi / 96.0) * 25.4 / 96.0;

        Assert.Equal(expected, PrintLayoutCalculator.ActualSizeMm(px, 1, dpi).Width, 1e-9);
    }

    [Fact]
    public void ActualSizeMm_KnownValues()
    {
        Assert.Equal(508, PrintLayoutCalculator.ActualSizeMm(1920, 1080, 96).Width, Tol);
        Assert.Equal(285.75, PrintLayoutCalculator.ActualSizeMm(1920, 1080, 96).Height, Tol);
        Assert.Equal(338.6666666666667, PrintLayoutCalculator.ActualSizeMm(1920, 1080, 144).Width, 1e-9);
        Assert.Equal(0.26458333333333334, PrintLayoutCalculator.ActualSizeMm(1, 1, 96).Width, 1e-12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-96.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ActualSizeMm_InvalidDpi_Throws(double dpi)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintLayoutCalculator.ActualSizeMm(100, 100, dpi));
    }

    [Fact]
    public void ActualSize_SmallImage_FitsAtTopLeftOfPrintableArea()
    {
        var r = PrintLayoutCalculator.ActualSize(400, 300, 96, PageOrientation.Portrait, PrintMargins.Default);

        Assert.True(r.FitsOnPage);
        Assert.Equal(10, r.ImageRect.X, Tol);
        Assert.Equal(10, r.ImageRect.Y, Tol);
        Assert.Equal(400 * 25.4 / 96, r.ImageRect.Width, Tol);
        Assert.Equal(300 * 25.4 / 96, r.ImageRect.Height, Tol);
    }

    [Fact]
    public void ActualSize_FullHdAt96Dpi_DoesNotFit()
    {
        var r = PrintLayoutCalculator.ActualSize(1920, 1080, 96, PageOrientation.Landscape, PrintMargins.Default);

        Assert.False(r.FitsOnPage);
        Assert.Equal(508, r.ImageRect.Width, Tol); // masshtab o'zgarmaydi — UI ogohlantiradi
    }

    [Fact]
    public void ActualSize_ExactlyFillsWidth_StillFits_OnePixelMoreDoesNot()
    {
        // 127 DPI da 1 mm = 5 px, kitob yo'nalishida soha kengligi 190 mm = 950 px.
        Assert.True(PrintLayoutCalculator.ActualSize(950, 100, 127, PageOrientation.Portrait, PrintMargins.Default).FitsOnPage);
        Assert.False(PrintLayoutCalculator.ActualSize(951, 100, 127, PageOrientation.Portrait, PrintMargins.Default).FitsOnPage);
    }

    [Fact]
    public void ActualSize_RespectsHardMargins()
    {
        var m = new PrintMargins(0, HardMargins.Symmetric(4.5, 6));
        var r = PrintLayoutCalculator.ActualSize(100, 100, 96, PageOrientation.Portrait, m);

        Assert.Equal(4.5, r.ImageRect.X, Tol);
        Assert.Equal(6, r.ImageRect.Y, Tol);
    }

    [Fact]
    public void Units_HundredthsOfInch_RoundTrip()
    {
        Assert.Equal(100, Units.MmToHundredthsInch(25.4), Tol);
        Assert.Equal(25.4, Units.HundredthsInchToMm(100), Tol);
        Assert.Equal(210, Units.HundredthsInchToMm(Units.MmToHundredthsInch(210)), Tol);
        Assert.Equal(300, Units.MmToPixels(25.4, 300), Tol);
    }
}
