using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkrinshotUZ.Core.Settings;

/// <summary>Sozlamalarni o'qish/yozish.</summary>
public interface ISettingsStore
{
    /// <summary>O'qiydi. Fayl yo'q yoki buzilgan bo'lsa — standart sozlamalar.</summary>
    /// <returns>Sozlamalar.</returns>
    AppSettings Load();

    /// <summary>Yozadi.</summary>
    /// <param name="settings">Sozlamalar.</param>
    void Save(AppSettings settings);
}

/// <summary>
/// JSON faylda saqlash. Yozish xavfsiz: avval vaqtinchalik faylga, keyin almashtiriladi —
/// kompyuter yozish paytida o'chib qolsa ham fayl buzilmaydi. Buzilgan fayl <c>.bad</c> qilib saqlab qo'yiladi.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    /// <summary>Yangi ombor.</summary>
    /// <param name="path">settings.json to'liq yo'li.</param>
    public JsonSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>Fayl yo'li.</summary>
    public string Path => _path;

    /// <inheritdoc />
    public AppSettings Load()
    {
        string json;
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            json = File.ReadAllText(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            BackupCorruptFile();
            return new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? folder = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _path, overwrite: true);
    }

    private void BackupCorruptFile()
    {
        try
        {
            File.Move(_path, _path + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Zaxira nusxa olinmasa ham, dastur standart sozlamalar bilan ishlayveradi.
        }
    }
}
