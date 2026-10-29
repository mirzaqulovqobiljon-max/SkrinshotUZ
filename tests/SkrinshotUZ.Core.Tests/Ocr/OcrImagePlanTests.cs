using SkrinshotUZ.Core.Ocr;

namespace SkrinshotUZ.Core.Tests.Ocr;

/// <summary>OCR'dan oldin rasmni kattalashtirish: monitor DPI'siga qarab va xotira chegarasi ichida.</summary>
public class OcrImagePlanTests
{
    [Theory]
    [InlineData(96.0, 3)]    // 100% — harflar mayda, 3 marta
    [InlineData(120.0, 2)]   // 125%
    [InlineData(144.0, 2)]   // 150%
    [InlineData(192.0, 2)]   // 200% (1.5 → 2)
    [InlineData(288.0, 1)]
    [InlineData(600.0, 1)]
    public void Scale_DependsOnMonitorDpi(double dpi, int expected)
    {
        Assert.Equal(expected, OcrImagePlan.ChooseScale(800, 600, dpi));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-96.0)]
    [InlineData(10.0)]
    [InlineData(5000.0)]
    [InlineData(double.NaN)]
    public void Scale_InvalidDpi_TreatedAs96(double dpi)
    {
        Assert.Equal(3, OcrImagePlan.ChooseScale(800, 600, dpi));
    }

    [Fact]
    public void Scale_TinyImage_1x1_IsAllowed()
    {
        Assert.Equal(3, OcrImagePlan.ChooseScale(1, 1, 96));
    }

    [Fact]
    public void Scale_FullHdScreen_ReducedToStayUnderMemoryLimit()
    {
        // 1920×1080 ×3 = 18.7 MP (chegara ichida), lekin 5760 px < 8000 — 3 qoladi.
        Assert.Equal(3, OcrImagePlan.ChooseScale(1920, 1080, 96));

        // 2560×1440 ×3 = 33 MP > 24 MP → 2 (22 MP).
        Assert.Equal(2, OcrImagePlan.ChooseScale(2560, 1440, 96));
    }

    [Fact]
    public void Scale_VeryWideImage_10000x200_NotEnlarged()
    {
        // Har qanday kattalashtirish 8000 px tomon chegarasidan oshadi.
        Assert.Equal(1, OcrImagePlan.ChooseScale(10000, 200, 96));
    }

    [Fact]
    public void Scale_VeryTallImage_LimitedBySide()
    {
        Assert.Equal(2, OcrImagePlan.ChooseScale(300, 3500, 96));   // 3×3500 = 10500 > 8000; 2× = 7000
        Assert.Equal(1, OcrImagePlan.ChooseScale(300, 5000, 96));
    }

    [Fact]
    public void Scale_ResultAlwaysWithinLimits()
    {
        foreach (int w in new[] { 1, 37, 640, 1366, 1920, 2560, 3840, 7680, 12000 })
        {
            foreach (int h in new[] { 1, 23, 480, 768, 1080, 1440, 2160, 4320, 9000 })
            {
                foreach (double dpi in new[] { 96.0, 120.0, 144.0, 192.0 })
                {
                    int s = OcrImagePlan.ChooseScale(w, h, dpi);
                    Assert.InRange(s, 1, OcrImagePlan.MaxScale);
                    if (s > 1)
                    {
                        Assert.True((long)w * s <= OcrImagePlan.MaxSide && (long)h * s <= OcrImagePlan.MaxSide, $"{w}x{h}@{dpi} → {s}");
                        Assert.True((long)w * s * h * s <= OcrImagePlan.MaxPixels, $"{w}x{h}@{dpi} → {s}");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void Scale_InvalidSize_Throws(int w, int h)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => OcrImagePlan.ChooseScale(w, h, 96));
    }

    [Theory]
    [InlineData(96.0, 3, 288)]
    [InlineData(144.0, 2, 288)]
    [InlineData(96.0, 1, 96)]
    [InlineData(0.0, 3, 288)]       // noma'lum DPI → 96
    [InlineData(48.0, 1, 70)]       // Tesseract minimal 70
    public void SourceResolution_IsScaledDpiWithinTesseractRange(double dpi, int scale, int expected)
    {
        Assert.Equal(expected, OcrImagePlan.SourceResolution(dpi, scale));
    }

    [Fact]
    public void Languages_UzbekLatinCyrillicRussianEnglish()
    {
        Assert.Equal("uzb+uzb_cyrl+rus+eng", OcrLanguages.All);
        Assert.Equal(new[] { "uzb.traineddata", "uzb_cyrl.traineddata", "rus.traineddata", "eng.traineddata" }, OcrLanguages.ModelFiles);
    }
}
