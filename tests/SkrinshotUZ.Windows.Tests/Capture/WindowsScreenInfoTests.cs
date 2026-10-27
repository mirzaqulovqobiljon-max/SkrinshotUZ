using SkrinshotUZ.Capture;

namespace SkrinshotUZ.Windows.Tests.Capture;

/// <summary>Haqiqiy Windows API (CI'da windows-latest mashinasi).</summary>
public class WindowsScreenInfoTests
{
    [Fact]
    public void GetLayout_ReturnsAtLeastOneMonitor_WithPrimaryAndValidDpi()
    {
        var layout = WindowsScreenInfo.GetLayout();

        Assert.NotEmpty(layout.Monitors);
        Assert.Contains(layout.Monitors, m => m.IsPrimary);
        Assert.All(layout.Monitors, m =>
        {
            Assert.True(m.Dpi >= 96, $"DPI: {m.Dpi}");
            Assert.True(m.Bounds.Width > 0 && m.Bounds.Height > 0);
            Assert.True(layout.VirtualBounds.Contains(m.Bounds));
        });
    }

    [Fact]
    public void PrimaryMonitor_StartsAtOrigin()
    {
        var primary = WindowsScreenInfo.GetLayout().Monitors.Single(m => m.IsPrimary);

        Assert.Equal(System.Drawing.Point.Empty, primary.Bounds.Location);
    }

    [Fact]
    public void CaptureVirtualScreen_ReturnsCanvasSizedOpaqueImage()
    {
        // Regressiya: avval CopyFromScreen(SourceCopy | CaptureBlt) InvalidEnumArgumentException berardi.
        var layout = WindowsScreenInfo.GetLayout();

        using var screen = ScreenCapture.CaptureVirtualScreen(layout);

        Assert.Equal(layout.CanvasSize, screen.Size);
        var center = screen.GetPixel(screen.Width / 2, screen.Height / 2);
        Assert.Equal(255, center.A); // skrinshot shaffof bo'lib qolmasligi kerak
    }

    [Fact]
    public void GetForegroundWindowBounds_DoesNotThrow()
    {
        var bounds = WindowsScreenInfo.GetForegroundWindowBounds();

        if (bounds is { } b)
        {
            Assert.True(b.Width > 0 && b.Height > 0);
        }
    }
}
