using System.Drawing;
using System.Drawing.Imaging;
using SkrinshotUZ.Ads;
using SkrinshotUZ.Core.Ads;
using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Windows.Tests.Ads;

/// <summary>Reklama oynasi: aniq belgilangan, fokus olmaydi, yopiladi, buzilgan rasmda yiqilmaydi.</summary>
public class AdWindowTests
{
    private static readonly AdItem WithLink = new(Guid.NewGuid(), "Kurs & chegirma", null, "https://kurs.uz", 0);
    private static readonly AdItem NoLink = new(Guid.NewGuid(), "Faqat e'lon", null, null, 0);

    private static byte[] Png(int w, int h, ImageFormat? format = null)
    {
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.SteelBlue);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, format ?? ImageFormat.Png);
        return ms.ToArray();
    }

    private static IEnumerable<Control> All(Control root) =>
        root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c)));

    [Fact]
    public void ClearlyLabeledAsAd_WithAppName()
    {
        using var w = new AdWindow(WithLink, null);
        var texts = All(w).Select(c => c.Text).ToList();

        Assert.Contains(Strings.Ads_Label, texts);
        Assert.Contains(Strings.AppName, texts);
        Assert.Contains("Kurs & chegirma", texts);
        Assert.False(All(w).OfType<Label>().Single(l => l.Text == "Kurs & chegirma").UseMnemonic); // '&' yo'qolmasin
    }

    [Fact]
    public void MoreButton_OnlyWhenLinkExists_CloseAlwaysExists()
    {
        using var linked = new AdWindow(WithLink, null);
        using var plain = new AdWindow(NoLink, null);

        Assert.Contains(All(linked).OfType<Button>(), b => b.Text == Strings.Ads_More);
        Assert.DoesNotContain(All(plain).OfType<Button>(), b => b.Text == Strings.Ads_More);
        Assert.Contains(All(plain).OfType<Button>(), b => b.Text == Strings.Ads_Close);
        Assert.Contains(All(plain).OfType<Button>(), b => b.Text == "✕");
    }

    [Fact]
    public void DoesNotStealFocus_NotInTaskbar_Topmost()
    {
        using var w = new AdWindow(WithLink, null);

        Assert.False(w.ShowInTaskbar);
        Assert.True(w.TopMost);
        Assert.Equal(0x08000000, w.ExtendedStyleForTest & 0x08000000); // WS_EX_NOACTIVATE — yozayotgan matn uzilmaydi
        Assert.Equal(0x00000080, w.ExtendedStyleForTest & 0x00000080); // WS_EX_TOOLWINDOW
    }

    [Theory]
    [InlineData("✕")]
    [InlineData("Yopish")]
    public async Task CloseButtons_CompleteAsDismissed(string text)
    {
        using var w = new AdWindow(NoLink, null);
        w.Show();

        All(w).OfType<Button>().First(b => b.Text == text).PerformClick();

        Assert.Equal(AdOutcome.Dismissed, await w.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CloseSilently_IsNotAClick()
    {
        using var w = new AdWindow(WithLink, null);
        w.Show();

        w.CloseSilently();

        Assert.Equal(AdOutcome.TimedOut, await w.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void PlacedInBottomRightCorner_InsideWorkingArea()
    {
        var area = new Rectangle(0, 0, 1920, 1040);
        using var w = new AdWindow(WithLink, Png(640, 280));

        w.PlaceInCorner(area);

        Assert.True(w.Right <= area.Right && w.Bottom <= area.Bottom, w.Bounds.ToString());
        Assert.True(w.Right >= area.Right - 40 && w.Bottom >= area.Bottom - 40, w.Bounds.ToString());
    }

    [Fact]
    public void Image_PngAndJpg_Shown_WindowGrowsForIt()
    {
        using var withPng = new AdWindow(NoLink, Png(640, 280));
        using var withJpg = new AdWindow(NoLink, Png(640, 280, ImageFormat.Jpeg));
        using var without = new AdWindow(NoLink, null);

        Assert.True(withPng.HasImage);
        Assert.True(withJpg.HasImage);
        Assert.False(without.HasImage);
        Assert.True(withPng.Height > without.Height + 100);
    }

    [Fact]
    public void BrokenOrHugeImage_ShownWithoutImage_NoCrash()
    {
        Assert.Null(AdWindow.TryDecode(new byte[] { 1, 2, 3, 4 }));
        Assert.Null(AdWindow.TryDecode(Array.Empty<byte>()));
        Assert.Null(AdWindow.TryDecode(null));
        Assert.Null(AdWindow.TryDecode(Png(4100, 10)));     // juda katta o'lcham
        using var big = AdWindow.TryDecode(Png(3000, 1000))!;
        Assert.Equal(720, big.Width);                        // xotira uchun kichraytiriladi

        using var w = new AdWindow(NoLink, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0 });
        Assert.False(w.HasImage);
    }

    [Fact]
    public void AdsConfig_EmptyInTestBuild_FeatureOff()
    {
        // Testlar va CI tutun testi Supabase'siz yig'iladi — dastur hech qayerga ulanmaydi.
        Assert.Null(AdsConfig.Endpoint);
    }
}
