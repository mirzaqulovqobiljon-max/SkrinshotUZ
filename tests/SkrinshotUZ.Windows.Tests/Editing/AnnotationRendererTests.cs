using System.Drawing;
using System.Drawing.Imaging;
using SkrinshotUZ.Core.Editing;
using SkrinshotUZ.Editing;

namespace SkrinshotUZ.Windows.Tests.Editing;

/// <summary>Chizmalar haqiqatan to'g'ri joyga, to'g'ri rangda chiziladi (bitmap piksellari bo'yicha).</summary>
public class AnnotationRendererTests
{
    private static readonly Color Red = Color.FromArgb(255, 0, 0);

    private static Bitmap White(int w = 200, int h = 150)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        return bmp;
    }

    private static Bitmap Render(Annotation a, Point offset = default)
    {
        var bmp = White();
        using var g = Graphics.FromImage(bmp);
        AnnotationRenderer.Draw(g, a, offset);
        return bmp;
    }

    private static bool IsRed(Color c) => c.R > 200 && c.G < 80 && c.B < 80;

    private static bool IsWhite(Color c) => c.R > 245 && c.G > 245 && c.B > 245;

    [Fact]
    public void Rectangle_BorderIsColored_InsideStaysClean()
    {
        using var bmp = Render(new RectangleAnnotation(new Rectangle(20, 20, 100, 60), Red, 4));

        Assert.True(IsRed(bmp.GetPixel(20, 50)), "chap chet");
        Assert.True(IsRed(bmp.GetPixel(120, 50)), "o'ng chet");
        Assert.True(IsRed(bmp.GetPixel(70, 20)), "yuqori chet");
        Assert.True(IsRed(bmp.GetPixel(70, 80)), "pastki chet");
        Assert.True(IsWhite(bmp.GetPixel(70, 50)), "ichi bo'sh bo'lishi kerak");
        Assert.True(IsWhite(bmp.GetPixel(5, 5)), "tashqarisi toza");
    }

    [Fact]
    public void Line_CoversPathOnly()
    {
        using var bmp = Render(new LineAnnotation(new Point(10, 75), new Point(190, 75), Red, 6, false));

        Assert.True(IsRed(bmp.GetPixel(100, 75)));
        Assert.True(IsRed(bmp.GetPixel(100, 73)));
        Assert.True(IsWhite(bmp.GetPixel(100, 65)));
    }

    [Fact]
    public void Arrow_HeadIsFilledNearTip_WiderThanLine()
    {
        using var bmp = Render(new LineAnnotation(new Point(10, 75), new Point(190, 75), Red, 4, true));
        float head = ShapeGeometry.ArrowHeadLength(4); // 16 px

        // Uch qismida, chiziq qalinligidan (±2) ancha tashqarida ham rang bor.
        Assert.True(IsRed(bmp.GetPixel(190 - (int)(head * 0.75f), 75 + 4)));
        // Chiziq o'rtasida esa bu balandlikda rang yo'q.
        Assert.True(IsWhite(bmp.GetPixel(100, 75 + 4)));
    }

    [Fact]
    public void Pen_SingleClick_DrawsDot()
    {
        using var bmp = Render(new StrokeAnnotation(new[] { new Point(50, 50) }, Red, 8, false));

        Assert.True(IsRed(bmp.GetPixel(50, 50)));
        Assert.True(IsWhite(bmp.GetPixel(60, 50)));
    }

    [Fact]
    public void Pen_Polyline_FollowsPoints()
    {
        using var bmp = Render(new StrokeAnnotation(new[] { new Point(20, 20), new Point(100, 20), new Point(100, 100) }, Red, 4, false));

        Assert.True(IsRed(bmp.GetPixel(60, 20)));
        Assert.True(IsRed(bmp.GetPixel(100, 60)));
        Assert.True(IsWhite(bmp.GetPixel(60, 60)));
    }

    [Fact]
    public void Marker_IsSemiTransparent_UnderlyingContentShowsThrough()
    {
        var yellow = Color.FromArgb(255, 200, 0);
        using var bmp = White();
        using (var g = Graphics.FromImage(bmp))
        using (var black = new SolidBrush(Color.Black))
        {
            g.FillRectangle(black, 0, 70, 200, 10); // "matn qatori"
            AnnotationRenderer.Draw(g, new StrokeAnnotation(new[] { new Point(10, 75), new Point(190, 75) }, yellow, 20, true), Point.Empty);
        }

        var onWhite = bmp.GetPixel(100, 67);
        var onBlack = bmp.GetPixel(100, 75);
        Assert.True(onWhite.B < 200 && onWhite.R > 230, $"oq ustida sarg'ish bo'lishi kerak: {onWhite}");
        Assert.True(onBlack.R < 120, $"qora matn marker ostida ko'rinib turishi kerak: {onBlack}");
    }

    [Fact]
    public void Text_DrawsPixelsAtLocation()
    {
        var size = new Size(120, 40);
        using var bmp = Render(new TextAnnotation(new Point(30, 40), "Salom", Red, 28, size));

        bool any = false;
        for (int y = 40; y < 80 && !any; y++)
        {
            for (int x = 30; x < 150 && !any; x++)
            {
                any = IsRed(bmp.GetPixel(x, y));
            }
        }

        Assert.True(any, "matn chizilmadi");
        Assert.True(IsWhite(bmp.GetPixel(10, 10)));
    }

    [Fact]
    public void Offset_ShiftsDrawing()
    {
        // Kanvasdagi (120, 70) nuqta, offset (100, 50) bilan, natija rasmida (20, 20) da bo'lishi kerak.
        using var bmp = Render(new StrokeAnnotation(new[] { new Point(120, 70) }, Red, 6, false), new Point(100, 50));

        Assert.True(IsRed(bmp.GetPixel(20, 20)));
    }

    [Fact]
    public void Compose_CropsAndDrawsAnnotationsInSelectionCoordinates()
    {
        using var screen = new Bitmap(400, 300, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(screen))
        {
            g.Clear(Color.White);
        }

        var selection = new Rectangle(150, 100, 120, 80);
        var annotations = new Annotation[]
        {
            new RectangleAnnotation(new Rectangle(160, 110, 50, 40), Red, 4),        // sohada (10,10) dan
            new StrokeAnnotation(new[] { new Point(10, 10) }, Color.Blue, 20, false), // soha tashqarisida — natijaga tushmaydi
        };

        using var result = ImageComposer.Compose(screen, selection, annotations);

        Assert.Equal(new Size(120, 80), result.Size);
        Assert.True(IsRed(result.GetPixel(10, 30)), "to'rtburchakning chap cheti (10, y)");
        Assert.True(IsWhite(result.GetPixel(60, 30)), "to'rtburchak ichi");
        Assert.True(IsWhite(result.GetPixel(0, 0)), "soha tashqarisidagi chizma natijaga tushmasligi kerak");
    }
}
