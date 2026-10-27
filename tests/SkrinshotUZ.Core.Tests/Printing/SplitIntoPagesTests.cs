using SkrinshotUZ.Core.Printing;

namespace SkrinshotUZ.Core.Tests.Printing;

/// <summary>C) "Bir nechta varaqqa bo'lish".</summary>
public class SplitIntoPagesTests
{
    private const double Tol = 1e-9;

    [Fact]
    public void SmallImage_IsSinglePage_WithWholeSource()
    {
        var tiles = PrintLayoutCalculator.SplitIntoPages(400, 300, 96, PageOrientation.Portrait, PrintMargins.Default);

        var t = Assert.Single(tiles);
        Assert.Equal(1, t.PageNumber);
        Assert.Equal(new PixelRect(0, 0, 400, 300), t.Layout.Source);
        Assert.True(t.Layout.FitsOnPage);
    }

    [Fact]
    public void FullHdAt96Dpi_Landscape_IsTwoByTwoPages()
    {
        // Soha 277×190 mm → 1046×718 px (96 DPI) → 1920×1080 uchun 2 ustun × 2 qator.
        var tiles = PrintLayoutCalculator.SplitIntoPages(1920, 1080, 96, PageOrientation.Landscape, PrintMargins.Default);

        Assert.Equal(4, tiles.Count);
        Assert.Equal(new PixelRect(0, 0, 1046, 718), tiles[0].Layout.Source);
        Assert.Equal(new PixelRect(1046, 0, 874, 718), tiles[1].Layout.Source);
        Assert.Equal(new PixelRect(0, 718, 1046, 362), tiles[2].Layout.Source);
        Assert.Equal(new PixelRect(1046, 718, 874, 362), tiles[3].Layout.Source);
        Assert.Equal(874 * 25.4 / 96, tiles[1].Layout.ImageRect.Width, Tol);
    }

    [Fact]
    public void ExactMultipleOfPage_DoesNotCreateExtraEmptyPage()
    {
        // 127 DPI: soha kengligi 190 mm = aynan 950 px. 1900 px → aynan 2 ustun (3 emas).
        var tiles = PrintLayoutCalculator.SplitIntoPages(1900, 100, 127, PageOrientation.Portrait, PrintMargins.Default);

        Assert.Equal(2, tiles.Count);
        Assert.Equal(950, tiles[0].Layout.Source.Width);
        Assert.Equal(950, tiles[1].Layout.Source.Width);
    }

    [Fact]
    public void FloatingPointRounding_DoesNotLoseAPixelPerPage()
    {
        // 139.7 DPI, 15 mm maydon: soha 180 mm = aynan 990 px, lekin double hisobida 989.9999999999999 chiqadi.
        // Epsilonsiz 989 px olinib, 1980 px uchun 3 ta varaq (biri deyarli bo'sh) chiqib qolardi.
        var tiles = PrintLayoutCalculator.SplitIntoPages(1980, 100, 139.7, PageOrientation.Portrait, new PrintMargins(15));

        Assert.Equal(2, tiles.Count);
        Assert.Equal(990, tiles[0].Layout.Source.Width);
        Assert.True(tiles[1].Layout.FitsOnPage);
    }

    [Theory]
    [InlineData(1, 1, 96.0, PageOrientation.Portrait, 10.0, 0.0)]
    [InlineData(10000, 200, 96.0, PageOrientation.Landscape, 10.0, 0.0)]
    [InlineData(200, 10000, 120.0, PageOrientation.Portrait, 10.0, 0.0)]
    [InlineData(3000, 3000, 144.0, PageOrientation.Portrait, 5.0, 0.0)]
    [InlineData(1920, 1080, 192.0, PageOrientation.Landscape, 0.0, 4.2)]
    [InlineData(2777, 1955, 96.0, PageOrientation.Portrait, 15.0, 6.35)]
    public void Tiles_CoverEveryPixelExactlyOnce_AndEachFitsOnPage(int w, int h, double dpi, PageOrientation o, double margin, double hard)
    {
        var tiles = PrintLayoutCalculator.SplitIntoPages(w, h, dpi, o, new PrintMargins(margin, HardMargins.Symmetric(hard, hard)));

        // Har bir piksel aynan bir marta qamrab olinadi.
        var covered = new int[w * h];
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            var s = t.Layout.Source;

            Assert.Equal(i + 1, t.PageNumber);
            Assert.True(s.Width > 0 && s.Height > 0);
            Assert.True(s.X >= 0 && s.Y >= 0 && s.X + s.Width <= w && s.Y + s.Height <= h, $"Bo'lak rasm chegarasidan chiqdi: {s}");
            Assert.True(t.Layout.FitsOnPage, $"{t.PageNumber}-varaq sohaga sig'madi.");
            Assert.True(t.Layout.ImageRect.IsInside(t.Layout.PrintableArea));

            // Masshtab o'zgarmaydi: 1 px = 25.4/dpi mm.
            Assert.Equal(25.4 / dpi, t.Layout.MmPerPixel, 1e-9);

            for (int y = s.Y; y < s.Y + s.Height; y++)
            {
                for (int x = s.X; x < s.X + s.Width; x++)
                {
                    covered[(y * w) + x]++;
                }
            }
        }

        Assert.DoesNotContain(covered, c => c != 1);
    }

    [Fact]
    public void Tiles_AreInRowMajorOrder()
    {
        var tiles = PrintLayoutCalculator.SplitIntoPages(3000, 3000, 96, PageOrientation.Portrait, PrintMargins.Default);
        int columns = tiles.Max(t => t.Column) + 1;

        for (int i = 0; i < tiles.Count; i++)
        {
            Assert.Equal(i / columns, tiles[i].Row);
            Assert.Equal(i % columns, tiles[i].Column);
        }
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintLayoutCalculator.SplitIntoPages(0, 10, 96, PageOrientation.Portrait, PrintMargins.Default));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintLayoutCalculator.SplitIntoPages(10, 10, 0, PageOrientation.Portrait, PrintMargins.Default));
        Assert.Throws<ArgumentNullException>(() => PrintLayoutCalculator.SplitIntoPages(10, 10, 96, PageOrientation.Portrait, null!));
    }
}
