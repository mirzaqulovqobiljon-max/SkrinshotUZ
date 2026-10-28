using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Core.Tests.Printing;

/// <summary>Chop etish oynasidagi o'lcham matnlari va fayl DPI'si.</summary>
public class PrintTextTests
{
    [Theory]
    [InlineData(190.0, "190,0")]
    [InlineData(106.875, "106,9")]
    [InlineData(0.04, "0,0")]
    [InlineData(100.0125, "100,0")]
    public void Mm_UsesDecimalComma_OneDigit(double mm, string expected)
    {
        Assert.Equal(expected, PrintText.Mm(mm));
    }

    [Fact]
    public void Size_Format()
    {
        Assert.Equal("190,0 × 106,9 mm", PrintText.Size(new SizeMm(190, 106.875)));
    }

    [Fact]
    public void SizeInfo_Fitted_ShowsPercent()
    {
        var plan = PrintPlanner.Create(new PrintImage(3000, 2000, 96), null, new PrintOptions { Mode = PrintMode.ActualSize }, default);

        // 277 / 793,75 = 34,9%
        Assert.Equal("Rasm qog'ozda: 277,0 × 184,7 mm · asl o'lchamning 35%", PrintText.SizeInfo(plan));
    }

    [Fact]
    public void SizeInfo_ActualSize_SaysActual()
    {
        var plan = PrintPlanner.Create(new PrintImage(378, 189, 96), null, new PrintOptions { Mode = PrintMode.ActualSize }, default);

        Assert.Equal("Rasm qog'ozda: 100,0 × 50,0 mm · asl o'lchamda", PrintText.SizeInfo(plan));
    }

    [Fact]
    public void SizeInfo_EnlargedSmallImage_PercentAbove100()
    {
        var plan = PrintPlanner.Create(new PrintImage(100, 100, 96), null, new PrintOptions(), default);

        // 190 mm / 26,46 mm = 718%
        Assert.Equal("Rasm qog'ozda: 190,0 × 190,0 mm · asl o'lchamning 718%", PrintText.SizeInfo(plan));
    }

    [Fact]
    public void PagesInfo_OrientationPagesCopies()
    {
        var landscape = PrintPlanner.Create(new PrintImage(1920, 1080, 96), null, new PrintOptions(), default);
        var split = PrintPlanner.Create(new PrintImage(400, 3000, 96), null, new PrintOptions { Mode = PrintMode.ActualSize, Overflow = OverflowChoice.SplitPages }, default);

        Assert.Equal("A4 albom · 1 varaq", PrintText.PagesInfo(landscape, 1));
        Assert.Equal("A4 albom · 1 varaq × 3 nusxa", PrintText.PagesInfo(landscape, 3));
        Assert.Equal("A4 kitob · " + split.Pages.Count + " varaq", PrintText.PagesInfo(split, 1));
        Assert.True(split.Pages.Count >= 3);
    }

    [Theory]
    [InlineData(96.0, 96.0)]
    [InlineData(120.0, 120.0)]
    [InlineData(144.0, 144.0)]
    [InlineData(192.0, 192.0)]
    [InlineData(48.0, 48.0)]
    [InlineData(960.0, 960.0)]
    [InlineData(0.0, 96.0)]
    [InlineData(-1.0, 96.0)]
    [InlineData(47.9, 96.0)]
    [InlineData(5000.0, 96.0)]
    [InlineData(double.NaN, 96.0)]
    public void SanitizeImageDpi(double input, double expected)
    {
        Assert.Equal(expected, Units.SanitizeImageDpi(input));
    }
}
