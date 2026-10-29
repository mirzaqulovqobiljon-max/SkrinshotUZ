namespace SkrinshotUZ.Core.Ocr;

/// <summary>
/// OCR uchun kulrang (8 bit) rasm tayyorlash. Rangli rasm avval kulrangga o'tkaziladi (4 marta kam xotira),
/// keyin kattalashtiriladi — eski kompyuterlarda ham katta skrinshot xotirani to'ldirib yubormaydi.
/// </summary>
public static class GrayImage
{
    /// <summary>
    /// 32 bitli BGRA piksellardan kulrang rasm: yorqinlik = 0.299R + 0.587G + 0.114B (ITU-R BT.601).
    /// Shaffof piksellar oq fonga qo'yilgandek hisoblanadi.
    /// </summary>
    /// <param name="bgra">Piksellar (qator ketma-ketligi, har biri 4 bayt: B, G, R, A).</param>
    /// <param name="stride">Bir qator baytlari (≥ width × 4).</param>
    /// <param name="width">Kenglik.</param>
    /// <param name="height">Balandlik.</param>
    /// <returns>width × height bayt.</returns>
    public static byte[] FromBgra(ReadOnlySpan<byte> bgra, int stride, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (stride < width * 4 || bgra.Length < ((long)stride * (height - 1)) + (width * 4))
        {
            throw new ArgumentException("Piksel ma'lumoti o'lchamga mos emas.", nameof(bgra));
        }

        var gray = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            var row = bgra.Slice(y * stride, width * 4);
            int o = y * width;
            for (int x = 0; x < width; x++)
            {
                int b = row[x * 4], g = row[(x * 4) + 1], r = row[(x * 4) + 2], a = row[(x * 4) + 3];

                // Butun sonlarda: (299R + 587G + 114B) / 1000, keyin oq fon bilan aralashtirish.
                int luma = ((299 * r) + (587 * g) + (114 * b) + 500) / 1000;
                gray[o + x] = (byte)(((luma * a) + (255 * (255 - a)) + 127) / 255);
            }
        }

        return gray;
    }

    /// <summary>Bilinear kattalashtirish (piksel markazlari bo'yicha — siljish bo'lmaydi).</summary>
    /// <param name="source">Kulrang rasm.</param>
    /// <param name="width">Kenglik.</param>
    /// <param name="height">Balandlik.</param>
    /// <param name="scale">Koeffitsient (1 — nusxa).</param>
    /// <returns>(width·scale) × (height·scale) bayt.</returns>
    public static byte[] Upscale(byte[] source, int width, int height, int scale)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        if (source.Length != width * height)
        {
            throw new ArgumentException("Massiv uzunligi o'lchamga mos emas.", nameof(source));
        }

        if (scale == 1)
        {
            return (byte[])source.Clone();
        }

        int w2 = width * scale, h2 = height * scale;
        var result = new byte[w2 * h2];

        // Har bir yangi ustun uchun manba ustunlari va og'irliklari oldindan hisoblanadi (×256).
        var x0 = new int[w2];
        var x1 = new int[w2];
        var fx = new int[w2];
        for (int x = 0; x < w2; x++)
        {
            Map(x, scale, width, out x0[x], out x1[x], out fx[x]);
        }

        for (int y = 0; y < h2; y++)
        {
            Map(y, scale, height, out int y0, out int y1, out int fy);
            int r0 = y0 * width, r1 = y1 * width, o = y * w2;
            for (int x = 0; x < w2; x++)
            {
                int top = (source[r0 + x0[x]] * (256 - fx[x])) + (source[r0 + x1[x]] * fx[x]);
                int bottom = (source[r1 + x0[x]] * (256 - fx[x])) + (source[r1 + x1[x]] * fx[x]);
                result[o + x] = (byte)(((top * (256 - fy)) + (bottom * fy) + 32768) >> 16);
            }
        }

        return result;
    }

    /// <summary>Yangi koordinatani manba koordinatalari va og'irlikka aylantiradi.</summary>
    private static void Map(int dest, int scale, int size, out int i0, out int i1, out int weight)
    {
        // Markaz: (dest + 0.5) / scale - 0.5, ×256 butun sonlarda.
        int pos = (((2 * dest) + 1) * 128 / scale) - 128;
        if (pos <= 0)
        {
            i0 = i1 = 0;
            weight = 0;
            return;
        }

        i0 = pos >> 8;
        weight = pos & 255;
        if (i0 >= size - 1)
        {
            i0 = i1 = size - 1;
            weight = 0;
            return;
        }

        i1 = i0 + 1;
    }
}
