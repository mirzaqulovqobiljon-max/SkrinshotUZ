using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Storage;

namespace SkrinshotUZ.Storage;

/// <summary>Rasmni faylga yozish (PNG yoki JPG).</summary>
internal static class ImageSaver
{
    /// <summary>
    /// Saqlaydi. Avval vaqtinchalik faylga yoziladi, keyin nomlanadi — xato bo'lsa chala fayl qolmaydi.
    /// </summary>
    /// <param name="image">Rasm.</param>
    /// <param name="path">To'liq yo'l.</param>
    /// <param name="format">Format.</param>
    public static void Save(Image image, string path, ImageFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string temp = path + ".tmp";
        try
        {
            if (format == ImageFileFormat.Jpg)
            {
                var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                using var parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, SaveOptions.JpegQuality);
                image.Save(temp, codec, parameters);
            }
            else
            {
                image.Save(temp, ImageFormat.Png);
            }

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    /// <summary>Saqlashdagi kutiladigan xatolar (ruxsat yo'q, disk to'la, fleshka chiqarib olingan…).</summary>
    public static bool IsSaveError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ExternalException or ArgumentException or NotSupportedException;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // vaqtinchalik fayl qolsa ham zarar yo'q
        }
    }
}

/// <summary>Rasm ichida saqlanadigan monitor DPI'si (PNG pHYs / JPEG JFIF) — "Asl o'lchamda" chop etish uchun.</summary>
internal static class ImageDpi
{
    /// <summary>Rasmning DPI'si (yo'q yoki g'alati bo'lsa — 96).</summary>
    public static double Of(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return SkrinshotUZ.Core.Printing.Units.SanitizeImageDpi(image.HorizontalResolution);
    }

    /// <summary>Rasmga monitor DPI'sini yozadi (faylga saqlanganda ham qoladi).</summary>
    public static void Set(Bitmap image, double dpi)
    {
        ArgumentNullException.ThrowIfNull(image);
        float value = (float)SkrinshotUZ.Core.Printing.Units.SanitizeImageDpi(dpi);
        image.SetResolution(value, value);
    }
}

/// <summary>Tarix oynasi uchun rasmchalar.</summary>
internal static class Thumbnails
{
    private static readonly Color Background = Color.FromArgb(243, 243, 243);

    /// <summary>Rasmcha (proporsiya saqlanadi, markazda). Fayl o'qilmasa — <c>null</c>.</summary>
    public static Bitmap? Create(string path, Size size)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            var thumb = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(thumb);
            g.Clear(Background);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Kichik rasm kattalashtirilmaydi (xiralashib ketadi) — asl o'lchamda markazda turadi.
            double scale = Math.Min(1.0, Math.Min((double)size.Width / image.Width, (double)size.Height / image.Height));
            int w = Math.Max(1, (int)Math.Round(image.Width * scale));
            int h = Math.Max(1, (int)Math.Round(image.Height * scale));
            if (scale >= 1.0)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor; // 1:1 — piksel-piksel nusxa
                g.PixelOffsetMode = PixelOffsetMode.Half;
            }

            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY); // chetlarda fon bilan aralashgan "halo" bo'lmasin
            g.DrawImage(image, new Rectangle((size.Width - w) / 2, (size.Height - h) / 2, w, h), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            using var border = new Pen(Color.FromArgb(200, 200, 200));
            g.DrawRectangle(border, 0, 0, size.Width - 1, size.Height - 1);
            return thumb;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or ExternalException)
        {
            return null;
        }
    }

    /// <summary>Fayldagi rasmni to'liq yuklaydi (faylni band qilib qo'ymaydi). O'qilmasa — <c>null</c>.</summary>
    public static Bitmap? Load(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var image = Image.FromStream(stream);
            var copy = new Bitmap(image);
            copy.SetResolution(image.HorizontalResolution, image.VerticalResolution); // DPI = monitor masshtabi ("Asl o'lcham" uchun)
            return copy;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or ExternalException)
        {
            return null;
        }
    }
}

/// <summary>Windows Explorer bilan ishlash (faqat mahalliy fayllar).</summary>
internal static class Shell
{
    /// <summary>Faylni standart dasturda ochadi (masalan, "Rasmlar" ilovasi).</summary>
    public static void Open(string path) => Start(new ProcessStartInfo(path) { UseShellExecute = true });

    /// <summary>
    /// Hujjatni standart dasturda ochadi (.docx — Word, LibreOffice yoki WPS).
    /// Ochadigan dastur yo'q bo'lsa — <c>false</c> (foydalanuvchiga papka ko'rsatiladi).
    /// </summary>
    public static bool TryOpenDocument(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Havolani standart brauzerda ochadi (faqat https — boshqa hech narsa ishga tushirilmaydi).</summary>
    public static void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    /// <summary>Papkani ochib, faylni belgilaydi.</summary>
    public static void ShowInFolder(string path) =>
        Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });

    /// <summary>Papkani ochadi (yo'q bo'lsa — yaratadi).</summary>
    public static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private static void Start(ProcessStartInfo info)
    {
        try
        {
            using var _ = Process.Start(info);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // Ochib bo'lmasa — jim (masalan, fayl shu orada o'chirilgan).
        }
    }
}
