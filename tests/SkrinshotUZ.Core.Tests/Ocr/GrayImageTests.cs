using SkrinshotUZ.Core.Ocr;

namespace SkrinshotUZ.Core.Tests.Ocr;

/// <summary>Kulrangga o'tkazish va kattalashtirish.</summary>
public class GrayImageTests
{
    [Fact]
    public void FromBgra_PureColors_UseBt601Weights()
    {
        // B, G, R, A
        byte[] px =
        {
            0, 0, 255, 255,       // qizil → 76
            0, 255, 0, 255,       // yashil → 150
            255, 0, 0, 255,       // ko'k → 29
            255, 255, 255, 255,   // oq → 255
            0, 0, 0, 255,         // qora → 0
        };

        var g = GrayImage.FromBgra(px, 20, 5, 1);

        Assert.Equal(new byte[] { 76, 150, 29, 255, 0 }, g);
    }

    [Fact]
    public void FromBgra_Transparent_BecomesWhite_HalfTransparentBlack_IsMidGray()
    {
        byte[] px = { 0, 0, 0, 0, 0, 0, 0, 128 };

        var g = GrayImage.FromBgra(px, 8, 2, 1);

        Assert.Equal(255, g[0]);
        Assert.InRange(g[1], 126, 128);
    }

    [Fact]
    public void FromBgra_RespectsStridePadding()
    {
        // 1 piksel kenglik, qator 8 bayt (4 bayt to'ldiruvchi — e'tiborsiz qolishi kerak).
        byte[] px = { 0, 0, 0, 255, 9, 9, 9, 9, 255, 255, 255, 255, 9, 9, 9, 9 };

        Assert.Equal(new byte[] { 0, 255 }, GrayImage.FromBgra(px, 8, 1, 2));
    }

    [Fact]
    public void FromBgra_TooShortBuffer_Throws()
    {
        Assert.Throws<ArgumentException>(() => GrayImage.FromBgra(new byte[7], 8, 2, 1));
        Assert.Throws<ArgumentException>(() => GrayImage.FromBgra(new byte[8], 4, 2, 1)); // stride < width*4
    }

    [Fact]
    public void Upscale_Scale1_IsCopy()
    {
        byte[] src = { 1, 2, 3, 4 };
        var r = GrayImage.Upscale(src, 2, 2, 1);

        Assert.Equal(src, r);
        Assert.NotSame(src, r);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Upscale_UniformImage_StaysUniform_SizeMultiplied(int scale)
    {
        byte[] src = Enumerable.Repeat((byte)137, 7 * 5).ToArray();

        var r = GrayImage.Upscale(src, 7, 5, scale);

        Assert.Equal(7 * scale * 5 * scale, r.Length);
        Assert.All(r, v => Assert.Equal(137, v));
    }

    [Fact]
    public void Upscale_Edge_IsInterpolated_Monotonic_CornersPreserved()
    {
        // Qora | Oq (2×1) → 3 marta: 6×3; har bir qator chapdan o'ngga oshib boradi.
        var all = GrayImage.Upscale(new byte[] { 0, 255 }, 2, 1, 3);
        Assert.Equal(18, all.Length);
        Assert.Equal(all.Take(6).ToArray(), all.Skip(12).ToArray());   // qatorlar bir xil
        var r = all.Take(6).ToArray();

        Assert.Equal(0, r[0]);
        Assert.Equal(255, r[5]);
        for (int i = 1; i < r.Length; i++)
        {
            Assert.True(r[i] >= r[i - 1], $"{r[i - 1]} → {r[i]}");
        }

        Assert.InRange(r[2], 60, 120);   // markazga yaqinroq qora tomonda
        Assert.InRange(r[3], 135, 195);
    }

    [Fact]
    public void Upscale_IsSymmetric()
    {
        var a = GrayImage.Upscale(new byte[] { 0, 255 }, 2, 1, 2);
        var b = GrayImage.Upscale(new byte[] { 255, 0 }, 2, 1, 2);

        Assert.Equal(a, b.Reverse().ToArray());
    }

    [Fact]
    public void Upscale_1x1_Works()
    {
        Assert.Equal(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9, 9 }, GrayImage.Upscale(new byte[] { 9 }, 1, 1, 3));
    }

    [Fact]
    public void Upscale_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentException>(() => GrayImage.Upscale(new byte[3], 2, 2, 2));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => GrayImage.Upscale(new byte[4], 2, 2, 0));
    }

    [Fact]
    public void NormalizePolarity_DarkBackground_Inverted()
    {
        byte[] g = { 20, 20, 20, 230 };   // qorong'i fon, bitta och "harf"

        Assert.True(GrayImage.NormalizePolarity(g));
        Assert.Equal(new byte[] { 235, 235, 235, 25 }, g);
    }

    [Fact]
    public void NormalizePolarity_LightBackground_Unchanged()
    {
        byte[] g = { 250, 250, 250, 10 };

        Assert.False(GrayImage.NormalizePolarity(g));
        Assert.Equal(new byte[] { 250, 250, 250, 10 }, g);
    }

    [Fact]
    public void NormalizePolarity_ThresholdBoundary_AndEmpty()
    {
        byte[] atThreshold = { GrayImage.DarkThreshold, GrayImage.DarkThreshold };
        Assert.False(GrayImage.NormalizePolarity(atThreshold));

        byte[] below = { GrayImage.DarkThreshold - 1, GrayImage.DarkThreshold - 1 };
        Assert.True(GrayImage.NormalizePolarity(below));

        Assert.False(GrayImage.NormalizePolarity(Array.Empty<byte>()));
    }
}
