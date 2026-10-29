using System.Text.Json;
using SkrinshotUZ.Core.Ads;

namespace SkrinshotUZ.Core.Tests.Ads;

/// <summary>Server javobini o'qish (buzilgan/xavfli yozuvlar) va "kuniga bir marta" + navbat.</summary>
public class AdFeedAndScheduleTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    [Fact]
    public void Parse_ReadsServerRows_InOrder()
    {
        var ads = AdFeed.Parse($$"""
            [
              {"id":"{{A}}","title":"  Ingliz tili kursi  ","image_url":"https://x.supabase.co/storage/v1/object/public/ad-images/a.png","link_url":"https://kurs.uz/?a=1&b=2","priority":5},
              {"id":"{{B}}","title":"Telegram kanal","image_url":null,"link_url":"https://t.me/kanal","priority":0}
            ]
            """);

        Assert.Equal(2, ads.Count);
        Assert.Equal(new AdItem(A, "Ingliz tili kursi", "https://x.supabase.co/storage/v1/object/public/ad-images/a.png", "https://kurs.uz/?a=1&b=2", 5), ads[0]);
        Assert.Equal(B, ads[1].Id);
        Assert.Null(ads[1].ImageUrl);
    }

    [Theory]
    [InlineData("http://kurs.uz")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-settings:privacy")]
    [InlineData("https://user:parol@kurs.uz")]
    [InlineData("kurs.uz")]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsafeUrls_AreDropped_ButAdStillShown(string url)
    {
        var ads = AdFeed.Parse(JsonSerializer.Serialize(new[] { new { id = A.ToString(), title = "Kurs", image_url = url, link_url = url } }));

        var ad = Assert.Single(ads);
        Assert.Null(ad.LinkUrl);
        Assert.Null(ad.ImageUrl);
        Assert.Null(AdFeed.SafeUrl(url));
    }

    [Fact]
    public void SafeUrl_AcceptsHttps_AndNormalizes()
    {
        Assert.Equal("https://kurs.uz/", AdFeed.SafeUrl("  https://KURS.uz  "));
        Assert.Equal("https://t.me/kanal", AdFeed.SafeUrl("https://t.me/kanal"));
        Assert.Null(AdFeed.SafeUrl("https://x.uz/" + new string('a', 2100)));
    }

    [Fact]
    public void Parse_SkipsBrokenItems_KeepsGoodOnes()
    {
        var ads = AdFeed.Parse($$"""
            [
              {"id":"emas-guid","title":"x"},
              {"id":"{{A}}","title":"   "},
              {"id":"{{A}}"},
              {"title":"id yo'q"},
              42,
              "matn",
              null,
              {"id":"{{B}}","title":"Yaxshi","priority":"baland"},
              {"id":"{{C}}","title":123}
            ]
            """);

        var ad = Assert.Single(ads);
        Assert.Equal(B, ad.Id);
        Assert.Equal(0, ad.Priority); // noto'g'ri ustuvorlik → 0
    }

    [Fact]
    public void Parse_LongTitle_IsShortened_AtMost120Chars()
    {
        var ads = AdFeed.Parse(JsonSerializer.Serialize(new[] { new { id = A.ToString(), title = new string('x', 500) } }));

        Assert.Equal(AdFeed.MaxTitleLength, ads[0].Title.Length);
        Assert.EndsWith("…", ads[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_TakesAtMost20()
    {
        var many = Enumerable.Range(0, 50).Select(i => new { id = Guid.NewGuid().ToString(), title = "R" + i });
        Assert.Equal(AdFeed.MaxAds, AdFeed.Parse(JsonSerializer.Serialize(many)).Count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"salom\"")]
    [InlineData("<html>")]
    [InlineData("")]
    public void Parse_NotAnArray_Throws_JsonException(string body)
    {
        Assert.ThrowsAny<JsonException>(() => AdFeed.Parse(body));
    }

    [Fact]
    public void Parse_EmptyArray_NoAds()
    {
        Assert.Empty(AdFeed.Parse("[]"));
    }

    [Fact]
    public void IsDue_OncePerDay()
    {
        var today = new DateOnly(2026, 10, 29);
        Assert.True(AdSchedule.IsDue(null, today));                      // hech qachon ko'rsatilmagan
        Assert.True(AdSchedule.IsDue(today.AddDays(-1), today));         // kecha
        Assert.True(AdSchedule.IsDue(today.AddDays(-30), today));
        Assert.False(AdSchedule.IsDue(today, today));                    // bugun allaqachon
        Assert.True(AdSchedule.IsDue(today.AddDays(1), today));          // soat orqaga surilgan — qayta sinxronlanadi
    }

    [Fact]
    public void Pick_RotatesThroughAds_DayByDay()
    {
        var ads = new[] { Ad(A), Ad(B), Ad(C) };

        Assert.Equal(A, AdSchedule.Pick(ads, null)!.Id);   // birinchi kun — eng ustuvori
        Assert.Equal(B, AdSchedule.Pick(ads, A)!.Id);
        Assert.Equal(C, AdSchedule.Pick(ads, B)!.Id);
        Assert.Equal(A, AdSchedule.Pick(ads, C)!.Id);      // aylanma
    }

    [Fact]
    public void Pick_LastAdRemoved_StartsFromFirst_EmptyGivesNull()
    {
        var ads = new[] { Ad(B), Ad(C) };

        Assert.Equal(B, AdSchedule.Pick(ads, A)!.Id);
        Assert.Equal(B, AdSchedule.Pick(new[] { Ad(B) }, B)!.Id); // bitta reklama — har kuni o'sha
        Assert.Null(AdSchedule.Pick(Array.Empty<AdItem>(), A));
    }

    private static AdItem Ad(Guid id) => new(id, "R", null, null, 0);
}
