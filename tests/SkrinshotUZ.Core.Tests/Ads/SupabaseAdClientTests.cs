using System.Net;
using System.Text;
using System.Text.Json;
using SkrinshotUZ.Core.Ads;

namespace SkrinshotUZ.Core.Tests.Ads;

/// <summary>
/// HTTP so'rovlari aynan qanday ketishi: manzil, sarlavhalar, tana — va serverga shaxsiy ma'lumot yuborilmasligi.
/// Tarmoq o'rniga soxta handler.
/// </summary>
public class SupabaseAdClientTests
{
    private const string Publishable = "sb_publishable_abc123";

    // {"role":"anon"} va {"role":"service_role"} — imzo muhim emas, faqat payload o'qiladi.
    private static readonly string AnonJwt = Jwt("{\"iss\":\"supabase\",\"role\":\"anon\"}");
    private static readonly string ServiceJwt = Jwt("{\"iss\":\"supabase\",\"role\":\"service_role\"}");

    private static readonly Guid Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static string Jwt(string payload) =>
        "eyJhbGciOiJIUzI1NiJ9." + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".imzo";

    private static (SupabaseAdClient Client, FakeHandler Handler) Create(string key = Publishable)
    {
        var handler = new FakeHandler();
        var endpoint = AdsEndpoint.TryCreate("https://abcd.supabase.co/", key)!;
        return (new SupabaseAdClient(new HttpClient(handler), endpoint), handler);
    }

    [Fact]
    public async Task GetActiveAds_PostsRpc_WithPublishableKeyOnly()
    {
        var (client, h) = Create();
        h.Respond = _ => Json($$"""[{"id":"{{Id}}","title":"Kurs","link_url":"https://kurs.uz","image_url":null,"priority":1}]""");

        var ads = await client.GetActiveAdsAsync(CancellationToken.None);

        Assert.Equal("Kurs", Assert.Single(ads).Title);
        var r = Assert.Single(h.Requests);
        Assert.Equal(HttpMethod.Post, r.Method);
        Assert.Equal("https://abcd.supabase.co/rest/v1/rpc/active_ads", r.Url);
        Assert.Equal("{}", r.Body);
        Assert.Equal(Publishable, r.Headers["apikey"]);
        Assert.False(r.Headers.ContainsKey("Authorization")); // yangi kalit JWT emas — Authorization yuborilmaydi
    }

    [Fact]
    public async Task LegacyAnonJwt_SentAsBearerToo()
    {
        var (client, h) = Create(AnonJwt);
        h.Respond = _ => Json("[]");

        await client.GetActiveAdsAsync(CancellationToken.None);

        Assert.Equal("Bearer " + AnonJwt, h.Requests[0].Headers["Authorization"]);
        Assert.Equal(AnonJwt, h.Requests[0].Headers["apikey"]);
    }

    [Theory]
    [InlineData(AdEventKind.View, "view")]
    [InlineData(AdEventKind.Click, "click")]
    public async Task Record_SendsOnlyAdIdAndKind(AdEventKind kind, string expected)
    {
        var (client, h) = Create();
        h.Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        await client.RecordAsync(Id, kind, CancellationToken.None);

        var r = Assert.Single(h.Requests);
        Assert.Equal("https://abcd.supabase.co/rest/v1/rpc/record_ad_event", r.Url);
        using var body = JsonDocument.Parse(r.Body!);
        var props = body.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "p_ad_id", "p_kind" }, props); // boshqa hech narsa: na kompyuter, na foydalanuvchi
        Assert.Equal(Id.ToString(), body.RootElement.GetProperty("p_ad_id").GetString());
        Assert.Equal(expected, body.RootElement.GetProperty("p_kind").GetString());
    }

    [Fact]
    public async Task NoIdentifyingHeaders_AreSent()
    {
        var (client, h) = Create();
        h.Respond = _ => Json("[]");

        await client.GetActiveAdsAsync(CancellationToken.None);

        var names = h.Requests[0].Headers.Keys.Select(k => k.ToUpperInvariant()).ToHashSet();
        Assert.Equal(new HashSet<string> { "APIKEY", "ACCEPT", "CONTENT-TYPE" }, names);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ServerError_ThrowsHttpRequestException(HttpStatusCode code)
    {
        var (client, h) = Create();
        h.Respond = _ => new HttpResponseMessage(code);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetActiveAdsAsync(CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.RecordAsync(Id, AdEventKind.View, CancellationToken.None));
    }

    [Fact]
    public async Task HugeFeed_Rejected()
    {
        var (client, h) = Create();
        h.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[SupabaseAdClient.MaxFeedBytes + 1]) };

        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetActiveAdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task HugeResponse_WithoutContentLength_StillStopsAtLimit()
    {
        // Server hajmni aytmasa (chunked) ham — o'qish chegaradan oshganda to'xtaydi, xotira to'lmaydi.
        var (client, h) = Create();
        h.Respond = _ => Streamed(SupabaseAdClient.MaxFeedBytes + 10, "application/json");
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetActiveAdsAsync(CancellationToken.None));

        h.Respond = _ => Streamed(SupabaseAdClient.MaxImageBytes + 10, "image/png");
        Assert.Null(await client.GetImageAsync("https://x.uz/a.png", CancellationToken.None));

        h.Respond = _ => Streamed(1000, "image/png");
        Assert.Equal(1000, (await client.GetImageAsync("https://x.uz/a.png", CancellationToken.None))!.Length);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public async Task Image_PngOrJpg_Downloaded(string type)
    {
        var (client, h) = Create();
        h.Respond = _ => Bytes(new byte[] { 9, 8, 7 }, type);

        var bytes = await client.GetImageAsync("https://abcd.supabase.co/storage/v1/object/public/ad-images/a.png", CancellationToken.None);

        Assert.Equal(new byte[] { 9, 8, 7 }, bytes);
        Assert.Equal(HttpMethod.Get, h.Requests[0].Method);
        Assert.False(h.Requests[0].Headers.ContainsKey("apikey")); // ommaviy rasm — kalit kerak emas
    }

    [Theory]
    [InlineData("image/webp")]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("application/octet-stream")]
    public async Task Image_OtherTypes_Ignored(string type)
    {
        var (client, h) = Create();
        h.Respond = _ => Bytes(new byte[] { 1 }, type);

        Assert.Null(await client.GetImageAsync("https://x.uz/a", CancellationToken.None));
    }

    [Fact]
    public async Task Image_TooBig_Or_NotFound_Or_UnsafeUrl_IsNull()
    {
        var (client, h) = Create();
        h.Respond = _ => Bytes(new byte[SupabaseAdClient.MaxImageBytes + 1], "image/png");
        Assert.Null(await client.GetImageAsync("https://x.uz/katta.png", CancellationToken.None));

        h.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        Assert.Null(await client.GetImageAsync("https://x.uz/yoq.png", CancellationToken.None));

        int before = h.Requests.Count;
        Assert.Null(await client.GetImageAsync("http://x.uz/a.png", CancellationToken.None));
        Assert.Null(await client.GetImageAsync("file:///C:/a.png", CancellationToken.None));
        Assert.Equal(before, h.Requests.Count); // xavfli manzilga so'rov umuman ketmadi
    }

    [Theory]
    [InlineData(null, "k")]
    [InlineData("", "k")]
    [InlineData("https://abcd.supabase.co", null)]
    [InlineData("https://abcd.supabase.co", "  ")]
    [InlineData("http://abcd.supabase.co", "k")]
    [InlineData("abcd.supabase.co", "k")]
    [InlineData("https://abcd.supabase.co", "sb_secret_XYZ")]
    public void Endpoint_InvalidOrSecret_IsNull_FeatureOff(string? url, string? key)
    {
        Assert.Null(AdsEndpoint.TryCreate(url, key));
    }

    [Fact]
    public void Endpoint_ServiceRoleJwt_Refused_AnonJwtAccepted()
    {
        Assert.Null(AdsEndpoint.TryCreate("https://abcd.supabase.co", ServiceJwt));
        var ok = AdsEndpoint.TryCreate(" https://abcd.supabase.co/ ", AnonJwt)!;
        Assert.Equal("https://abcd.supabase.co/", ok.BaseUrl.AbsoluteUri);
        Assert.True(ok.IsJwtKey);
        Assert.False(AdsEndpoint.TryCreate("https://abcd.supabase.co", Publishable)!.IsJwtKey);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(byte[] data, string type)
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    /// <summary>Hajmi oldindan noma'lum javob (Content-Length yo'q).</summary>
    private static HttpResponseMessage Streamed(int size, string type)
    {
        var content = new StreamContent(new UnknownLengthStream(new byte[size]));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        Assert.Null(content.Headers.ContentLength);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class UnknownLengthStream : Stream
    {
        private readonly MemoryStream _inner;

        public UnknownLengthStream(byte[] data) => _inner = new MemoryStream(data);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed record Captured(HttpMethod Method, string Url, Dictionary<string, string> Headers, string? Body);

    private sealed class FakeHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        public List<Captured> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
            if (request.Content is not null)
            {
                foreach (var h in request.Content.Headers)
                {
                    headers[h.Key] = string.Join(",", h.Value);
                }
            }

            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Captured(request.Method, request.RequestUri!.AbsoluteUri, headers, body));
            return Respond(request);
        }
    }
}
