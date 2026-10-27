using System.Drawing;
using SkrinshotUZ.Core.Capture;
using SkrinshotUZ.Core.Editing;

namespace SkrinshotUZ.Core.Tests.Editing;

public class EditorUiTests
{
    private static readonly Rectangle Monitor = new(0, 0, 1920, 1080);
    private static readonly Size ToolsBar = new(48, 420);
    private static readonly Size ActionsBar = new(200, 48);

    [Fact]
    public void ToolsBar_RightOfSelection()
    {
        Assert.Equal(new Point(808, 100), EditorUi.PlaceToolsBar(new Rectangle(300, 100, 500, 400), ToolsBar, Monitor, 8));
    }

    [Fact]
    public void ToolsBar_NoRoomRight_GoesLeft()
    {
        Assert.Equal(new Point(1400 - 8 - 48, 100), EditorUi.PlaceToolsBar(new Rectangle(1400, 100, 500, 400), ToolsBar, Monitor, 8));
    }

    [Fact]
    public void ToolsBar_FullScreenSelection_GoesInside_StaysOnMonitor()
    {
        var p = EditorUi.PlaceToolsBar(Monitor, ToolsBar, Monitor, 8);

        Assert.Equal(1920 - 8 - 48, p.X);
        Assert.True(Monitor.Contains(new Rectangle(p, ToolsBar)));
    }

    [Fact]
    public void ToolsBar_SelectionNearBottom_ClampedUp()
    {
        var p = EditorUi.PlaceToolsBar(new Rectangle(300, 1000, 200, 50), ToolsBar, Monitor, 8);

        Assert.Equal(1080 - 420, p.Y);
    }

    [Fact]
    public void ActionsBar_BelowSelection_RightAligned()
    {
        Assert.Equal(new Point(800 - 200, 508), EditorUi.PlaceActionsBar(new Rectangle(300, 100, 500, 400), ActionsBar, Monitor, 8));
    }

    [Fact]
    public void ActionsBar_NoRoomBelow_GoesAbove()
    {
        Assert.Equal(new Point(600, 600 - 8 - 48), EditorUi.PlaceActionsBar(new Rectangle(300, 600, 500, 470), ActionsBar, Monitor, 8));
    }

    [Fact]
    public void ActionsBar_FullScreenSelection_GoesInside()
    {
        var p = EditorUi.PlaceActionsBar(Monitor, ActionsBar, Monitor, 8);

        Assert.Equal(1080 - 8 - 48, p.Y);
        Assert.True(Monitor.Contains(new Rectangle(p, ActionsBar)));
    }

    [Fact]
    public void Bars_OnSecondMonitor_StayOnIt()
    {
        var second = new Rectangle(1920, 0, 1280, 1024);
        var sel = new Rectangle(1920, 0, 1280, 1024);

        Assert.True(second.Contains(new Rectangle(EditorUi.PlaceToolsBar(sel, ToolsBar, second, 8), ToolsBar)));
        Assert.True(second.Contains(new Rectangle(EditorUi.PlaceActionsBar(sel, ActionsBar, second, 8), ActionsBar)));
    }

    [Theory]
    [InlineData('P', EditorTool.Pen)]
    [InlineData('p', EditorTool.Pen)]
    [InlineData('L', EditorTool.Line)]
    [InlineData('A', EditorTool.Arrow)]
    [InlineData('R', EditorTool.Rectangle)]
    [InlineData('M', EditorTool.Marker)]
    [InlineData('t', EditorTool.Text)]
    public void ToolForKey_KnownLetters(char key, EditorTool expected)
    {
        Assert.Equal(expected, EditorUi.ToolForKey(key));
    }

    [Theory]
    [InlineData('X')]
    [InlineData('1')]
    [InlineData(' ')]
    public void ToolForKey_OtherKeys_Null(char key)
    {
        Assert.Null(EditorUi.ToolForKey(key));
    }

    [Fact]
    public void EveryTool_HasNameWithItsShortcut()
    {
        foreach (var tool in EditorUi.Tools)
        {
            string name = EditorUi.ToolName(tool);
            char key = "PLARMT"[EditorUi.Tools.ToList().IndexOf(tool)];
            Assert.Equal(tool, EditorUi.ToolForKey(key));
            Assert.Contains("(" + key + ")", name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Palette_EightDistinctNamedColors_FirstIsDefault()
    {
        Assert.Equal(8, EditorUi.Palette.Count);
        Assert.Equal(EditorController.DefaultColor, EditorUi.Palette[0].Color);
        Assert.Equal(8, EditorUi.Palette.Select(p => p.Color.ToArgb()).Distinct().Count());
        Assert.DoesNotContain(EditorUi.Palette, p => string.IsNullOrWhiteSpace(p.Name));
    }

    [Fact]
    public void EveryThickness_HasName()
    {
        foreach (var t in Enum.GetValues<Thickness>())
        {
            Assert.False(string.IsNullOrWhiteSpace(EditorUi.ThicknessName(t)));
        }
    }

    [Fact]
    public void SelectionWithAnnotations_ClickOutsideDoesNotStartNewSelection()
    {
        // Chizma bor sohada tasodifiy bosish chizmalarni "yo'qotmasligi" kerak.
        var c = new SelectionController(new Size(1000, 800));
        c.SetSelection(new Rectangle(100, 100, 200, 100));
        c.CanStartNew = false;

        c.BeginDrag(new Point(600, 600), 4);
        c.Drag(new Point(700, 700));
        c.EndDrag(new Point(700, 700));

        Assert.Equal(new Rectangle(100, 100, 200, 100), c.Selection);
        Assert.Equal(SelectionState.Selected, c.State);

        // Surish esa ishlayveradi.
        c.BeginDrag(new Point(150, 150), 4);
        c.EndDrag(new Point(160, 150));
        Assert.Equal(new Rectangle(110, 100, 200, 100), c.Selection);
    }
}
