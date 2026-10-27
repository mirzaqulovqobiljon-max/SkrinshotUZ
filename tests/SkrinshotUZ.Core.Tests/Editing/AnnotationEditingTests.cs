using System.Drawing;
using SkrinshotUZ.Core.Editing;

namespace SkrinshotUZ.Core.Tests.Editing;

/// <summary>
/// Chizilgandan keyin tahrirlash: strelka/chiziq uchlarini sudrash, to'rtburchak burchaklari, siljitish,
/// o'chirish, matnni qayta tahrirlash — barchasi Ctrl+Z bilan bekor qilinadi.
/// </summary>
public class AnnotationEditingTests
{
    private const int Tol = 6;

    private readonly AnnotationHistory _history = new();

    private EditorController Editor(EditorTool tool = EditorTool.None) => new(_history) { Tool = tool };

    private static void DrawShape(EditorController e, Point from, Point to)
    {
        e.Begin(from, 1.0);
        e.End(to, false);
    }

    private static void Drag(EditorController e, Point from, Point to, bool shift = false)
    {
        Assert.True(e.TryBeginEdit(from, Tol), $"{from} nuqtada chizma ushlanmadi");
        e.Edit(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), shift);
        e.EndEdit(to, shift);
    }

    // ---------- Geometriya ----------

    [Fact]
    public void LineHandles_AreBothEnds()
    {
        var l = new LineAnnotation(new Point(10, 20), new Point(110, 70), Color.Red, 4, true);

        Assert.Equal(new[] { (AnnotationHandle.Start, new Point(10, 20)), (AnnotationHandle.End, new Point(110, 70)) }, AnnotationGeometry.Handles(l));
    }

    [Fact]
    public void RectangleHandles_AreFourCorners()
    {
        var r = new RectangleAnnotation(new Rectangle(10, 20, 100, 50), Color.Red, 4);

        Assert.Equal(4, AnnotationGeometry.Handles(r).Count);
        Assert.Contains((AnnotationHandle.BottomRight, new Point(110, 70)), AnnotationGeometry.Handles(r));
    }

    [Fact]
    public void StrokeAndText_HaveNoHandles_OnlyMove()
    {
        Assert.Empty(AnnotationGeometry.Handles(new StrokeAnnotation(new[] { new Point(1, 1) }, Color.Red, 4, false)));
        Assert.Empty(AnnotationGeometry.Handles(new TextAnnotation(new Point(1, 1), "a", Color.Red, 16, new Size(10, 10))));
    }

    [Theory]
    [InlineData(10, 20, AnnotationHandle.Start)]
    [InlineData(13, 17, AnnotationHandle.Start)]
    [InlineData(110, 70, AnnotationHandle.End)]
    [InlineData(60, 45, AnnotationHandle.Body)]   // chiziq o'rtasi
    [InlineData(60, 70, AnnotationHandle.None)]   // chiziqdan uzoq
    public void Line_HitTest_WithHandles(int x, int y, AnnotationHandle expected)
    {
        var l = new LineAnnotation(new Point(10, 20), new Point(110, 70), Color.Red, 4, false);

        Assert.Equal(expected, AnnotationGeometry.HitTest(l, new Point(x, y), Tol, withHandles: true));
    }

    [Fact]
    public void HitTest_WithoutHandles_EndsAreBody()
    {
        var l = new LineAnnotation(new Point(10, 20), new Point(110, 70), Color.Red, 4, false);

        Assert.Equal(AnnotationHandle.Body, AnnotationGeometry.HitTest(l, new Point(10, 20), Tol, withHandles: false));
    }

    [Fact]
    public void ShortArrow_NearestEndWins()
    {
        // Juda qisqa strelka: ikki uch ham radius ichida — eng yaqini tanlanadi.
        var l = new LineAnnotation(new Point(100, 100), new Point(106, 100), Color.Red, 4, true);

        Assert.Equal(AnnotationHandle.Start, AnnotationGeometry.HitTest(l, new Point(101, 100), Tol, true));
        Assert.Equal(AnnotationHandle.End, AnnotationGeometry.HitTest(l, new Point(106, 101), Tol, true));
    }

    [Fact]
    public void Rectangle_BorderIsBody_InteriorIsFree()
    {
        var r = new RectangleAnnotation(new Rectangle(100, 100, 200, 100), Color.Red, 4);

        Assert.Equal(AnnotationHandle.Body, AnnotationGeometry.HitTest(r, new Point(200, 102), Tol, false));
        Assert.Equal(AnnotationHandle.Body, AnnotationGeometry.HitTest(r, new Point(299, 150), Tol, false));
        Assert.Equal(AnnotationHandle.None, AnnotationGeometry.HitTest(r, new Point(200, 150), Tol, false)); // ichiga chizish mumkin
        Assert.Equal(AnnotationHandle.TopLeft, AnnotationGeometry.HitTest(r, new Point(102, 99), Tol, true));
    }

    [Fact]
    public void Stroke_HitAlongPath_Text_HitInsideBox()
    {
        var s = new StrokeAnnotation(new[] { new Point(0, 0), new Point(100, 0), new Point(100, 100) }, Color.Red, 4, false);
        var t = new TextAnnotation(new Point(50, 50), "Salom", Color.Red, 20, new Size(60, 25));

        Assert.Equal(AnnotationHandle.Body, AnnotationGeometry.HitTest(s, new Point(100, 50), Tol, false));
        Assert.Equal(AnnotationHandle.None, AnnotationGeometry.HitTest(s, new Point(50, 50), Tol, false));
        Assert.Equal(AnnotationHandle.Body, AnnotationGeometry.HitTest(t, new Point(80, 60), Tol, false));
        Assert.Equal(AnnotationHandle.None, AnnotationGeometry.HitTest(t, new Point(200, 60), Tol, false));
    }

    [Fact]
    public void Move_ShiftsEveryKind()
    {
        Assert.Equal(new Point(15, 27), ((LineAnnotation)AnnotationGeometry.Move(new LineAnnotation(new Point(10, 20), new Point(0, 0), Color.Red, 4, true), 5, 7)).Start);
        Assert.Equal(new Rectangle(15, 27, 10, 10), ((RectangleAnnotation)AnnotationGeometry.Move(new RectangleAnnotation(new Rectangle(10, 20, 10, 10), Color.Red, 4), 5, 7)).Rect);
        Assert.Equal(new[] { new Point(5, 7), new Point(6, 8) }, ((StrokeAnnotation)AnnotationGeometry.Move(new StrokeAnnotation(new[] { new Point(0, 0), new Point(1, 1) }, Color.Red, 4, false), 5, 7)).Points);
        Assert.Equal(new Point(5, 7), ((TextAnnotation)AnnotationGeometry.Move(new TextAnnotation(Point.Empty, "a", Color.Red, 16, Size.Empty), 5, 7)).Location);
    }

    [Fact]
    public void DragHandle_RectangleCorner_KeepsOppositeCorner_ShiftMakesSquare()
    {
        var r = new RectangleAnnotation(new Rectangle(100, 100, 200, 100), Color.Red, 4);

        Assert.Equal(new Rectangle(50, 100, 250, 150), ((RectangleAnnotation)AnnotationGeometry.DragHandle(r, AnnotationHandle.BottomLeft, new Point(50, 250), false)).Rect);
        Assert.Equal(new Rectangle(100, 100, 50, 50), ((RectangleAnnotation)AnnotationGeometry.DragHandle(r, AnnotationHandle.BottomRight, new Point(130, 150), true)).Rect);
        // Qarama-qarshi tomondan o'tsa — ag'dariladi, manfiy o'lcham bo'lmaydi.
        Assert.Equal(new Rectangle(300, 200, 100, 50), ((RectangleAnnotation)AnnotationGeometry.DragHandle(r, AnnotationHandle.TopLeft, new Point(400, 250), false)).Rect);
    }

    // ---------- Foydalanuvchi oqimi (EditorController) ----------

    [Fact]
    public void NewArrow_IsSelectedImmediately_BothEndsCanBeStretched()
    {
        var e = Editor(EditorTool.Arrow);
        DrawShape(e, new Point(100, 100), new Point(200, 100));

        Assert.Equal(0, e.SelectedIndex);

        Drag(e, new Point(200, 100), new Point(300, 150));   // uchini cho'zish
        Drag(e, new Point(100, 100), new Point(50, 50));     // boshini cho'zish

        var a = Assert.IsType<LineAnnotation>(Assert.Single(_history.Items));
        Assert.Equal(new Point(50, 50), a.Start);
        Assert.Equal(new Point(300, 150), a.End);
        Assert.True(a.HasArrowHead);
    }

    [Fact]
    public void ArrowEndWithShift_SnapsAroundOtherEnd()
    {
        var e = Editor(EditorTool.Arrow);
        DrawShape(e, new Point(100, 100), new Point(200, 100));
        Drag(e, new Point(200, 100), new Point(203, 180), shift: true);

        // (103, 80) siljish ≈ 38° → eng yaqin 45° diagonal, uzunlik = max(103, 80).
        Assert.Equal(new Point(203, 203), ((LineAnnotation)_history.Items[0]).End);
    }

    [Fact]
    public void ArrowMiddle_MovesWholeArrow()
    {
        var e = Editor(EditorTool.Arrow);
        DrawShape(e, new Point(100, 100), new Point(200, 100));
        Drag(e, new Point(150, 100), new Point(160, 130));

        var a = (LineAnnotation)_history.Items[0];
        Assert.Equal(new Point(110, 130), a.Start);
        Assert.Equal(new Point(210, 130), a.End);
    }

    [Fact]
    public void EachEdit_IsOneUndoStep()
    {
        var e = Editor(EditorTool.Arrow);
        DrawShape(e, new Point(100, 100), new Point(200, 100));
        Drag(e, new Point(200, 100), new Point(300, 150));
        Drag(e, new Point(150, 110), new Point(150, 160));

        _history.Undo(); // siljitish bekor
        Assert.Equal(new Point(300, 150), ((LineAnnotation)_history.Items[0]).End);
        _history.Undo(); // cho'zish bekor
        Assert.Equal(new Point(200, 100), ((LineAnnotation)_history.Items[0]).End);
        _history.Undo(); // chizish bekor
        Assert.Equal(0, _history.Count);

        _history.Redo();
        _history.Redo();
        Assert.Equal(new Point(300, 150), ((LineAnnotation)_history.Items[0]).End);
    }

    [Fact]
    public void ClickWithoutMoving_DoesNotCreateUndoStep()
    {
        var e = Editor(EditorTool.Rectangle);
        DrawShape(e, new Point(100, 100), new Point(200, 200));
        Assert.True(e.TryBeginEdit(new Point(100, 150), Tol));
        e.EndEdit(new Point(100, 150), false);

        _history.Undo();
        Assert.Equal(0, _history.Count); // bitta "Undo" — chizishni bekor qildi, bo'sh tahrir yozilmagan
    }

    [Fact]
    public void ClickOnPenStrokeWithoutMoving_DoesNotCreateUndoStep()
    {
        // Qalam izining nuqtalari ro'yxat — "teng" tekshiruvi bu yerda yordam bermaydi, shuning uchun alohida test.
        var e = Editor(EditorTool.Pen);
        e.Begin(new Point(10, 10), 1.0);
        e.Move(new Point(60, 10), false);
        e.End(new Point(110, 10), false);
        Assert.True(e.TryBeginEdit(new Point(60, 10), Tol));
        e.EndEdit(new Point(60, 10), false);

        _history.Undo();
        Assert.Equal(0, _history.Count);
    }

    [Fact]
    public void StretchingArrowToZeroLength_IsRejected()
    {
        var e = Editor(EditorTool.Arrow);
        DrawShape(e, new Point(100, 100), new Point(200, 100));
        Drag(e, new Point(200, 100), new Point(100, 100));

        Assert.Equal(new Point(200, 100), ((LineAnnotation)_history.Items[0]).End);
    }

    [Fact]
    public void EditPreview_IsLive_HistoryUnchangedUntilRelease()
    {
        var e = Editor(EditorTool.Line);
        DrawShape(e, new Point(0, 0), new Point(100, 0));
        Assert.True(e.TryBeginEdit(new Point(100, 0), Tol));
        e.Edit(new Point(100, 80), false);

        Assert.True(e.IsEditing);
        Assert.Equal(new Point(100, 80), ((LineAnnotation)e.EditPreview!).End);
        Assert.Equal(new Point(100, 0), ((LineAnnotation)_history.Items[0]).End);

        e.EndEdit(new Point(100, 80), false);
        Assert.False(e.IsEditing);
        Assert.Null(e.EditPreview);
    }

    [Fact]
    public void WithNoTool_ClickOnAnyAnnotation_SelectsTopmost()
    {
        var e = Editor(EditorTool.Rectangle);
        DrawShape(e, new Point(100, 100), new Point(200, 200));
        e.Tool = EditorTool.Line;
        DrawShape(e, new Point(100, 150), new Point(300, 150)); // to'rtburchak chetini kesib o'tadi
        e.Tool = EditorTool.None;

        Assert.Equal(-1, e.SelectedIndex); // asbob almashganda tanlov olib tashlanadi
        Assert.True(e.TryBeginEdit(new Point(100, 150), Tol));
        Assert.Equal(1, e.SelectedIndex); // ustidagi — chiziq
        e.EndEdit(new Point(100, 150), false);

        Assert.True(e.TryBeginEdit(new Point(150, 100), Tol));
        Assert.Equal(0, e.SelectedIndex); // to'rtburchakning yuqori cheti
    }

    [Fact]
    public void WithPenTool_OtherAnnotationsAreNotGrabbed_FreehandOverThemWorks()
    {
        var e = Editor(EditorTool.Rectangle);
        DrawShape(e, new Point(100, 100), new Point(200, 200));
        e.Deselect();
        e.Tool = EditorTool.Pen;

        Assert.False(e.TryBeginEdit(new Point(100, 150), Tol));
    }

    [Fact]
    public void EmptyArea_IsNotGrabbed()
    {
        var e = Editor(EditorTool.Arrow);
        DrawShape(e, new Point(100, 100), new Point(200, 100));

        Assert.False(e.TryBeginEdit(new Point(500, 500), Tol));
    }

    [Fact]
    public void Delete_RemovesSelected_UndoRestoresAtSamePlace()
    {
        var e = Editor(EditorTool.Line);
        DrawShape(e, new Point(0, 0), new Point(100, 0));
        DrawShape(e, new Point(0, 50), new Point(100, 50));
        DrawShape(e, new Point(0, 90), new Point(100, 90));
        e.Tool = EditorTool.None;
        Assert.True(e.TryBeginEdit(new Point(50, 50), Tol));
        e.EndEdit(new Point(50, 50), false);

        Assert.True(e.DeleteSelected());
        Assert.Equal(2, _history.Count);
        Assert.Equal(-1, e.SelectedIndex);
        Assert.False(e.DeleteSelected());

        _history.Undo();
        Assert.Equal(new Point(0, 50), ((LineAnnotation)_history.Items[1]).Start);
    }

    [Fact]
    public void ReplaceText_UpdatesTextKeepsStyle_EmptyRemoves()
    {
        var e = Editor(EditorTool.Text);
        e.Color = Color.Blue;
        e.CommitText(new Point(10, 10), "Eski", new Size(40, 20), 20f);

        var updated = e.ReplaceText(0, "Yangi\r\nmatn", new Size(60, 44));
        Assert.NotNull(updated);
        Assert.Equal("Yangi\nmatn", updated.Text);
        Assert.Equal(Color.Blue, updated.Color);
        Assert.Equal(new Point(10, 10), updated.Location);
        Assert.Equal(new Size(60, 44), updated.Size);

        Assert.Null(e.ReplaceText(0, "   ", Size.Empty));
        Assert.Equal(0, _history.Count);

        _history.Undo();
        Assert.Equal("Yangi\nmatn", ((TextAnnotation)_history.Items[0]).Text);
    }

    [Fact]
    public void ReplaceText_NotText_Throws()
    {
        var e = Editor(EditorTool.Line);
        DrawShape(e, new Point(0, 0), new Point(100, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => e.ReplaceText(0, "x", Size.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => e.ReplaceText(5, "x", Size.Empty));
    }

    [Fact]
    public void SelectionClearedWhenIndexDisappears_AfterUndo()
    {
        var e = Editor(EditorTool.Line);
        DrawShape(e, new Point(0, 0), new Point(100, 0));
        _history.Undo();

        Assert.Equal(-1, e.SelectedIndex);
        Assert.Null(e.Selected);
    }

    // ---------- Tarix: almashtirish va o'chirish ----------

    [Fact]
    public void History_ReplaceAndRemove_UndoRedo()
    {
        var a = new RectangleAnnotation(new Rectangle(0, 0, 10, 10), Color.Red, 2);
        var b = a with { Rect = new Rectangle(5, 5, 10, 10) };
        var c = new RectangleAnnotation(new Rectangle(50, 0, 10, 10), Color.Red, 2);
        var h = new AnnotationHistory();
        h.Add(a);
        h.Add(c);
        h.Replace(0, b);
        h.RemoveAt(1);

        Assert.Equal(new Annotation[] { b }, h.Items);
        Assert.Same(c, h.Undo());
        Assert.Equal(new Annotation[] { b, c }, h.Items);
        Assert.Same(a, h.Undo());
        Assert.Equal(new Annotation[] { a, c }, h.Items);
        h.Redo();
        h.Redo();
        Assert.Equal(new Annotation[] { b }, h.Items);
    }

    [Fact]
    public void History_ReplaceWithEqual_IsNoOp()
    {
        var a = new RectangleAnnotation(new Rectangle(0, 0, 10, 10), Color.Red, 2);
        var h = new AnnotationHistory();
        h.Add(a);
        h.Replace(0, a with { });

        h.Undo();
        Assert.Equal(0, h.Count);
    }

    [Fact]
    public void History_BadIndex_Throws()
    {
        var h = new AnnotationHistory();

        Assert.Throws<ArgumentOutOfRangeException>(() => h.RemoveAt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Replace(-1, new RectangleAnnotation(Rectangle.Empty, Color.Red, 1)));
    }
}
