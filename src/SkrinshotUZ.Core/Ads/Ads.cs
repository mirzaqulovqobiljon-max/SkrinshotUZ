using System.Text.Json;

namespace SkrinshotUZ.Core.Ads;

/// <summary>Bitta reklama (serverdagi <c>active_ads()</c> natijasi).</summary>
/// <param name="Id">Identifikator (statistika uchun).</param>
/// <param name="Title">Sarlavha (oynada ko'rsatiladi).</param>
/// <param name="ImageUrl">Rasm manzili (faqat https; yo'q bo'lishi mumkin).</param>
/// <param name="LinkUrl">"Batafsil" havolasi (faqat https; yo'q bo'lishi mumkin).</param>
/// <param name="Priority">Ustuvorlik (server tartiblaydi).</param>
public sealed record AdItem(Guid Id, string Title, string? ImageUrl, string? LinkUrl, int Priority);

/// <summary>Statistika hodisasi.</summary>
public enum AdEventKind
{
    /// <summary>Reklama ko'rsatildi.</summary>
    View,

    /// <summary>"Batafsil" bosildi.</summary>
    Click,
}

/// <summary>Reklama oynasi qanday yopildi.</summary>
public enum AdOutcome
{
    /// <summary>"Batafsil" bosildi (havola brauzerda ochildi).</summary>
    Clicked,

    /// <summary>"Yopish" bosildi.</summary>
    Dismissed,

    /// <summary>Hech narsa bosilmadi — vaqt o'tib o'zi yopildi.</summary>
    TimedOut,
}

/// <summary>Bir marta ishga tushirish natijasi (log va testlar uchun).</summary>
public enum AdRunResult
{
    /// <summary>Bugun allaqachon ko'rsatilgan.</summary>
    AlreadyShownToday,

    /// <summary>Reklama tizimi sozlanmagan (Supabase manzili yo'q) — internetga chiqilmaydi.</summary>
    NotConfigured,

    /// <summary>Internet yo'q yoki server javob bermadi — jim o'tib ketildi.</summary>
    Offline,

    /// <summary>Hozir faol reklama yo'q.</summary>
    NoAds,

    /// <summary>Reklama ko'rsatildi.</summary>
    Shown,
}

/// <summary>Server javobini o'qish: buzilgan yoki xavfli yozuvlar tashlab yuboriladi, dastur hech qachon yiqilmaydi.</summary>
public static class AdFeed
{
    /// <summary>Ro'yxatdagi eng ko'p reklama.</summary>
    public const int MaxAds = 20;

    /// <summary>Sarlavhaning eng katta uzunligi (oynaga sig'ishi uchun).</summary>
    public const int MaxTitleLength = 120;

    /// <summary>JSON massivni o'qiydi.</summary>
    /// <param name="json">Server javobi.</param>
    /// <returns>Yaroqli reklamalar (server tartibida).</returns>
    /// <exception cref="JsonException">JSON umuman o'qilmasa.</exception>
    public static IReadOnlyList<AdItem> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Reklamalar ro'yxati massiv bo'lishi kerak.");
        }

        var result = new List<AdItem>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (result.Count >= MaxAds)
            {
                break;
            }

            if (e.ValueKind != JsonValueKind.Object
                || !TryString(e, "id", out var idText) || !Guid.TryParse(idText, out var id)
                || !TryString(e, "title", out var title) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            title = title.Trim();
            if (title.Length > MaxTitleLength)
            {
                title = title[..(MaxTitleLength - 1)].TrimEnd() + "…";
            }

            int priority = e.TryGetProperty("priority", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int v) ? v : 0;
            result.Add(new AdItem(
                id,
                title,
                SafeUrl(TryString(e, "image_url", out var img) ? img : null),
                SafeUrl(TryString(e, "link_url", out var link) ? link : null),
                priority));
        }

        return result;
    }

    /// <summary>Faqat to'liq https manzil qabul qilinadi (http, file:, javascript: va boshqalar — yo'q).</summary>
    /// <param name="url">Manzil.</param>
    /// <returns>Xavfsiz manzil yoki <c>null</c>.</returns>
    public static string? SafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048)
        {
            return null;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && !string.IsNullOrEmpty(uri.Host)
               && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : null;
    }

    private static bool TryString(JsonElement e, string name, out string value)
    {
        if (e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
        {
            value = p.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

/// <summary>Qaysi reklama va qachon.</summary>
public static class AdSchedule
{
    /// <summary>Kuniga bir marta: bugun hali ko'rsatilmagan bo'lsa.</summary>
    /// <param name="lastShownOn">Oxirgi ko'rsatilgan kun.</param>
    /// <param name="today">Bugun (kompyuter sanasi).</param>
    /// <returns>Ko'rsatish kerakmi.</returns>
    public static bool IsDue(DateOnly? lastShownOn, DateOnly today) => lastShownOn != today;

    /// <summary>
    /// Navbatdagi reklama: oxirgi ko'rsatilganidan keyingisi (aylanma) — har kuni boshqa reklama chiqadi.
    /// Oxirgisi ro'yxatda bo'lmasa (o'chirilgan) — birinchisi (eng ustuvori).
    /// </summary>
    /// <param name="ads">Server tartibidagi reklamalar.</param>
    /// <param name="lastAdId">Oxirgi ko'rsatilgan reklama.</param>
    /// <returns>Reklama yoki <c>null</c> (ro'yxat bo'sh).</returns>
    public static AdItem? Pick(IReadOnlyList<AdItem> ads, Guid? lastAdId)
    {
        ArgumentNullException.ThrowIfNull(ads);
        if (ads.Count == 0)
        {
            return null;
        }

        int last = lastAdId is Guid id ? IndexOf(ads, id) : -1;
        return ads[(last + 1) % ads.Count];
    }

    private static int IndexOf(IReadOnlyList<AdItem> ads, Guid id)
    {
        for (int i = 0; i < ads.Count; i++)
        {
            if (ads[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>Reklama serveri (Windows'da — Supabase; testlarda soxta).</summary>
public interface IAdClient
{
    /// <summary>Faol reklamalar.</summary>
    Task<IReadOnlyList<AdItem>> GetActiveAdsAsync(CancellationToken cancellationToken);

    /// <summary>Rasm baytlari (yaroqsiz yoki juda katta bo'lsa — <c>null</c>).</summary>
    Task<byte[]?> GetImageAsync(string url, CancellationToken cancellationToken);

    /// <summary>"Ko'rildi"/"bosildi" hisoblagichi (faqat reklama ID'si va hodisa turi yuboriladi).</summary>
    Task RecordAsync(Guid adId, AdEventKind kind, CancellationToken cancellationToken);
}

/// <summary>Reklama oynasi (Windows'da — burchakdagi kichik oyna; testlarda soxta).</summary>
public interface IAdPresenter
{
    /// <summary>Ko'rsatadi va yopilishini kutadi. "Batafsil" bosilsa havolani o'zi ochadi.</summary>
    Task<AdOutcome> ShowAsync(AdItem ad, byte[]? image);
}

/// <summary>Eslab qolinadigan holat (sozlamalarda).</summary>
public interface IAdState
{
    /// <summary>Oxirgi ko'rsatilgan kun.</summary>
    DateOnly? LastShownOn { get; set; }

    /// <summary>Oxirgi ko'rsatilgan reklama.</summary>
    Guid? LastAdId { get; set; }

    /// <summary>Saqlash.</summary>
    void Save();
}

/// <summary>
/// Kuniga bir marta: ro'yxatni oladi → navbatdagi reklamani tanlaydi → "bugun ko'rsatildi" deb belgilaydi →
/// ko'rsatadi → statistikani yuboradi. Har qanday tarmoq xatosi jim yutiladi: reklama hech qachon dastur ishiga xalaqit bermaydi.
/// </summary>
public sealed class AdCoordinator
{
    private readonly IAdClient? _client;
    private readonly IAdPresenter _presenter;
    private readonly IAdState _state;
    private readonly Func<DateOnly> _today;

    /// <summary>Yangi koordinator.</summary>
    /// <param name="client">Server (<c>null</c> — reklama sozlanmagan, internetga chiqilmaydi).</param>
    /// <param name="presenter">Oyna.</param>
    /// <param name="state">Holat.</param>
    /// <param name="today">Bugungi sana.</param>
    public AdCoordinator(IAdClient? client, IAdPresenter presenter, IAdState state, Func<DateOnly> today)
    {
        _client = client;
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _today = today ?? throw new ArgumentNullException(nameof(today));
    }

    /// <summary>Kerak bo'lsa reklamani ko'rsatadi.</summary>
    /// <param name="cancellationToken">Bekor qilish (dastur yopilmoqda).</param>
    /// <returns>Natija.</returns>
    public async Task<AdRunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return AdRunResult.NotConfigured;
        }

        var today = _today();
        if (!AdSchedule.IsDue(_state.LastShownOn, today))
        {
            return AdRunResult.AlreadyShownToday;
        }

        IReadOnlyList<AdItem> ads;
        try
        {
            ads = await _client.GetActiveAdsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            return AdRunResult.Offline;
        }

        var ad = AdSchedule.Pick(ads, _state.LastAdId);
        if (ad is null)
        {
            return AdRunResult.NoAds;
        }

        byte[]? image = null;
        if (ad.ImageUrl is not null)
        {
            try
            {
                image = await _client.GetImageAsync(ad.ImageUrl, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
            {
                image = null; // rasmsiz ham ko'rsatiladi (sarlavha + havola)
            }
        }

        // Ko'rsatishdan OLDIN belgilanadi: oyna paytida dastur yopilsa ham bugun qayta chiqmaydi.
        _state.LastShownOn = today;
        _state.LastAdId = ad.Id;
        _state.Save();

        await RecordQuietlyAsync(ad.Id, AdEventKind.View, cancellationToken).ConfigureAwait(true);
        var outcome = await _presenter.ShowAsync(ad, image).ConfigureAwait(true);
        if (outcome == AdOutcome.Clicked)
        {
            await RecordQuietlyAsync(ad.Id, AdEventKind.Click, cancellationToken).ConfigureAwait(true);
        }

        return AdRunResult.Shown;
    }

    private static bool IsNetworkError(Exception ex, CancellationToken token) =>
        ex is HttpRequestException or JsonException or IOException or InvalidDataException
        || (ex is OperationCanceledException && !token.IsCancellationRequested);

    private async Task RecordQuietlyAsync(Guid id, AdEventKind kind, CancellationToken token)
    {
        try
        {
            await _client!.RecordAsync(id, kind, token).ConfigureAwait(true);
        }
        catch (Exception ex) when (IsNetworkError(ex, token))
        {
            // Statistika yo'qolsa ham foydalanuvchiga ta'sir qilmaydi.
        }
    }
}

/// <summary><see cref="IAdState"/> — dastur sozlamalarida (settings.json).</summary>
public sealed class SettingsAdState : IAdState
{
    private readonly Settings.AppSettings _settings;
    private readonly Action _save;

    /// <summary>Yangi holat.</summary>
    /// <param name="settings">Sozlamalar.</param>
    /// <param name="save">Saqlash.</param>
    public SettingsAdState(Settings.AppSettings settings, Action save)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _save = save ?? throw new ArgumentNullException(nameof(save));
    }

    /// <inheritdoc />
    public DateOnly? LastShownOn
    {
        get => _settings.AdsLastShownOn;
        set => _settings.AdsLastShownOn = value;
    }

    /// <inheritdoc />
    public Guid? LastAdId
    {
        get => _settings.AdsLastAdId;
        set => _settings.AdsLastAdId = value;
    }

    /// <inheritdoc />
    public void Save() => _save();
}
