using System.Drawing;
using System.Drawing.Imaging;
using SkrinshotUZ.Core.Storage;
using SkrinshotUZ.Storage;

namespace SkrinshotUZ.Windows.Tests.Storage;

/// <summary>Haqiqiy diskka yozish/o'qish (har test o'z vaqtinchalik papkasida).</summary>
public sealed class ImageFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.tests." + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>Chap yarmi qizil, o'ng yarmi ko'k (JPG siqishidan keyin ham ajralib turadi).</summary>
    private static Bitmap TwoTone(int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.FillRectangle(Brushes.Red, 0, 0, w / 2, h);
        g.FillRectangle(Brushes.Blue, w / 2, 0, w - (w / 2), h);
        return bmp;
    }

    [Fact]
    public void Save_Png_IsLosslessAndCreatesFolder()
    {
        using var image = TwoTone(37, 21);
        string path = Path.Combine(_dir, "ichki", "a.png");

        ImageSaver.Save(image, path, ImageFileFormat.Png);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        using var back = Thumbnails.Load(path);
        Assert.NotNull(back);
        Assert.Equal(image.Size, back!.Size);
        for (int x = 0; x < 37; x += 6)
        {
            Assert.Equal(image.GetPixel(x, 10).ToArgb(), back.GetPixel(x, 10).ToArgb());
        }

        // PNG signaturasi
        byte[] head = File.ReadAllBytes(path)[..4];
        Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, head);
    }

    [Fact]
    public void Save_Jpg_WritesJpegWithCloseColors()
    {
        using var image = TwoTone(200, 100);
        string path = Path.Combine(_dir, "b.jpg");

        ImageSaver.Save(image, path, ImageFileFormat.Jpg);

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
        using var back = Thumbnails.Load(path);
        Assert.NotNull(back);
        Assert.Equal(new Size(200, 100), back!.Size);
        var red = back.GetPixel(40, 50);
        var blue = back.GetPixel(160, 50);
        Assert.True(red.R > 220 && red.G < 40 && red.B < 40, red.ToString());
        Assert.True(blue.B > 220 && blue.R < 40 && blue.G < 40, blue.ToString());
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        string path = Path.Combine(_dir, "c.png");
        using (var first = TwoTone(10, 10))
        {
            ImageSaver.Save(first, path, ImageFileFormat.Png);
        }

        using (var second = TwoTone(30, 5))
        {
            ImageSaver.Save(second, path, ImageFileFormat.Png);
        }

        using var back = Thumbnails.Load(path);
        Assert.Equal(new Size(30, 5), back!.Size);
    }

    [Fact]
    public void Save_IntoInvalidPath_ThrowsSaveError_AndLeavesNoTempFile()
    {
        using var image = TwoTone(10, 10);
        Directory.CreateDirectory(_dir);
        string blocker = Path.Combine(_dir, "fayl");
        File.WriteAllText(blocker, "x"); // papka o'rnida oddiy fayl — ichiga yozib bo'lmaydi

        var ex = Record.Exception(() => ImageSaver.Save(image, Path.Combine(blocker, "d.png"), ImageFileFormat.Png));

        Assert.NotNull(ex);
        Assert.True(ImageSaver.IsSaveError(ex!), ex!.GetType().FullName);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Load_DoesNotLockFile()
    {
        string path = Path.Combine(_dir, "e.png");
        using (var image = TwoTone(8, 8))
        {
            ImageSaver.Save(image, path, ImageFileFormat.Png);
        }

        using var back = Thumbnails.Load(path);
        File.Delete(path); // rasm ochiq turganda ham fayl o'chirilishi mumkin bo'lishi kerak
        Assert.False(File.Exists(path));
        Assert.Equal(8, back!.Width);
    }

    [Fact]
    public void Load_And_Create_ReturnNull_ForMissingOrCorruptFiles()
    {
        Directory.CreateDirectory(_dir);
        string broken = Path.Combine(_dir, "buzuq.png");
        File.WriteAllText(broken, "bu rasm emas");

        Assert.Null(Thumbnails.Load(Path.Combine(_dir, "yoq.png")));
        Assert.Null(Thumbnails.Load(broken));
        Assert.Null(Thumbnails.Create(Path.Combine(_dir, "yoq.png"), new Size(50, 30)));
        Assert.Null(Thumbnails.Create(broken, new Size(50, 30)));
    }

    [Fact]
    public void Thumbnail_KeepsAspectRatio_Centered()
    {
        // Keng rasm (400×100) 160×100 rasmchaga: 160×40, tepa va pastda fon.
        string path = Path.Combine(_dir, "keng.png");
        using (var image = TwoTone(400, 100))
        {
            ImageSaver.Save(image, path, ImageFileFormat.Png);
        }

        using var thumb = Thumbnails.Create(path, new Size(160, 100));

        Assert.NotNull(thumb);
        Assert.Equal(new Size(160, 100), thumb!.Size);
        var background = Color.FromArgb(243, 243, 243).ToArgb();
        Assert.Equal(background, thumb.GetPixel(80, 10).ToArgb());   // tepada fon
        Assert.Equal(background, thumb.GetPixel(80, 90).ToArgb());   // pastda fon
        var left = thumb.GetPixel(30, 50);
        var right = thumb.GetPixel(130, 50);
        Assert.True(left.R > 200 && left.B < 60, left.ToString());   // markazda rasm: chapi qizil
        Assert.True(right.B > 200 && right.R < 60, right.ToString()); // o'ngi ko'k
    }

    [Fact]
    public void Thumbnail_OfSmallImage_IsNotEnlarged_AndKeepsExactColors()
    {
        // 1×1 va 10×6 rasmlar 64×40 rasmchada asl o'lchamda, markazda, rangi aynan saqlanadi (xiralik yo'q).
        string one = Path.Combine(_dir, "bir.png");
        using (var image = new Bitmap(1, 1))
        {
            image.SetPixel(0, 0, Color.Lime);
            ImageSaver.Save(image, one, ImageFileFormat.Png);
        }

        using (var thumb = Thumbnails.Create(one, new Size(64, 40)))
        {
            Assert.NotNull(thumb);
            Assert.Equal(Color.Lime.ToArgb(), thumb!.GetPixel(31, 19).ToArgb());
            Assert.Equal(Color.FromArgb(243, 243, 243).ToArgb(), thumb.GetPixel(33, 19).ToArgb());
        }

        string small = Path.Combine(_dir, "kichik.png");
        using (var image = TwoTone(10, 6))
        {
            ImageSaver.Save(image, small, ImageFileFormat.Png);
        }

        using var t = Thumbnails.Create(small, new Size(64, 40));
        Assert.Equal(Color.Red.ToArgb(), t!.GetPixel(27, 17).ToArgb());  // x 27..31 qizil
        Assert.Equal(Color.Blue.ToArgb(), t.GetPixel(36, 22).ToArgb());  // x 32..36 ko'k
        Assert.Equal(Color.FromArgb(243, 243, 243).ToArgb(), t.GetPixel(26, 17).ToArgb());
        Assert.Equal(Color.FromArgb(243, 243, 243).ToArgb(), t.GetPixel(37, 17).ToArgb());
    }
}
