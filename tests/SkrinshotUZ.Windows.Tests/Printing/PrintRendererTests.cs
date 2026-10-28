using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Printing;
using SkrinshotUZ.Printing;
using SkrinshotUZ.Storage;
using SkrinshotUZ.UI;

namespace SkrinshotUZ.Windows.Tests.Printing;

/// <summary>
/// Chop etish chizmasi 300 DPI "qog'oz"ga (Bitmap) chiziladi va rasm haqiqatan kutilgan millimetrda ekani
/// piksellar bo'yicha o'lchanadi (±0.5 mm) — printerdagi bilan bir xil kod.
/// </summary>
public class PrintRendererTests
{
    private const float PaperDpi = 300f;
    private const double TolMm = 0.5;

    private static double PxToMm(double px) => px * 25.4 / PaperDpi;

    private static int MmToPx(double mm) => (int)Math.Round(mm * PaperDpi / 25.4);

    private static Bitmap Solid(int w, int h, Color color)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(color);
        return bmp;
    }

    /// <summary>"Qog'oz": berilgan mm o'lchamdagi oq Bitmap, 300 DPI.</summary>
    private static Bitmap Paper(double widthMm, double heightMm)
    {
        var bmp = new Bitmap(MmToPx(widthMm), MmToPx(heightMm), PixelFormat.Format24bppRgb);
        bmp.SetResolution(PaperDpi, PaperDpi);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        return bmp;
    }

    private static Bitmap Render(PrintPage page, IReadOnlyList<Image> images, PointF origin = default, double? widthMm = null, double? heightMm = null, bool grayscale = false)
    {
        var paper = Paper(widthMm ?? page.Size.Width, heightMm ?? page.Size.Height);
        using var g = Graphics.FromImage(paper);
        PrintRenderer.DrawPage(g, page, images, origin, grayscale, showMargins: false);
        return paper;
    }

    /// <summary>Qorong'i piksellar chegarasi (mm) — berilgan Y oralig'ida.</summary>
    private static RectMm DarkBounds(Bitmap bmp, int threshold = 100, int yFrom = 0, int yTo = int.MaxValue)
    {
        var data = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[data.Stride];
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = Math.Max(0, yFrom); y < Math.Min(bmp.Height, yTo); y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, data.Stride);
                for (int x = 0; x < bmp.Width; x++)
                {
                    int i = x * 3;
                    if ((row[i] + row[i + 1] + row[i + 2]) / 3 < threshold)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            Assert.True(maxX >= 0, "qog'ozda hech narsa chizilmagan");
            return new RectMm(PxToMm(minX), PxToMm(minY), PxToMm(maxX - minX + 1), PxToMm(maxY - minY + 1));
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static void AssertMm(RectMm expected, RectMm actual)
    {
        Assert.True(Math.Abs(expected.X - actual.X) <= TolMm, $"X: kutilgan {expected.X:0.00} mm, chizilgan {actual.X:0.00} mm");
        Assert.True(Math.Abs(expected.Y - actual.Y) <= TolMm, $"Y: kutilgan {expected.Y:0.00} mm, chizilgan {actual.Y:0.00} mm");
        Assert.True(Math.Abs(expected.Width - actual.Width) <= TolMm, $"Kenglik: kutilgan {expected.Width:0.00} mm, chizilgan {actual.Width:0.00} mm");
        Assert.True(Math.Abs(expected.Height - actual.Height) <= TolMm, $"Balandlik: kutilgan {expected.Height:0.00} mm, chizilgan {actual.Height:0.00} mm");
    }

    private static PrintPlan Plan(int w, int h, double dpi, PrintOptions options, HardMargins hard = default, PrintImage? second = null) =>
        PrintPlanner.Create(new PrintImage(w, h, dpi), second, options, hard);

    [Fact]
    public void FullPage_Portrait_ImageExactlyWhereAndHowBigPlanned()
    {
        using var image = Solid(400, 300, Color.Black);
        var plan = Plan(400, 300, 96, new PrintOptions { Orientation = OrientationChoice.Portrait });

        using var paper = Render(plan.Pages[0], new[] { image });

        var expected = plan.Pages[0].Images[0].Dest;
        Assert.Equal(190, expected.Width, 6);
        Assert.Equal(142.5, expected.Height, 6);
        AssertMm(expected, DarkBounds(paper));
    }

    [Fact]
    public void FullPage_Landscape_FullHd()
    {
        using var image = Solid(1920, 1080, Color.Black);
        var plan = Plan(1920, 1080, 96, new PrintOptions());

        Assert.Equal(PageOrientation.Landscape, plan.Pages[0].Orientation);
        using var paper = Render(plan.Pages[0], new[] { image });

        AssertMm(plan.Pages[0].Images[0].Dest, DarkBounds(paper));
        Assert.Equal(277, DarkBounds(paper).Width, 0); // ±0.5 mm
    }

    [Fact]
    public void HardMargins_GraphicsOriginAtPrintableArea_ImageStillAtPlannedPaperPosition()
    {
        // Haqiqiy printer: Graphics (0,0) — qog'oz chetida emas, bosilmaydigan chetdan keyin (masalan 4.23 × 6.35 mm).
        var hard = new HardMargins(4.23, 6.35, 4.23, 12.7);
        using var image = Solid(800, 600, Color.Black);
        var plan = Plan(800, 600, 96, new PrintOptions { Orientation = OrientationChoice.Portrait, MarginMm = 0 }, hard);
        var dest = plan.Pages[0].Images[0].Dest;
        Assert.Equal(4.23, dest.X, 6); // maydon 0 bo'lsa ham printer cheti hisobga olingan

        var origin = new PointF((float)hard.Left, (float)hard.Top);
        using var printable = Render(plan.Pages[0], new[] { image }, origin, 210 - hard.Left - hard.Right, 297 - hard.Top - hard.Bottom);

        var measured = DarkBounds(printable);
        AssertMm(dest, measured.Offset(hard.Left, hard.Top));
        Assert.True(measured.X >= 0 && measured.Right <= PxToMm(printable.Width) + TolMm);
    }

    [Theory]
    [InlineData(378, 96.0)]
    [InlineData(473, 120.0)]
    [InlineData(567, 144.0)]
    [InlineData(756, 192.0)]
    public void ActualSize_PrintsAtScreenSize(int px, double dpi)
    {
        using var image = Solid(px, px / 2, Color.Black);
        var plan = Plan(px, px / 2, dpi, new PrintOptions { Mode = PrintMode.ActualSize });

        using var paper = Render(plan.Pages[0], new[] { image });

        var measured = DarkBounds(paper);
        double expectedWidth = px / (dpi / 96.0) * 25.4 / 96.0;
        Assert.True(Math.Abs(measured.Width - expectedWidth) <= TolMm, $"{measured.Width:0.00} mm ≠ {expectedWidth:0.00} mm");
        AssertMm(plan.Pages[0].Images[0].Dest, measured);
    }

    [Fact]
    public void ActualSize_Split_EveryTileAtActualSize()
    {
        // Kitob, maydon 10 mm: 190 mm = 718 px → 1200 px = 718 + 482 (2 varaq).
        using var image = Solid(1200, 300, Color.Black);
        var plan = Plan(1200, 300, 96, new PrintOptions { Mode = PrintMode.ActualSize, Orientation = OrientationChoice.Portrait, Overflow = OverflowChoice.SplitPages });

        Assert.Equal(2, plan.Pages.Count);
        foreach (var page in plan.Pages)
        {
            using var paper = Render(page, new[] { image });
            AssertMm(page.Images[0].Dest, DarkBounds(paper));
        }
    }

    [Fact]
    public void HalfPage_StaysAboveMiddle()
    {
        using var image = Solid(1920, 1080, Color.Black);
        var plan = Plan(1920, 1080, 96, new PrintOptions { Mode = PrintMode.HalfPage });

        using var paper = Render(plan.Pages[0], new[] { image });

        var measured = DarkBounds(paper);
        AssertMm(plan.Pages[0].Images[0].Dest, measured);
        Assert.True(measured.Bottom < 148.5 - 9.5);
    }

    [Fact]
    public void TwoPerPage_TwoImages_AndDashedCutLineInMiddle()
    {
        using var top = Solid(600, 400, Color.Black);
        using var bottom = Solid(300, 600, Color.Black);
        var plan = Plan(600, 400, 96, new PrintOptions { Mode = PrintMode.TwoPerPage }, second: new PrintImage(300, 600, 96));
        var page = plan.Pages[0];

        using var paper = Render(page, new Image[] { top, bottom });

        int middle = MmToPx(148.5);
        AssertMm(page.Images[0].Dest, DarkBounds(paper, yTo: middle - MmToPx(3)));
        AssertMm(page.Images[1].Dest, DarkBounds(paper, yFrom: middle + MmToPx(3)));

        // Kesish chizig'i: o'rtadagi qatorda kulrang punktir (bo'laklar va bo'shliqlar).
        int gray = 0, white = 0;
        for (int x = MmToPx(20); x < MmToPx(200); x++)
        {
            var c = paper.GetPixel(x, middle);
            if (c.R < 200)
            {
                gray++;
            }
            else
            {
                white++;
            }
        }

        Assert.True(gray > 200, $"chiziq ko'rinmayapti ({gray})");
        Assert.True(white > 100, $"chiziq punktir emas ({white})");
    }

    [Fact]
    public void Grayscale_RedBecomesGray()
    {
        using var image = Solid(200, 100, Color.Red);
        var plan = Plan(200, 100, 96, new PrintOptions());

        using var paper = Render(plan.Pages[0], new[] { image }, grayscale: true);

        var dest = plan.Pages[0].Images[0].Dest;
        var c = paper.GetPixel(MmToPx(dest.X + (dest.Width / 2)), MmToPx(dest.Y + (dest.Height / 2)));
        Assert.True(Math.Abs(c.R - c.G) <= 2 && Math.Abs(c.G - c.B) <= 2, c.ToString());
        Assert.InRange(c.R, 70, 85); // 0.299 × 255 ≈ 76

        using var color = Render(plan.Pages[0], new[] { image });
        var cc = color.GetPixel(MmToPx(dest.X + (dest.Width / 2)), MmToPx(dest.Y + (dest.Height / 2)));
        Assert.True(cc.R > 240 && cc.G < 15, cc.ToString());
    }

    [Fact]
    public void Caption_DrawnBelowImage_InsideItsRect()
    {
        using var image = Solid(400, 300, Color.White); // rasm oq — qog'ozdagi qora piksellar faqat yozuvniki
        var plan = Plan(400, 300, 96, new PrintOptions { Orientation = OrientationChoice.Portrait, Caption = "7-B sinf · 28.10.2026" });
        var caption = plan.Pages[0].Captions[0];

        using var paper = Render(plan.Pages[0], new[] { image });

        var text = DarkBounds(paper, threshold: 140);
        Assert.True(text.Y >= caption.Rect.Y - TolMm, $"yozuv rasm ustiga chiqib ketgan: {text.Y:0.0} < {caption.Rect.Y:0.0}");
        Assert.True(text.Bottom <= caption.Rect.Bottom + TolMm);
        Assert.True(text.Width > 20 && text.Width < 120, text.Width.ToString());
        Assert.Equal(105, text.X + (text.Width / 2), 0); // markazda (±0.5 mm)
    }

    [Fact]
    public void Preview_ShowsMarginGuides_PrintDoesNot()
    {
        using var image = Solid(10, 10, Color.White);
        var page = Plan(10, 10, 96, new PrintOptions { Orientation = OrientationChoice.Portrait }).Pages[0];

        using var preview = Paper(210, 297);
        using (var g = Graphics.FromImage(preview))
        {
            PrintRenderer.DrawPage(g, page, new[] { image }, PointF.Empty, grayscale: false, showMargins: true);
        }

        using var print = Render(page, new[] { image });

        var guides = DarkBounds(preview, threshold: 235);
        AssertMm(new RectMm(10, 10, 190, 277), guides);
        // Yuqori maydon chizig'i (y = 10 mm) — rasmdan tashqarida (rasm 53.5 mm dan boshlanadi).
        // Punktir bo'lgani uchun bitta nuqta emas, 5 mm bo'lak tekshiriladi.
        var span = Enumerable.Range(MmToPx(100), MmToPx(5)).ToList();
        Assert.Contains(span, x => preview.GetPixel(x, MmToPx(10)).R < 235);
        Assert.All(span, x => Assert.Equal(Color.White.ToArgb(), print.GetPixel(x, MmToPx(10)).ToArgb())); // printerda chegara chizilmaydi
    }

    [Fact]
    public void FromHundredths_ComputesAllFourEdges()
    {
        var paper = new PaperSize("A4", 827, 1169);
        var hard = Printers.FromHundredths(16.7f, 25f, new RectangleF(16.7f, 25f, 793.6f, 1094f), paper);

        Assert.Equal(16.7 * 0.254, hard.Left, 3);
        Assert.Equal(25 * 0.254, hard.Top, 3);
        Assert.Equal((827 - 16.7 - 793.6) * 0.254, hard.Right, 3);
        Assert.Equal((1169 - 25 - 1094) * 0.254, hard.Bottom, 3);

        var none = Printers.FromHundredths(0, 0, new RectangleF(0, 0, 900, 1200), paper);
        Assert.Equal(HardMargins.None, none); // manfiy bo'lmaydi
    }

    [Theory]
    [InlineData(96f)]
    [InlineData(144f)]
    [InlineData(192f)]
    public void ImageDpi_SurvivesSaveAndLoad_PngAndJpg(float dpi)
    {
        string dir = Path.Combine(Path.GetTempPath(), "SkrinshotUZ.dpi." + Guid.NewGuid().ToString("N"));
        try
        {
            using var image = Solid(50, 40, Color.Black);
            ImageDpi.Set(image, dpi);
            foreach (var (name, format) in new[] { ("a.png", Core.Storage.ImageFileFormat.Png), ("a.jpg", Core.Storage.ImageFileFormat.Jpg) })
            {
                string path = Path.Combine(dir, name);
                ImageSaver.Save(image, path, format);
                using var back = Thumbnails.Load(path);
                Assert.NotNull(back);
                Assert.Equal(dpi, ImageDpi.Of(back!), 0);
            }
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void ImageDpi_ImplausibleValues_FallBackTo96()
    {
        using var image = Solid(5, 5, Color.Black);
        image.SetResolution(5000, 5000);
        Assert.Equal(96, ImageDpi.Of(image));
        ImageDpi.Set(image, 0);
        Assert.Equal(96, ImageDpi.Of(image));
    }

    [Fact]
    public void ModeIcons_AreDistinct()
    {
        var hashes = new HashSet<string>();
        foreach (PrintMode mode in Enum.GetValues<PrintMode>())
        {
            using var icon = PrintForm.ModeIcon(mode, 40);
            Assert.True(icon.Height >= 40);
            using var ms = new MemoryStream();
            icon.Save(ms, ImageFormat.Png);
            hashes.Add(Convert.ToBase64String(ms.ToArray()));
        }

        Assert.Equal(4, hashes.Count);
    }
}
