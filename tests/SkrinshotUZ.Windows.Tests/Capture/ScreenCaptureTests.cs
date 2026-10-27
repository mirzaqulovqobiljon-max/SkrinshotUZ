using System.Drawing;
using System.Drawing.Imaging;
using SkrinshotUZ.Capture;
using SkrinshotUZ.Core.Capture;

namespace SkrinshotUZ.Windows.Tests.Capture;

/// <summary>Kesish piksel-piksel aniq bo'lishi kerak (DPI masshtabida "bir piksel siljish" xatosi bo'lmasligi uchun).</summary>
public class ScreenCaptureTests
{
    /// <summary>Har bir piksel o'z koordinatasini "kodlaydi": R = x, G = y, B = 77.</summary>
    private static Bitmap Pattern(int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bmp.SetPixel(x, y, Color.FromArgb(255, x % 256, y % 256, 77));
            }
        }

        return bmp;
    }

    [Fact]
    public void Crop_ReturnsExactPixels_NoScalingNoShift()
    {
        using var source = Pattern(200, 150);
        var rect = new Rectangle(37, 21, 100, 60);

        using var cropped = ScreenCapture.Crop(source, rect);

        Assert.Equal(new Size(100, 60), cropped.Size);
        Assert.Equal(PixelFormat.Format24bppRgb, cropped.PixelFormat);
        foreach (var (x, y) in new[] { (0, 0), (99, 0), (0, 59), (99, 59), (50, 30) })
        {
            var c = cropped.GetPixel(x, y);
            Assert.Equal(rect.X + x, c.R);
            Assert.Equal(rect.Y + y, c.G);
            Assert.Equal(77, c.B);
        }
    }

    [Fact]
    public void Crop_OnePixel_Works()
    {
        using var source = Pattern(10, 10);
        using var cropped = ScreenCapture.Crop(source, new Rectangle(9, 9, 1, 1));

        Assert.Equal(new Size(1, 1), cropped.Size);
        Assert.Equal(9, cropped.GetPixel(0, 0).R);
    }

    [Fact]
    public void Crop_WholeImage_Works()
    {
        using var source = Pattern(64, 48);
        using var cropped = ScreenCapture.Crop(source, new Rectangle(0, 0, 64, 48));

        Assert.Equal(63, cropped.GetPixel(63, 47).R);
        Assert.Equal(47, cropped.GetPixel(63, 47).G);
    }

    [Theory]
    [InlineData(-1, 0, 10, 10)]
    [InlineData(0, 0, 0, 10)]
    [InlineData(5, 5, 10, 10)]
    public void Crop_OutsideOrEmpty_Throws(int x, int y, int w, int h)
    {
        using var source = Pattern(10, 10);

        Assert.Throws<ArgumentException>(() => ScreenCapture.Crop(source, new Rectangle(x, y, w, h)));
    }

    [Fact]
    public void SelectionOnNegativeMonitor_CropsCorrectCanvasPixels()
    {
        // Chap monitor manfiy koordinatada: ekran nuqtasi (-1900, 10) kanvasda (20, 10) bo'lishi kerak.
        var layout = new ScreenLayout(new[]
        {
            new MonitorInfo(new Rectangle(0, 0, 100, 80), 144, true),
            new MonitorInfo(new Rectangle(-1920, 0, 1920, 80), 96, false),
        });
        using var canvas = Pattern(layout.CanvasSize.Width, layout.CanvasSize.Height);
        var canvasRect = layout.ToCanvas(new Rectangle(-1900, 10, 30, 20));

        using var cropped = ScreenCapture.Crop(canvas, canvasRect);

        Assert.Equal(20, cropped.GetPixel(0, 0).R);
        Assert.Equal(10, cropped.GetPixel(0, 0).G);
    }
}
