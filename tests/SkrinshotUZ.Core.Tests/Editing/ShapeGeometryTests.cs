using System.Drawing;
using SkrinshotUZ.Core.Editing;

namespace SkrinshotUZ.Core.Tests.Editing;

public class ShapeGeometryTests
{
    [Theory]
    [InlineData(100, 3, 100, 0)]      // deyarli gorizontal → gorizontal
    [InlineData(-80, -5, -80, 0)]     // chapga
    [InlineData(4, 90, 0, 90)]        // deyarli vertikal → vertikal
    [InlineData(-2, -70, 0, -70)]     // yuqoriga
    [InlineData(50, 45, 50, 50)]      // diagonal o'ng-past → aniq 45°
    [InlineData(-60, 55, -60, 60)]    // chap-past
    [InlineData(40, -48, 48, -48)]    // o'ng-yuqori
    [InlineData(-30, -33, -33, -33)]  // chap-yuqori
    public void SnapAngle_ToNearest45Degrees(int dx, int dy, int ex, int ey)
    {
        var o = new Point(500, 500);

        Assert.Equal(new Point(500 + ex, 500 + ey), ShapeGeometry.SnapAngle(o, new Point(500 + dx, 500 + dy)));
    }

    [Fact]
    public void SnapAngle_SamePoint_Unchanged()
    {
        Assert.Equal(new Point(5, 5), ShapeGeometry.SnapAngle(new Point(5, 5), new Point(5, 5)));
    }

    [Theory]
    [InlineData(130, 180, 100, 100, 80)]  // o'ng-past: tomon = max(30, 80)
    [InlineData(70, 20, 20, 20, 80)]      // chap-yuqori
    [InlineData(40, 130, 40, 100, 60)]    // chap-past: anchor o'ng-yuqori burchakda qoladi
    [InlineData(150, 90, 100, 50, 50)]    // o'ng-yuqori
    public void SquareFrom_KeepsDragDirection(int px, int py, int x, int y, int side)
    {
        Assert.Equal(new Rectangle(x, y, side, side), ShapeGeometry.SquareFrom(new Point(100, 100), new Point(px, py)));
    }

    [Fact]
    public void RectFrom_Normalizes()
    {
        Assert.Equal(new Rectangle(10, 20, 30, 40), ShapeGeometry.RectFrom(new Point(40, 60), new Point(10, 20)));
    }

    [Fact]
    public void ArrowHead_PointsAlongLine_SymmetricWings()
    {
        var h = ShapeGeometry.ArrowHeadFor(new Point(0, 0), new Point(100, 0), 4f);
        float len = ShapeGeometry.ArrowHeadLength(4f); // 16

        Assert.Equal(new PointF(100, 0), h.Tip);
        Assert.Equal(100 - len, h.Base.X, 3);
        Assert.Equal(0, h.Base.Y, 3);
        Assert.Equal(h.Base.X, h.Left.X, 3);
        Assert.Equal(h.Base.X, h.Right.X, 3);
        Assert.Equal(-h.Left.Y, h.Right.Y, 3);           // simmetrik
        Assert.Equal(len, Math.Abs(h.Left.Y - h.Right.Y), 3); // qanotlar kengligi = uzunlik
    }

    [Fact]
    public void ArrowHead_DiagonalDirection()
    {
        var h = ShapeGeometry.ArrowHeadFor(new Point(0, 0), new Point(100, 100), 4f);

        Assert.True(h.Base.X < 100 && h.Base.Y < 100);
        Assert.Equal(h.Base.X, h.Base.Y, 3); // diagonal bo'ylab
    }

    [Fact]
    public void ArrowHead_ShortArrow_HeadNotLongerThanLine()
    {
        var h = ShapeGeometry.ArrowHeadFor(new Point(0, 0), new Point(5, 0), 10f);

        Assert.Equal(0f, h.Base.X, 3);
    }

    [Fact]
    public void ArrowHead_ZeroLength_Degenerate_NoNaN()
    {
        var h = ShapeGeometry.ArrowHeadFor(new Point(7, 7), new Point(7, 7), 4f);

        Assert.Equal(new PointF(7, 7), h.Left);
        Assert.False(float.IsNaN(h.Right.X));
    }

    [Fact]
    public void ArrowHeadLength_HasMinimum_GrowsWithWidth()
    {
        Assert.Equal(12f, ShapeGeometry.ArrowHeadLength(1f));
        Assert.Equal(48f, ShapeGeometry.ArrowHeadLength(12f));
    }

    [Fact]
    public void Bounds_IncludeStrokeWidthAndArrowHead()
    {
        var line = new LineAnnotation(new Point(100, 100), new Point(200, 100), Color.Red, 4, false);
        var arrow = line with { HasArrowHead = true };
        var rect = new RectangleAnnotation(new Rectangle(10, 10, 50, 50), Color.Red, 6);
        var stroke = new StrokeAnnotation(new[] { new Point(0, 0), new Point(10, 20) }, Color.Red, 10, false);

        Assert.True(line.Bounds.Contains(new Rectangle(100, 98, 100, 4)));
        Assert.True(arrow.Bounds.Contains(line.Bounds));
        Assert.True(arrow.Bounds.Height >= ShapeGeometry.ArrowHeadLength(4));
        Assert.True(rect.Bounds.Contains(Rectangle.Inflate(rect.Rect, 3, 3)));
        Assert.True(stroke.Bounds.Contains(new Rectangle(-5, -5, 20, 30)));
        Assert.Equal(Rectangle.Empty, new StrokeAnnotation(Array.Empty<Point>(), Color.Red, 4, false).Bounds);
    }
}
