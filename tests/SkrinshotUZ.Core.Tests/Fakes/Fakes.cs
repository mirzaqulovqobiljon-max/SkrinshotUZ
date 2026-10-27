using SkrinshotUZ.Core.Hotkeys;
using SkrinshotUZ.Core.Permissions;
using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Settings;

namespace SkrinshotUZ.Core.Tests.Fakes;

/// <summary>RegisterHotKey o'rnini bosuvchi soxta xizmat. <see cref="Taken"/> — "boshqa dastur egallagan" birikmalar.</summary>
public sealed class FakeHotkeyService : IHotkeyService
{
    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    public HashSet<Hotkey> Taken { get; } = new();

    public Dictionary<int, Hotkey> Registered { get; } = new();

    public int UnregisterCalls { get; private set; }

    public bool TryRegister(int id, Hotkey hotkey)
    {
        // Haqiqiy RegisterHotKey bir xil id bilan ikkinchi marta chaqirilsa xato beradi — bu yerda ham.
        if (Registered.ContainsKey(id))
        {
            throw new InvalidOperationException($"id={id} allaqachon ro'yxatda (avval Unregister qilinmagan).");
        }

        if (Taken.Contains(hotkey))
        {
            return false;
        }

        Registered[id] = hotkey;
        return true;
    }

    public void Unregister(int id)
    {
        UnregisterCalls++;
        Registered.Remove(id);
    }

    public void Press(int id) => Pressed?.Invoke(this, new HotkeyPressedEventArgs(id));

    public void Dispose()
    {
    }
}

/// <summary>Soxta past darajali hook.</summary>
public sealed class FakeKeyboardHook : IKeyboardHook
{
    public event EventHandler<KeyboardHookPressedEventArgs>? Pressed;

    public bool FailInstall { get; set; }

    public bool IsInstalled { get; private set; }

    public List<Hotkey> Watched { get; } = new();

    public int InstallCalls { get; private set; }

    public bool Install(IReadOnlyCollection<Hotkey> hotkeys)
    {
        InstallCalls++;
        Watched.Clear();
        if (FailInstall)
        {
            IsInstalled = false;
            return false;
        }

        Watched.AddRange(hotkeys);
        IsInstalled = true;
        return true;
    }

    public void Uninstall()
    {
        Watched.Clear();
        IsInstalled = false;
    }

    public void Press(Hotkey hotkey) => Pressed?.Invoke(this, new KeyboardHookPressedEventArgs(hotkey));

    public void Dispose()
    {
    }
}

/// <summary>Xotiradagi reyestr (HKCU). Har bir yozuv <see cref="Writes"/> ga qayd etiladi.</summary>
public sealed class InMemoryRegistry : IRegistryStore
{
    public Dictionary<(string SubKey, string Name), object> Values { get; } = new();

    public List<string> Writes { get; } = new();

    /// <summary>Administrator cheklovini taqlid qiladi.</summary>
    public bool DenyWrites { get; set; }

    public object? GetValue(string subKey, string name) =>
        Values.TryGetValue((subKey, name), out var v) ? v : null;

    public void SetDword(string subKey, string name, int value)
    {
        Write($"SetDword {subKey}\\{name}={value}");
        Values[(subKey, name)] = value;
    }

    public void SetString(string subKey, string name, string value)
    {
        Write($"SetString {subKey}\\{name}={value}");
        Values[(subKey, name)] = value;
    }

    public void DeleteValue(string subKey, string name)
    {
        Write($"Delete {subKey}\\{name}");
        Values.Remove((subKey, name));
    }

    private void Write(string entry)
    {
        if (DenyWrites)
        {
            throw new UnauthorizedAccessException("Ruxsat yo'q (test).");
        }

        Writes.Add(entry);
    }
}

/// <summary>Soxta oynalar: javoblar oldindan beriladi, har bir chaqiruv qayd etiladi.</summary>
public sealed class FakePrompts : IUserPrompts
{
    public PrintScreenAnswer PrintScreenAnswer { get; set; } = new(PermissionChoice.Allow, false);

    public bool DisableSnippingAnswer { get; set; }

    public bool RestoreSnippingAnswer { get; set; }

    public List<bool> PrintScreenAsked { get; } = new();

    public int SnippingAsked { get; private set; }

    public int RestoreAsked { get; private set; }

    public int DisabledShown { get; private set; }

    public List<string> Warnings { get; } = new();

    public PrintScreenAnswer AskPrintScreenPermission(bool offerAutostart)
    {
        PrintScreenAsked.Add(offerAutostart);
        return PrintScreenAnswer;
    }

    public bool AskDisableSnippingTool()
    {
        SnippingAsked++;
        return DisableSnippingAnswer;
    }

    public bool AskRestoreSnippingTool()
    {
        RestoreAsked++;
        return RestoreSnippingAnswer;
    }

    public void ShowSnippingToolDisabled() => DisabledShown++;

    public void ShowWarning(string message) => Warnings.Add(message);
}

/// <summary>Xotiradagi sozlamalar ombori (diskka o'xshab — nusxa saqlaydi).</summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    public AppSettings? Stored { get; set; }

    public int SaveCalls { get; private set; }

    public bool FailSave { get; set; }

    public AppSettings Load() => Stored?.Clone() ?? new AppSettings();

    public void Save(AppSettings settings)
    {
        SaveCalls++;
        if (FailSave)
        {
            throw new IOException("Disk to'la (test).");
        }

        Stored = settings.Clone();
    }
}
