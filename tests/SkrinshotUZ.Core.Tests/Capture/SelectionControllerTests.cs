using System.Drawing;
using SkrinshotUZ.Core.Capture;

namespace SkrinshotUZ.Core.Tests.Capture;

/// <summary>Soha tanlash: chizish, surish, burchak/chetdan o'zgartirish, klaviatura, chegaralar.</summary>
public class SelectionControllerTests
{
    private const int Grip = 4;

    private static SelectionController Canvas() => new(new Size(1000, 800));

    private static SelectionController WithSelection(Rectangle r)
    {
        var c = Canvas();
        Assert.True(c.SetSelection(r));
        return c;
    }

    private static void Drag(SelectionController c, Point from, Point to)
    {
        c.BeginDrag(from, Grip);
        c.Drag(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        c.EndDrag(to);
    }

    [Fact]
    public void Initially_Empty()
    {
        var c = Canvas();

        Assert.Equal(SelectionState.Empty, c.State);
        Assert.False(c.HasSelection);
        Assert.Equal(Rectangle.Empty, c.Selection);
        Assert.Equal(SelectionHandle.None, c.HitTest(new Point(10, 10), Grip));
    }

    [Fact]
    public void DragDownRight_CreatesHalfOpenRectangle()
    {
        var c = Canvas();
        Drag(c, new Point(10, 10), new Point(110, 60));

        Assert.Equal(SelectionState.Selected, c.State);
        Assert.Equal(new Rectangle(10, 10, 100, 50), c.Selection);
    }

    [Fact]
    public void DragUpLeft_GivesSameRectangle()
    {
        var c = Canvas();
        Drag(c, new Point(110, 60), new Point(10, 10));

        Assert.Equal(new Rectangle(10, 10, 100, 50), c.Selection);
    }

    [Fact]
    public void WhileCreating_SelectionIsLive()
    {
        var c = Canvas();
        c.BeginDrag(new Point(100, 100), Grip);
        c.Drag(new Point(150, 130));

        Assert.Equal(SelectionState.Creating, c.State);
        Assert.True(c.IsDragging);
        Assert.Equal(new Rectangle(100, 100, 50, 30), c.Selection);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(50, 1)]
    public void ClickOrTinyDrag_DoesNotCreateSelection(int dx, int dy)
    {
        var c = Canvas();
        Drag(c, new Point(100, 100), new Point(100 + dx, 100 + dy));

        Assert.Equal(SelectionState.Empty, c.State);
    }

    [Fact]
    public void MinimumSize_IsAccepted()
    {
        var c = Canvas();
        Drag(c, new Point(100, 100), new Point(102, 102));

        Assert.Equal(new Rectangle(100, 100, 2, 2), c.Selection);
    }

    [Fact]
    public void DragOutsideCanvas_IsClamped()
    {
        var c = Canvas();
        Drag(c, new Point(900, 700), new Point(5000, 5000));
        Assert.Equal(new Rectangle(900, 700, 100, 100), c.Selection);

        Drag(c, new Point(50, 50), new Point(-300, -10));
        Assert.Equal(new Rectangle(0, 0, 50, 50), c.Selection);
    }

    [Fact]
    public void DragCornerToCorner_SelectsWholeCanvas_IncludingLastPixel()
    {
        var c = Canvas();
        Drag(c, new Point(0, 0), new Point(1000, 800));

        Assert.Equal(c.Bounds, c.Selection);
    }

    [Fact]
    public void HitTest_InsideCornersEdgesAndOutside()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100)); // o'ng=300, past=200

        Assert.Equal(SelectionHandle.Inside, c.HitTest(new Point(200, 150), Grip));
        Assert.Equal(SelectionHandle.TopLeft, c.HitTest(new Point(98, 103), Grip));
        Assert.Equal(SelectionHandle.TopRight, c.HitTest(new Point(302, 97), Grip));
        Assert.Equal(SelectionHandle.BottomRight, c.HitTest(new Point(300, 200), Grip));
        Assert.Equal(SelectionHandle.BottomLeft, c.HitTest(new Point(100, 204), Grip));
        Assert.Equal(SelectionHandle.Top, c.HitTest(new Point(200, 98), Grip));
        Assert.Equal(SelectionHandle.Bottom, c.HitTest(new Point(200, 203), Grip));
        Assert.Equal(SelectionHandle.Left, c.HitTest(new Point(97, 150), Grip));
        Assert.Equal(SelectionHandle.Right, c.HitTest(new Point(303, 150), Grip));
        Assert.Equal(SelectionHandle.None, c.HitTest(new Point(400, 150), Grip));
        Assert.Equal(SelectionHandle.None, c.HitTest(new Point(200, 210), Grip));
    }

    [Fact]
    public void HitTest_TinySelection_CornersWin()
    {
        var c = WithSelection(new Rectangle(100, 100, 3, 3));

        Assert.Equal(SelectionHandle.TopLeft, c.HitTest(new Point(100, 100), Grip));
        Assert.Equal(SelectionHandle.BottomRight, c.HitTest(new Point(104, 104), Grip));
    }

    [Fact]
    public void MoveInside_ShiftsSelection_KeepsSize()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        Drag(c, new Point(150, 150), new Point(200, 170));

        Assert.Equal(new Rectangle(150, 120, 200, 100), c.Selection);
    }

    [Fact]
    public void MovePastCanvasEdge_StopsAtEdge_KeepsSize()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        Drag(c, new Point(150, 150), new Point(5000, -5000));

        Assert.Equal(new Rectangle(800, 0, 200, 100), c.Selection);
    }

    [Theory]
    [InlineData(SelectionHandle.TopLeft, 50, 60, 50, 60, 250, 140)]
    [InlineData(SelectionHandle.Top, 999, 60, 100, 60, 200, 140)]
    [InlineData(SelectionHandle.TopRight, 400, 60, 100, 60, 300, 140)]
    [InlineData(SelectionHandle.Right, 400, 999, 100, 100, 300, 100)]
    [InlineData(SelectionHandle.BottomRight, 400, 300, 100, 100, 300, 200)]
    [InlineData(SelectionHandle.Bottom, 999, 300, 100, 100, 200, 200)]
    [InlineData(SelectionHandle.BottomLeft, 50, 300, 50, 100, 250, 200)]
    [InlineData(SelectionHandle.Left, 50, 999, 50, 100, 250, 100)]
    public void ResizeByEachHandle_MovesOnlyItsEdges(SelectionHandle handle, int toX, int toY, int x, int y, int w, int h)
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        var start = HandlePoint(c.Selection, handle);
        Assert.Equal(handle, c.HitTest(start, Grip));

        c.BeginDrag(start, Grip);
        Assert.Equal(SelectionState.Resizing, c.State);
        Assert.Equal(handle, c.ActiveHandle);
        c.EndDrag(new Point(toX, toY));

        Assert.Equal(new Rectangle(x, y, w, h), c.Selection);
        Assert.Equal(SelectionState.Selected, c.State);
    }

    [Fact]
    public void ResizePastOppositeEdge_FlipsInsteadOfNegativeSize()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        Drag(c, new Point(100, 150), new Point(400, 150)); // chap chetni o'ng chetdan o'tkazamiz

        Assert.Equal(new Rectangle(300, 100, 100, 100), c.Selection);
    }

    [Fact]
    public void ResizeToZero_RestoresPreviousSelection()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        Drag(c, new Point(100, 150), new Point(300, 150)); // chap chet o'ng chetga aynan tushdi → kenglik 0

        Assert.Equal(new Rectangle(100, 100, 200, 100), c.Selection);
    }

    [Fact]
    public void ResizeBeyondCanvas_IsClamped()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        Drag(c, new Point(300, 200), new Point(9999, 9999));

        Assert.Equal(new Rectangle(100, 100, 900, 700), c.Selection);
    }

    [Fact]
    public void DragOutsideExistingSelection_StartsNewOne()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        Drag(c, new Point(500, 500), new Point(600, 650));

        Assert.Equal(new Rectangle(500, 500, 100, 150), c.Selection);
    }

    [Fact]
    public void ClickOutsideExistingSelection_ClearsIt()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        Drag(c, new Point(500, 500), new Point(500, 500));

        Assert.Equal(SelectionState.Empty, c.State);
    }

    [Fact]
    public void SetSelection_ClipsToCanvas_RejectsOutside()
    {
        var c = Canvas();

        Assert.True(c.SetSelection(new Rectangle(-50, 700, 300, 300)));  // masalan, ekrandan chiqib turgan oyna
        Assert.Equal(new Rectangle(0, 700, 250, 100), c.Selection);

        Assert.False(c.SetSelection(new Rectangle(2000, 2000, 100, 100)));
        Assert.Equal(new Rectangle(0, 700, 250, 100), c.Selection); // avvalgisi o'zgarmadi
    }

    [Fact]
    public void Nudge_MovesByPixels_AndStopsAtEdges()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));

        c.Nudge(1, 0);
        Assert.Equal(new Rectangle(101, 100, 200, 100), c.Selection);
        c.Nudge(-10, -10);
        Assert.Equal(new Rectangle(91, 90, 200, 100), c.Selection);
        c.Nudge(-1000, 5000);
        Assert.Equal(new Rectangle(0, 700, 200, 100), c.Selection);
    }

    [Fact]
    public void ResizeBy_GrowsAndShrinks_WithinLimits()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));

        c.ResizeBy(10, -20);
        Assert.Equal(new Rectangle(100, 100, 210, 80), c.Selection);
        c.ResizeBy(-1000, -1000);
        Assert.Equal(new Rectangle(100, 100, SelectionController.MinSize, SelectionController.MinSize), c.Selection);
        c.ResizeBy(5000, 5000);
        Assert.Equal(new Rectangle(100, 100, 900, 700), c.Selection);
    }

    [Fact]
    public void KeyboardWithoutSelection_DoesNothing()
    {
        var c = Canvas();
        c.Nudge(5, 5);
        c.ResizeBy(5, 5);
        c.EndDrag(new Point(5, 5));

        Assert.Equal(SelectionState.Empty, c.State);
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var c = WithSelection(new Rectangle(100, 100, 200, 100));
        c.Clear();

        Assert.False(c.HasSelection);
        Assert.Equal(SelectionHandle.None, c.HitTest(new Point(150, 150), Grip));
    }

    [Fact]
    public void InvalidCanvas_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectionController(new Size(0, 100)));
    }

    private static Point HandlePoint(Rectangle r, SelectionHandle h) => h switch
    {
        SelectionHandle.TopLeft => new Point(r.Left, r.Top),
        SelectionHandle.Top => new Point(r.Left + (r.Width / 2), r.Top),
        SelectionHandle.TopRight => new Point(r.Right, r.Top),
        SelectionHandle.Right => new Point(r.Right, r.Top + (r.Height / 2)),
        SelectionHandle.BottomRight => new Point(r.Right, r.Bottom),
        SelectionHandle.Bottom => new Point(r.Left + (r.Width / 2), r.Bottom),
        SelectionHandle.BottomLeft => new Point(r.Left, r.Bottom),
        SelectionHandle.Left => new Point(r.Left, r.Top + (r.Height / 2)),
        _ => throw new ArgumentOutOfRangeException(nameof(h)),
    };
}
