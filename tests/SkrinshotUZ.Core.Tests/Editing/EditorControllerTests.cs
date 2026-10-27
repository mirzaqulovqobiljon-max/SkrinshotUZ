using System.Drawing;
using SkrinshotUZ.Core.Editing;

namespace SkrinshotUZ.Core.Tests.Editing;

/// <summary>Har bir asbob: chizish, Shift cheklovlari, yaroqsiz shakllar, qalinlik va masshtab.</summary>
public class EditorControllerTests
{
    private readonly AnnotationHistory _history = new();

    private EditorController Editor(EditorTool tool) => new(_history) { Tool = tool };

    private static void Draw(EditorController e, Point from, Point to, bool shift = false, double scale = 1.0)
    {
        e.Begin(from, scale);
        e.Move(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), shift);
        e.End(to, shift);
    }

    [Fact]
    public void Defaults_RedMediumNoTool()
    {
        var e = new EditorController(_history);

        Assert.Equal(EditorTool.None, e.Tool);
        Assert.Equal(EditorController.DefaultColor, e.Color);
        Assert.Equal(Thickness.Medium, e.Thickness);
        Assert.False(e.IsDrawing);
        Assert.Null(e.Preview);
    }

    [Theory]
    [InlineData(EditorTool.None)]
    [InlineData(EditorTool.Text)]
    public void ToolsWithoutDrawing_BeginDoesNothing(EditorTool tool)
    {
        var e = Editor(tool);
        e.Begin(new Point(10, 10), 1.0);

        Assert.False(e.IsDrawing);
        Assert.Null(e.End(new Point(50, 50), false));
        Assert.Equal(0, _history.Count);
    }

    [Fact]
    public void Pen_CollectsPoints_SkipsDuplicates()
    {
        var e = Editor(EditorTool.Pen);
        e.Begin(new Point(10, 10), 1.0);
        e.Move(new Point(11, 10), false);
        e.Move(new Point(11, 10), false); // takror — tashlanadi
        e.Move(new Point(12, 12), false);
        var a = Assert.IsType<StrokeAnnotation>(e.End(new Point(12, 12), false));

        Assert.Equal(new[] { new Point(10, 10), new Point(11, 10), new Point(12, 12) }, a.Points);
        Assert.False(a.IsMarker);
        Assert.Equal(4f, a.Width);
        Assert.Same(a, Assert.Single(_history.Items));
    }

    [Fact]
    public void Pen_SingleClick_LeavesDot()
    {
        var e = Editor(EditorTool.Pen);
        e.Begin(new Point(30, 30), 1.0);
        var a = Assert.IsType<StrokeAnnotation>(e.End(new Point(30, 30), false));

        Assert.Equal(new[] { new Point(30, 30) }, a.Points);
    }

    [Fact]
    public void PenWithShift_IsStraightLineSnappedTo45()
    {
        var e = Editor(EditorTool.Pen);
        e.Begin(new Point(0, 0), 1.0);
        e.Move(new Point(30, 5), true);
        var a = Assert.IsType<StrokeAnnotation>(e.End(new Point(100, 4), true));

        Assert.Equal(new[] { new Point(0, 0), new Point(100, 0) }, a.Points);
    }

    [Fact]
    public void Marker_IsWiderAndMarked()
    {
        var e = Editor(EditorTool.Marker);
        Draw(e, new Point(0, 0), new Point(100, 0));
        var a = Assert.IsType<StrokeAnnotation>(Assert.Single(_history.Items));

        Assert.True(a.IsMarker);
        Assert.Equal(18f, a.Width);
    }

    [Fact]
    public void Line_FromStartToEnd()
    {
        var e = Editor(EditorTool.Line);
        Draw(e, new Point(10, 10), new Point(90, 40));
        var a = Assert.IsType<LineAnnotation>(Assert.Single(_history.Items));

        Assert.Equal(new Point(10, 10), a.Start);
        Assert.Equal(new Point(90, 40), a.End);
        Assert.False(a.HasArrowHead);
    }

    [Fact]
    public void Arrow_HasArrowHead_AndShiftSnapsVertical()
    {
        var e = Editor(EditorTool.Arrow);
        Draw(e, new Point(50, 50), new Point(53, 150), shift: true);
        var a = Assert.IsType<LineAnnotation>(Assert.Single(_history.Items));

        Assert.True(a.HasArrowHead);
        Assert.Equal(new Point(50, 150), a.End);
    }

    [Theory]
    [InlineData(EditorTool.Line)]
    [InlineData(EditorTool.Arrow)]
    public void ZeroOrOnePixelLine_IsDiscarded(EditorTool tool)
    {
        var e = Editor(tool);
        Draw(e, new Point(10, 10), new Point(10, 10));
        Draw(e, new Point(10, 10), new Point(11, 10));

        Assert.Equal(0, _history.Count);
    }

    [Fact]
    public void Rectangle_AnyDirection_Normalized()
    {
        var e = Editor(EditorTool.Rectangle);
        Draw(e, new Point(200, 150), new Point(100, 100));
        var a = Assert.IsType<RectangleAnnotation>(Assert.Single(_history.Items));

        Assert.Equal(new Rectangle(100, 100, 100, 50), a.Rect);
    }

    [Fact]
    public void RectangleWithShift_IsSquare()
    {
        var e = Editor(EditorTool.Rectangle);
        Draw(e, new Point(100, 100), new Point(130, 180), shift: true);
        var a = Assert.IsType<RectangleAnnotation>(Assert.Single(_history.Items));

        Assert.Equal(new Rectangle(100, 100, 80, 80), a.Rect);
    }

    [Fact]
    public void FlatRectangle_IsDiscarded()
    {
        var e = Editor(EditorTool.Rectangle);
        Draw(e, new Point(100, 100), new Point(200, 101));

        Assert.Equal(0, _history.Count);
    }

    [Fact]
    public void PreviewVisibleWhileDrawing_ClearedAfter()
    {
        var e = Editor(EditorTool.Rectangle);
        e.Begin(new Point(10, 10), 1.0);
        e.Move(new Point(60, 40), false);

        Assert.True(e.IsDrawing);
        Assert.Equal(new Rectangle(10, 10, 50, 30), Assert.IsType<RectangleAnnotation>(e.Preview).Rect);
        Assert.Equal(0, _history.Count); // hali tarixga yozilmagan

        e.End(new Point(60, 40), false);
        Assert.Null(e.Preview);
        Assert.False(e.IsDrawing);
    }

    [Fact]
    public void ChangingTool_CancelsDrawing()
    {
        var e = Editor(EditorTool.Line);
        e.Begin(new Point(10, 10), 1.0);
        e.Tool = EditorTool.Rectangle;

        Assert.False(e.IsDrawing);
        Assert.Null(e.End(new Point(90, 90), false));
        Assert.Equal(0, _history.Count);
    }

    [Fact]
    public void ColorAndThickness_AreAppliedToNewShapes()
    {
        var e = Editor(EditorTool.Line);
        e.Color = Color.Blue;
        e.Thickness = Thickness.ExtraThick;
        Draw(e, new Point(0, 0), new Point(50, 0));

        var a = Assert.Single(_history.Items);
        Assert.Equal(Color.Blue, a.Color);
        Assert.Equal(12f, a.Width);
    }

    [Theory]
    [InlineData(1.0, 4f)]
    [InlineData(1.25, 5f)]
    [InlineData(1.5, 6f)]
    [InlineData(2.0, 8f)]
    public void Width_ScalesWithMonitorDpi(double scale, float expected)
    {
        var e = Editor(EditorTool.Line);
        Draw(e, new Point(0, 0), new Point(50, 0), scale: scale);

        Assert.Equal(expected, Assert.Single(_history.Items).Width, 3);
    }

    [Fact]
    public void ThicknessLevels_AreIncreasing()
    {
        var levels = Enum.GetValues<Thickness>();
        for (int i = 1; i < levels.Length; i++)
        {
            Assert.True(EditorController.StrokeWidth(levels[i], 1) > EditorController.StrokeWidth(levels[i - 1], 1));
            Assert.True(EditorController.MarkerWidth(levels[i], 1) > EditorController.MarkerWidth(levels[i - 1], 1));
            Assert.True(EditorController.FontSize(levels[i], 1) > EditorController.FontSize(levels[i - 1], 1));
            Assert.True(EditorController.MarkerWidth(levels[i], 1) > EditorController.StrokeWidth(levels[i], 1));
        }
    }

    [Fact]
    public void CommitText_AddsWithCurrentColor_NormalizesLineEndings()
    {
        var e = Editor(EditorTool.Text);
        e.Color = Color.Green;
        var a = e.CommitText(new Point(40, 50), "Salom,\r\nsinf!\r\n\r\n", new Size(80, 40), 20f);

        Assert.NotNull(a);
        Assert.Equal("Salom,\nsinf!", a.Text);
        Assert.Equal(Color.Green, a.Color);
        Assert.Equal(new Point(40, 50), a.Location);
        Assert.Equal(20f, a.Width);
        Assert.Same(a, Assert.Single(_history.Items));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    [InlineData(null)]
    public void CommitText_EmptyIsIgnored(string? text)
    {
        Assert.Null(Editor(EditorTool.Text).CommitText(new Point(0, 0), text, Size.Empty, 20f));
        Assert.Equal(0, _history.Count);
    }

    [Fact]
    public void CommitText_UzbekCharactersPreserved()
    {
        var a = Editor(EditorTool.Text).CommitText(new Point(0, 0), "Oʻzbekiston — gʻalaba! Ўзбекистон", new Size(10, 10), 16f);

        Assert.Equal("Oʻzbekiston — gʻalaba! Ўзбекистон", a!.Text);
    }
}
