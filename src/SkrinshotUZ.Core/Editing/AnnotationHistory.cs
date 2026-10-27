namespace SkrinshotUZ.Core.Editing;

/// <summary>
/// Chizmalar ro'yxati va Ctrl+Z / Ctrl+Y tarixi. Har bir o'zgarish — buyruq (qo'shish, almashtirish, o'chirish),
/// shuning uchun keyin siljitilgan yoki cho'zilgan strelka ham bir qadamda bekor qilinadi.
/// Yangi o'zgarish "qaytarish" ro'yxatini tozalaydi (odatiy muharrirlardagidek).
/// </summary>
public sealed class AnnotationHistory
{
    private readonly List<Annotation> _items = new();
    private readonly Stack<Command> _undo = new();
    private readonly Stack<Command> _redo = new();

    /// <summary>Ro'yxat o'zgardi.</summary>
    public event EventHandler? Changed;

    /// <summary>Chizmalar (chizilish tartibida).</summary>
    public IReadOnlyList<Annotation> Items => _items;

    /// <summary>Chizmalar soni.</summary>
    public int Count => _items.Count;

    /// <summary>Ctrl+Z mumkinmi.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Ctrl+Y mumkinmi.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Chizma qo'shadi (oxiriga).</summary>
    /// <param name="annotation">Chizma.</param>
    public void Add(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        Do(new Command(CommandKind.Add, _items.Count, null, annotation));
    }

    /// <summary>Chizmani yangisi bilan almashtiradi (siljitish, cho'zish, matnni tahrirlash).</summary>
    /// <param name="index">O'rni.</param>
    /// <param name="annotation">Yangi ko'rinishi.</param>
    public void Replace(int index, Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        CheckIndex(index);
        if (ReferenceEquals(_items[index], annotation) || _items[index] == annotation)
        {
            return; // o'zgarmagan — tarixga yozilmaydi
        }

        Do(new Command(CommandKind.Replace, index, _items[index], annotation));
    }

    /// <summary>Chizmani o'chiradi.</summary>
    /// <param name="index">O'rni.</param>
    public void RemoveAt(int index)
    {
        CheckIndex(index);
        Do(new Command(CommandKind.Remove, index, _items[index], null));
    }

    /// <summary>Oxirgi o'zgarishni bekor qiladi.</summary>
    /// <returns>Ta'sir qilgan chizma (qo'shish uchun — olib tashlangani, boshqalar uchun — qaytarilgan eski holat) yoki <c>null</c>.</returns>
    public Annotation? Undo()
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        var c = _undo.Pop();
        Annotation? affected;
        switch (c.Kind)
        {
            case CommandKind.Add:
                affected = _items[c.Index];
                _items.RemoveAt(c.Index);
                break;
            case CommandKind.Replace:
                _items[c.Index] = c.Before!;
                affected = c.Before;
                break;
            default:
                _items.Insert(c.Index, c.Before!);
                affected = c.Before;
                break;
        }

        _redo.Push(c);
        Changed?.Invoke(this, EventArgs.Empty);
        return affected;
    }

    /// <summary>Bekor qilingan o'zgarishni qaytaradi.</summary>
    /// <returns>Ta'sir qilgan chizma yoki <c>null</c>.</returns>
    public Annotation? Redo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var c = _redo.Pop();
        Apply(c);
        _undo.Push(c);
        Changed?.Invoke(this, EventArgs.Empty);
        return c.After ?? c.Before;
    }

    private void Do(Command c)
    {
        Apply(c);
        _undo.Push(c);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(Command c)
    {
        switch (c.Kind)
        {
            case CommandKind.Add:
                _items.Insert(c.Index, c.After!);
                break;
            case CommandKind.Replace:
                _items[c.Index] = c.After!;
                break;
            default:
                _items.RemoveAt(c.Index);
                break;
        }
    }

    private void CheckIndex(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Bunday chizma yo'q.");
        }
    }

    private enum CommandKind
    {
        Add,
        Replace,
        Remove,
    }

    private sealed record Command(CommandKind Kind, int Index, Annotation? Before, Annotation? After);
}
