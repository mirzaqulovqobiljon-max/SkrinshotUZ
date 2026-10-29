using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SkrinshotUZ.Core.Ads;

/// <summary>Supabase manzili va ommaviy kaliti (dasturga yig'ish vaqtida beriladi).</summary>
/// <param name="BaseUrl">Masalan, https://abcd.supabase.co.</param>
/// <param name="ApiKey">Ommaviy kalit: "anon" (eyJ…) yoki "publishable" (sb_publishable_…). Hech qachon service_role emas.</param>
public sealed record AdsEndpoint(Uri BaseUrl, string ApiKey)
{
    /// <summary>
    /// Sozlamani tekshiradi. Bo'sh yoki noto'g'ri bo'lsa — <c>null</c> (reklama o'chiq, dastur internetga umuman chiqmaydi).
    /// Maxfiy (service_role / sb_secret_) kalit berilsa ham <c>null</c>: u dasturga hech qachon tushmasligi kerak.
    /// </summary>
    /// <param name="url">Manzil.</param>
    /// <param name="key">Kalit.</param>
    /// <returns>Sozlama yoki <c>null</c>.</returns>
    public static AdsEndpoint? TryCreate(string? url, string? key)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        key = key.Trim();
        if (key.StartsWith("sb_secret_", StringComparison.Ordinal) || IsServiceRoleJwt(key))
        {
            return null;
        }

        return Uri.TryCreate(url.Trim().TrimEnd('/'), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? new AdsEndpoint(uri, key)
            : null;
    }

    /// <summary>Eski uslubdagi JWT kalit (yangi "publishable" kalit Authorization sarlavhasisiz yuboriladi).</summary>
    public bool IsJwtKey => ApiKey.StartsWith("eyJ", StringComparison.Ordinal);

    private static bool IsServiceRoleJwt(string key)
    {
        var parts = key.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("role", out var role) && role.GetString() == "service_role";
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Supabase bilan ishlash (faqat ommaviy kalit bilan): <c>active_ads()</c> o'qish, rasm yuklash, <c>record_ad_event()</c>.
/// Serverga faqat reklama ID'si va hodisa turi yuboriladi — kompyuter yoki foydalanuvchi haqida hech narsa.
/// </summary>
public sealed class SupabaseAdClient : IAdClient
{
    /// <summary>Ro'yxat javobining eng katta hajmi.</summary>
    public const int MaxFeedBytes = 256 * 1024;

    /// <summary>Rasmning eng katta hajmi (Supabase bucket cheklovi bilan bir xil).</summary>
    public const int MaxImageBytes = 2 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly AdsEndpoint _endpoint;

    /// <summary>Yangi mijoz.</summary>
    /// <param name="http">HttpClient (egasi — chaqiruvchi; Timeout o'sha yerda).</param>
    /// <param name="endpoint">Supabase.</param>
    public SupabaseAdClient(HttpClient http, AdsEndpoint endpoint)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdItem>> GetActiveAdsAsync(CancellationToken cancellationToken)
    {
        using var request = Rpc("active_ads", "{}");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        byte[] body = await ReadLimitedAsync(response.Content, MaxFeedBytes, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Reklamalar ro'yxati juda katta.");
        return AdFeed.Parse(Encoding.UTF8.GetString(body));
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetImageAsync(string url, CancellationToken cancellationToken)
    {
        if (AdFeed.SafeUrl(url) is not string safe)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, safe);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string? type = response.Content.Headers.ContentType?.MediaType;
        if (type is not ("image/png" or "image/jpeg"))
        {
            return null; // dastur faqat PNG/JPG ni ko'rsata oladi
        }

        return await ReadLimitedAsync(response.Content, MaxImageBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecordAsync(Guid adId, AdEventKind kind, CancellationToken cancellationToken)
    {
        string body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["p_ad_id"] = adId.ToString("D"),
            ["p_kind"] = kind == AdEventKind.Click ? "click" : "view",
        });
        using var request = Rpc("record_ad_event", body);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Javobni chegaradan oshmay o'qiydi (server noto'g'ri ishlasa ham xotira to'lib ketmaydi).</summary>
    private static async Task<byte[]?> ReadLimitedAsync(HttpContent content, int limit, CancellationToken token)
    {
        if (content.Headers.ContentLength > limit)
        {
            return null;
        }

        await using var stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, token).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private HttpRequestMessage Rpc(string function, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint.BaseUrl, "/rest/v1/rpc/" + function))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("apikey", _endpoint.ApiKey);
        if (_endpoint.IsJwtKey)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _endpoint.ApiKey);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }
}
