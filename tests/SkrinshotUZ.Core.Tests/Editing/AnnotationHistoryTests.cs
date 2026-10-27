using System.Drawing;
using SkrinshotUZ.Core.Editing;

namespace SkrinshotUZ.Core.Tests.Editing;

/// <summary>Ctrl+Z / Ctrl+Y.</summary>
public class AnnotationHistoryTests
{
    private static RectangleAnnotation R(int x) => new(new Rectangle(x, 0, 10, 10), Color.Red, 2);

    [Fact]
    public void Empty_CannotUndoOrRedo()
    {
        var h = new AnnotationHistory();

        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
        Assert.Null(h.Undo());
        Assert.Null(h.Redo());
    }

    [Fact]
    public void UndoRedo_RestoresInOrder()
    {
        var h = new AnnotationHistory();
        var a = R(1);
        var b = R(2);
        var c = R(3);
        h.Add(a);
        h.Add(b);
        h.Add(c);

        Assert.Same(c, h.Undo());
        Assert.Same(b, h.Undo());
        Assert.Equal(new Annotation[] { a }, h.Items);
        Assert.True(h.CanRedo);

        Assert.Same(b, h.Redo());
        Assert.Same(c, h.Redo());
        Assert.Equal(new Annotation[] { a, b, c }, h.Items);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void AddAfterUndo_ClearsRedo()
    {
        var h = new AnnotationHistory();
        h.Add(R(1));
        h.Add(R(2));
        h.Undo();
        var d = R(4);
        h.Add(d);

        Assert.False(h.CanRedo);
        Assert.Null(h.Redo());
        Assert.Same(d, h.Items[^1]);
        Assert.Equal(2, h.Count);
    }

    [Fact]
    public void UndoAll_ThenRedoAll()
    {
        var h = new AnnotationHistory();
        for (int i = 0; i < 50; i++)
        {
            h.Add(R(i));
        }

        while (h.CanUndo)
        {
            h.Undo();
        }

        Assert.Equal(0, h.Count);
        while (h.CanRedo)
        {
            h.Redo();
        }

        Assert.Equal(50, h.Count);
        Assert.Equal(49, ((RectangleAnnotation)h.Items[^1]).Rect.X);
    }

    [Fact]
    public void Changed_RaisedOnEveryChange_NotOnNoOp()
    {
        var h = new AnnotationHistory();
        int count = 0;
        h.Changed += (_, _) => count++;

        h.Undo();       // hech narsa — hodisa yo'q
        h.Add(R(1));    // 1
        h.Undo();       // 2
        h.Redo();       // 3
        h.Redo();       // hech narsa

        Assert.Equal(3, count);
    }

    [Fact]
    public void AddNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AnnotationHistory().Add(null!));
    }
}
