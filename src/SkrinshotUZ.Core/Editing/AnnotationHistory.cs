namespace SkrinshotUZ.Core.Editing;

/// <summary>
/// Chizmalar ro'yxati va Ctrl+Z / Ctrl+Y tarixi. Yangi chizma qo'shilsa, "qaytarish" ro'yxati tozalanadi
/// (odatiy muharrirlardagidek).
/// </summary>
public sealed class AnnotationHistory
{
    private readonly List<Annotation> _items = new();
    private readonly Stack<Annotation> _redo = new();

    /// <summary>Ro'yxat o'zgardi.</summary>
    public event EventHandler? Changed;

    /// <summary>Chizmalar (chizilish tartibida).</summary>
    public IReadOnlyList<Annotation> Items => _items;

    /// <summary>Chizmalar soni.</summary>
    public int Count => _items.Count;

    /// <summary>Ctrl+Z mumkinmi.</summary>
    public bool CanUndo => _items.Count > 0;

    /// <summary>Ctrl+Y mumkinmi.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Chizma qo'shadi.</summary>
    /// <param name="annotation">Chizma.</param>
    public void Add(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        _items.Add(annotation);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Oxirgi chizmani olib tashlaydi.</summary>
    /// <returns>Olib tashlangan chizma yoki <c>null</c>.</returns>
    public Annotation? Undo()
    {
        if (_items.Count == 0)
        {
            return null;
        }

        var last = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        _redo.Push(last);
        Changed?.Invoke(this, EventArgs.Empty);
        return last;
    }

    /// <summary>Olib tashlangan chizmani qaytaradi.</summary>
    /// <returns>Qaytarilgan chizma yoki <c>null</c>.</returns>
    public Annotation? Redo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var item = _redo.Pop();
        _items.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
        return item;
    }
}
