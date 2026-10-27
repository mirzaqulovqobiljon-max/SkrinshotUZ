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
/// JSON fayl bilan xavfsiz ishlash: yozish avval vaqtinchalik faylga, keyin almashtiriladi —
/// kompyuter yozish paytida o'chib qolsa ham fayl buzilmaydi. Buzilgan fayl <c>.bad</c> qilib saqlab qo'yiladi
/// va standart qiymat qaytariladi (dastur ishlayveradi).
/// </summary>
internal static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Read<T>(string path, Func<T> fallback)
    {
        string json;
        try
        {
            if (!File.Exists(path))
            {
                return fallback();
            }

            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return fallback();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? fallback();
        }
        catch (JsonException)
        {
            try
            {
                File.Move(path, path + ".bad", overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Zaxira nusxa olinmasa ham, dastur standart qiymat bilan ishlayveradi.
            }

            return fallback();
        }
    }

    public static void Write<T>(string path, T value)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>Sozlamalar JSON faylda (<see cref="JsonFile"/> qoidalari bilan).</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
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
    public AppSettings Load() => JsonFile.Read(_path, () => new AppSettings());

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        JsonFile.Write(_path, settings);
    }
}
