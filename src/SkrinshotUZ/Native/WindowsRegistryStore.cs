using Microsoft.Win32;
using SkrinshotUZ.Core.Platform;

namespace SkrinshotUZ.Native;

/// <summary>HKEY_CURRENT_USER bilan ishlash (administrator huquqi kerak emas).</summary>
internal sealed class WindowsRegistryStore : IRegistryStore
{
    public object? GetValue(string subKey, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: false);
        return key?.GetValue(name);
    }

    public void SetDword(string subKey, string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    public void SetString(string subKey, string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string subKey, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
