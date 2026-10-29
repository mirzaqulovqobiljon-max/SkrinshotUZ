using System.Text.Json;
using SkrinshotUZ.Core.Ads;
using SkrinshotUZ.Core.Settings;

namespace SkrinshotUZ.Core.Tests.Ads;

/// <summary>Kuniga bir marta ko'rsatish oqimi: internet yo'q, reklama yo'q, rasm yuklanmadi, statistika.</summary>
public class AdCoordinatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);
    private static readonly AdItem First = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "Kurs", "https://x.uz/a.png", "https://kurs.uz", 5);
    private static readonly AdItem Second = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), "Kanal", null, "https://t.me/k", 0);

    private readonly FakeClient _client = new();
    private readonly FakePresenter _presenter = new();
    private readonly AppSettings _settings = new();
    private int _saves;

    private AdCoordinator Create(bool configured = true) =>
        new(configured ? _client : null, _presenter, new SettingsAdState(_settings, () => _saves++), () => Today);

    [Fact]
    public async Task FirstRunToday_ShowsFirstAd_RecordsView_MarksDay()
    {
        _client.Ads = new[] { First, Second };

        var result = await Create().RunOnceAsync(CancellationToken.None);

        Assert.Equal(AdRunResult.Shown, result);
        Assert.Equal(First, _presenter.Shown.Single().Ad);
        Assert.Equal(new byte[] { 1, 2, 3 }, _presenter.Shown.Single().Image);
        Assert.Equal(new[] { (First.Id, AdEventKind.View) }, _client.Events);
        Assert.Equal(Today, _settings.AdsLastShownOn);
        Assert.Equal(First.Id, _settings.AdsLastAdId);
        Assert.True(_saves >= 1);
    }

    [Fact]
    public async Task SecondRunSameDay_DoesNothing_NoNetwork()
    {
        _client.Ads = new[] { First };
        await Create().RunOnceAsync(CancellationToken.None);
        int calls = _client.Calls;

        var result = await Create().RunOnceAsync(CancellationToken.None);

        Assert.Equal(AdRunResult.AlreadyShownToday, result);
        Assert.Equal(calls, _client.Calls); // bugun internetga qayta chiqilmadi
        Assert.Single(_presenter.Shown);
    }

    [Fact]
    public async Task NextDay_ShowsNextAd()
    {
        _client.Ads = new[] { First, Second };
        _settings.AdsLastShownOn = Today.AddDays(-1);
        _settings.AdsLastAdId = First.Id;

        await Create().RunOnceAsync(CancellationToken.None);

        Assert.Equal(Second, _presenter.Shown.Single().Ad);
        Assert.Null(_presenter.Shown.Single().Image); // rasmi yo'q reklama — rasm so'ralmaydi
        Assert.Equal(0, _client.ImageCalls);
    }

    [Fact]
    public async Task Clicked_RecordsViewAndClick()
    {
        _client.Ads = new[] { First };
        _presenter.Outcome = AdOutcome.Clicked;

        await Create().RunOnceAsync(CancellationToken.None);

        Assert.Equal(new[] { (First.Id, AdEventKind.View), (First.Id, AdEventKind.Click) }, _client.Events);
    }

    [Theory]
    [InlineData(AdOutcome.Dismissed)]
    [InlineData(AdOutcome.TimedOut)]
    public async Task NotClicked_OnlyView(AdOutcome outcome)
    {
        _client.Ads = new[] { First };
        _presenter.Outcome = outcome;

        await Create().RunOnceAsync(CancellationToken.None);

        Assert.Equal(new[] { (First.Id, AdEventKind.View) }, _client.Events);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("json")]
    [InlineData("io")]
    public async Task Offline_IsSilent_DayNotMarked_RetriedNextStart(string failure)
    {
        _client.FeedError = failure switch
        {
            "http" => new HttpRequestException("internet yo'q"),
            "timeout" => new TaskCanceledException("10 s"),
            "json" => new JsonException("buzuq"),
            _ => new IOException("uzildi"),
        };

        var result = await Create().RunOnceAsync(CancellationToken.None);

        Assert.Equal(AdRunResult.Offline, result);
        Assert.Empty(_presenter.Shown);
        Assert.Null(_settings.AdsLastShownOn); // keyingi ishga tushishda (internet bo'lsa) qayta urinadi
        Assert.Equal(0, _saves);
    }

    [Fact]
    public async Task NoActiveAds_NothingShown_DayNotMarked()
    {
        _client.Ads = Array.Empty<AdItem>();

        Assert.Equal(AdRunResult.NoAds, await Create().RunOnceAsync(CancellationToken.None));
        Assert.Empty(_presenter.Shown);
        Assert.Null(_settings.AdsLastShownOn);
    }

    [Fact]
    public async Task ImageFails_AdShownWithoutImage()
    {
        _client.Ads = new[] { First };
        _client.ImageError = new HttpRequestException("404");

        Assert.Equal(AdRunResult.Shown, await Create().RunOnceAsync(CancellationToken.None));
        Assert.Null(_presenter.Shown.Single().Image);
    }

    [Fact]
    public async Task StatisticsFail_StillShown_NoCrash()
    {
        _client.Ads = new[] { First };
        _client.RecordError = new HttpRequestException("500");
        _presenter.Outcome = AdOutcome.Clicked;

        Assert.Equal(AdRunResult.Shown, await Create().RunOnceAsync(CancellationToken.None));
        Assert.Single(_presenter.Shown);
    }

    [Fact]
    public async Task DayMarked_BeforeWindowShown()
    {
        // Oyna ochiq turganda dastur yopilsa (yoki kompyuter o'chsa) — bugun qayta chiqmasligi kerak.
        _client.Ads = new[] { First };
        DateOnly? markedWhenShown = null;
        _presenter.OnShow = () => markedWhenShown = _settings.AdsLastShownOn;

        await Create().RunOnceAsync(CancellationToken.None);

        Assert.Equal(Today, markedWhenShown);
    }

    [Fact]
    public async Task NotConfigured_NeverTouchesNetwork()
    {
        _client.Ads = new[] { First };

        Assert.Equal(AdRunResult.NotConfigured, await Create(configured: false).RunOnceAsync(CancellationToken.None));
        Assert.Equal(0, _client.Calls);
        Assert.Empty(_presenter.Shown);
    }

    [Fact]
    public async Task AppClosing_CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _client.FeedError = new OperationCanceledException(cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => Create().RunOnceAsync(cts.Token));
        Assert.Empty(_presenter.Shown);
    }

    [Fact]
    public void Settings_RoundTrip_AdState()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.Tests." + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(Path.Combine(dir, "settings.json"));
            store.Save(new AppSettings { AdsLastShownOn = Today, AdsLastAdId = First.Id });

            var back = store.Load();

            Assert.Equal(Today, back.AdsLastShownOn);
            Assert.Equal(First.Id, back.AdsLastAdId);
            Assert.Contains("\"2026-10-29\"", File.ReadAllText(Path.Combine(dir, "settings.json")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private sealed class FakeClient : IAdClient
    {
        public IReadOnlyList<AdItem> Ads { get; set; } = Array.Empty<AdItem>();

        public Exception? FeedError { get; set; }

        public Exception? ImageError { get; set; }

        public Exception? RecordError { get; set; }

        public List<(Guid, AdEventKind)> Events { get; } = new();

        public int Calls { get; private set; }

        public int ImageCalls { get; private set; }

        public Task<IReadOnlyList<AdItem>> GetActiveAdsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return FeedError is null ? Task.FromResult(Ads) : Task.FromException<IReadOnlyList<AdItem>>(FeedError);
        }

        public Task<byte[]?> GetImageAsync(string url, CancellationToken cancellationToken)
        {
            Calls++;
            ImageCalls++;
            return ImageError is null ? Task.FromResult<byte[]?>(new byte[] { 1, 2, 3 }) : Task.FromException<byte[]?>(ImageError);
        }

        public Task RecordAsync(Guid adId, AdEventKind kind, CancellationToken cancellationToken)
        {
            Calls++;
            if (RecordError is not null)
            {
                return Task.FromException(RecordError);
            }

            Events.Add((adId, kind));
            return Task.CompletedTask;
        }
    }

    private sealed class FakePresenter : IAdPresenter
    {
        public AdOutcome Outcome { get; set; } = AdOutcome.Dismissed;

        public Action? OnShow { get; set; }

        public List<(AdItem Ad, byte[]? Image)> Shown { get; } = new();

        public Task<AdOutcome> ShowAsync(AdItem ad, byte[]? image)
        {
            OnShow?.Invoke();
            Shown.Add((ad, image));
            return Task.FromResult(Outcome);
        }
    }
}
