using System.Drawing;
using System.Globalization;
using SkrinshotUZ.Core.Capture;

namespace SkrinshotUZ.Core.Tests.Capture;

public class OverlayLayoutTests
{
    private static readonly Rectangle Monitor = new(0, 0, 1920, 1080);
    private static readonly Size Label = new(90, 24);

    [Fact]
    public void SizeLabel_AboveSelection_LeftAligned()
    {
        Assert.Equal(new Point(500, 400 - 6 - 24), OverlayLayout.PlaceSizeLabel(new Rectangle(500, 400, 300, 200), Label, Monitor, 6));
    }

    [Fact]
    public void SizeLabel_NoRoomAbove_GoesInside()
    {
        Assert.Equal(new Point(506, 16), OverlayLayout.PlaceSizeLabel(new Rectangle(500, 10, 300, 200), Label, Monitor, 6));
    }

    [Fact]
    public void SizeLabel_NearRightEdge_StaysOnMonitor()
    {
        var p = OverlayLayout.PlaceSizeLabel(new Rectangle(1900, 400, 20, 20), Label, Monitor, 6);

        Assert.Equal(1920 - 90, p.X);
    }

    [Fact]
    public void SizeLabel_OnSecondMonitor_StaysOnThatMonitor()
    {
        var second = new Rectangle(1920, 0, 2560, 1440);
        var p = OverlayLayout.PlaceSizeLabel(new Rectangle(1920, 0, 100, 100), Label, second, 6);

        Assert.True(second.Contains(new Rectangle(p, Label)), $"Belgi monitordan chiqdi: {p}");
    }

    [Fact]
    public void Hint_CenteredAtTop()
    {
        Assert.Equal(new Point((1920 - 600) / 2, 30), OverlayLayout.PlaceHint(new Size(600, 40), Monitor, 30));
        Assert.Equal(new Point(1920 + ((2560 - 600) / 2), 30), OverlayLayout.PlaceHint(new Size(600, 40), new Rectangle(1920, 0, 2560, 1440), 30));
        Assert.Equal(new Point(0, 30), OverlayLayout.PlaceHint(new Size(3000, 40), Monitor, 30));
    }

    [Fact]
    public void SizeText_IsCultureIndependent()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal("1280 × 720", OverlayLayout.SizeText(new Size(1280, 720)));
            Assert.Equal("10000 × 1", OverlayLayout.SizeText(new Size(10000, 1)));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
