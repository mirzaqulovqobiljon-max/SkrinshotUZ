using System.Drawing;
using SkrinshotUZ.Core.Capture;

namespace SkrinshotUZ.Core.Tests.Capture;

/// <summary>Ko'p monitor va DPI: koordinata o'girishlari va monitor topish.</summary>
public class ScreenLayoutTests
{
    // Odatiy maktab holati: noutbuk (150%) + chap tomonda proyektor/monitor (100%).
    private static readonly MonitorInfo Left100 = new(new Rectangle(-1920, 0, 1920, 1080), 96, false);
    private static readonly MonitorInfo Primary150 = new(new Rectangle(0, 0, 2560, 1440), 144, true);

    [Fact]
    public void SingleMonitor_CanvasEqualsScreen()
    {
        var layout = new ScreenLayout(new[] { new MonitorInfo(new Rectangle(0, 0, 1920, 1080), 96, true) });

        Assert.Equal(new Rectangle(0, 0, 1920, 1080), layout.VirtualBounds);
        Assert.Equal(new Size(1920, 1080), layout.CanvasSize);
        Assert.Equal(new Point(500, 300), layout.ToCanvas(new Point(500, 300)));
    }

    [Fact]
    public void MonitorLeftOfPrimary_NegativeScreenCoordinates_BecomePositiveOnCanvas()
    {
        var layout = new ScreenLayout(new[] { Primary150, Left100 });

        Assert.Equal(new Rectangle(-1920, 0, 4480, 1440), layout.VirtualBounds);
        Assert.Equal(new Point(0, 0), layout.ToCanvas(new Point(-1920, 0)));
        Assert.Equal(new Point(1920, 0), layout.ToCanvas(new Point(0, 0)));
        Assert.Equal(new Rectangle(1920, 0, 2560, 1440), layout.CanvasBoundsOf(Primary150));
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), layout.CanvasBoundsOf(Left100));
    }

    [Fact]
    public void MonitorAbovePrimary_NegativeY()
    {
        var top = new MonitorInfo(new Rectangle(0, -1080, 1920, 1080), 120, false);
        var primary = new MonitorInfo(new Rectangle(0, 0, 1920, 1080), 96, true);
        var layout = new ScreenLayout(new[] { primary, top });

        Assert.Equal(new Rectangle(0, -1080, 1920, 2160), layout.VirtualBounds);
        Assert.Equal(new Point(10, 1090), layout.ToCanvas(new Point(10, 10)));
        Assert.Same(top, layout.MonitorAt(new Point(10, -1)));
        Assert.Same(primary, layout.MonitorAt(new Point(10, 0)));
    }

    [Fact]
    public void MonitorAt_EdgesBelongToCorrectMonitor()
    {
        var layout = new ScreenLayout(new[] { Primary150, Left100 });

        Assert.Same(Left100, layout.MonitorAt(new Point(-1, 500)));
        Assert.Same(Primary150, layout.MonitorAt(new Point(0, 500)));
        Assert.Same(Primary150, layout.MonitorAt(new Point(2559, 1439)));
        Assert.Same(Left100, layout.MonitorAt(new Point(-1920, 0)));
    }

    [Fact]
    public void MonitorAt_PointInGapOfLShapedLayout_ReturnsNearestMonitor()
    {
        // Ikkinchi monitor pastroq: (1920, 1024) ostida bo'shliq bor.
        var big = new MonitorInfo(new Rectangle(0, 0, 1920, 1080), 96, true);
        var small = new MonitorInfo(new Rectangle(1920, 0, 1280, 1024), 96, false);
        var layout = new ScreenLayout(new[] { big, small });

        Assert.Same(small, layout.MonitorAt(new Point(2000, 1050)));  // kichik monitorga 27 px
        Assert.Same(big, layout.MonitorAt(new Point(1925, 1070)));    // kattasiga 6 px
        Assert.Same(big, layout.MonitorAt(new Point(-500, 500)));     // butunlay tashqarida
    }

    [Fact]
    public void MonitorAtCanvas_MatchesMonitorAtScreen()
    {
        var layout = new ScreenLayout(new[] { Primary150, Left100 });

        Assert.Same(Left100, layout.MonitorAtCanvas(new Point(1919, 10)));
        Assert.Same(Primary150, layout.MonitorAtCanvas(new Point(1920, 10)));
    }

    [Fact]
    public void ToScreen_IsInverseOfToCanvas()
    {
        var layout = new ScreenLayout(new[] { Primary150, Left100 });
        var r = new Rectangle(-300, 100, 800, 600); // ikki monitor chegarasini kesib o'tadi

        Assert.Equal(r, layout.ToScreen(layout.ToCanvas(r)));
        Assert.Equal(new Rectangle(1620, 100, 800, 600), layout.ToCanvas(r));
    }

    [Theory]
    [InlineData(96.0, 1.0)]
    [InlineData(120.0, 1.25)]
    [InlineData(144.0, 1.5)]
    [InlineData(192.0, 2.0)]
    public void Scale_FromDpi(double dpi, double expected)
    {
        Assert.Equal(expected, new MonitorInfo(new Rectangle(0, 0, 10, 10), dpi, true).Scale, 1e-12);
    }

    [Fact]
    public void InvalidMonitors_Throw()
    {
        Assert.Throws<ArgumentException>(() => new ScreenLayout(Array.Empty<MonitorInfo>()));
        Assert.Throws<ArgumentException>(() => new ScreenLayout(new[] { new MonitorInfo(new Rectangle(0, 0, 0, 1080), 96, true) }));
        Assert.Throws<ArgumentException>(() => new ScreenLayout(new[] { new MonitorInfo(new Rectangle(0, 0, 10, 10), 0, true) }));
    }
}
