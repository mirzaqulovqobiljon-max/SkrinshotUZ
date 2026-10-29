using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Ocr;
using SkrinshotUZ.Editing;
using SkrinshotUZ.Ocr;

namespace SkrinshotUZ.Windows.Tests.Ocr;

/// <summary>
/// "Matnni Word'ga" haqiqiy Tesseract bilan: .exe ichidagi fayllar, kutubxonalarni yuklash, lotin va kirill matn,
/// foydalanuvchi papkasi kirill harflarda bo'lgan holat, .docx natija.
/// CI'da OCR fayllari doim joylanadi — yo'q bo'lsa, bu yig'ish xatosi (test yiqiladi).
/// </summary>
public class OcrTests
{
    [Fact]
    public void EmbeddedFiles_Complete()
    {
        var missing = OcrFiles.Missing(OcrService.EmbeddedFiles.ToList());

        Assert.Empty(missing);
        Assert.True(OcrService.IsAvailable);
    }

    [Fact]
    public void Recognizes_LatinAndCyrillic_From96DpiScreenshot()
    {
        using var image = SmokeTest.RenderSampleText();

        var result = OcrService.Recognize(image, 96);
        string text = string.Join("\n", result.Paragraphs.SelectMany(p => p.Lines));

        Assert.Contains("Skrinshot", text);
        Assert.Contains("2026", text);
        Assert.Contains("Республик", text);
        Assert.InRange(result.VisibleChars, 30, 200);
    }

    [Fact]
    public void Recognizes_SmallText_At150PercentScaling()
    {
        // 150% masshtabdagi monitor: harflar kattaroq, DPI 144 — kattalashtirish 2 marta.
        using var image = SmokeTest.RenderSampleText(fontPx: 24f);

        var result = OcrService.Recognize(image, 144);

        Assert.Contains("Skrinshot", string.Join(" ", result.Paragraphs.SelectMany(p => p.Lines)));
    }

    [Fact]
    public void DarkMode_LightTextOnDarkBackground_Recognized()
    {
        using var light = SmokeTest.RenderSampleText();
        using var dark = new Bitmap(light.Width, light.Height, PixelFormat.Format24bppRgb);
        for (int y = 0; y < light.Height; y++)
        {
            for (int x = 0; x < light.Width; x++)
            {
                var c = light.GetPixel(x, y);
                dark.SetPixel(x, y, Color.FromArgb(255 - c.R, 255 - c.G, 255 - c.B)); // oq matn, qora fon
            }
        }

        var result = OcrService.Recognize(dark, 96);

        Assert.Contains("Skrinshot", string.Join(" ", result.Paragraphs.SelectMany(p => p.Lines)));
    }

    [Fact]
    public void BlankImage_NoText()
    {
        using var image = new Bitmap(300, 120, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.White);
        }

        var result = OcrService.Recognize(image, 96);

        Assert.Equal(0, result.VisibleChars);
    }

    [Fact]
    public void TinyImage_1x1_DoesNotCrash()
    {
        using var image = new Bitmap(1, 1, PixelFormat.Format24bppRgb);

        var result = OcrService.Recognize(image, 96);

        Assert.Equal(0, result.VisibleChars);
    }

    [Fact]
    public void TessdataInCyrillicUserFolder_StillLoads()
    {
        // C:\Users\Ўқитувчи\... — Tesseract yo'lni ANSI'da ochadi; joriy papka orqali nisbiy yo'l bilan ishlashi kerak.
        string root = Path.Combine(Path.GetTempPath(), "Ўқитувчи ocr " + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            string dir = OcrFiles.EnsureExtracted(root, OcrService.EmbeddedFiles, "test");
            var ocr = TesseractOcr.Load(dir);
            using var image = SmokeTest.RenderSampleText();

            string text = ocr.Recognize(image, 96);

            Assert.Contains("Skrinshot", text);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Recognize_RestoresCurrentDirectory()
    {
        string before = Environment.CurrentDirectory;
        using var image = SmokeTest.RenderSampleText();

        OcrService.Recognize(image, 96);

        Assert.Equal(before, Environment.CurrentDirectory);
    }

    [Fact]
    public void SaveDocument_WritesMatnDocx_WithRecognizedText()
    {
        string dir = Path.Combine(Path.GetTempPath(), "suz-ocr-doc-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var image = SmokeTest.RenderSampleText();
            var result = OcrService.Recognize(image, 96);

            string path = OcrService.SaveDocument(result, dir);

            Assert.StartsWith("matn_", Path.GetFileName(path));
            Assert.EndsWith(".docx", path);
            using var zip = ZipFile.OpenRead(path);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            Assert.Contains("Skrinshot", reader.ReadToEnd());

            string second = OcrService.SaveDocument(result, dir);
            Assert.NotEqual(path, second); // bir soniyada ikkinchisi — yangi nom, eskisi buzilmaydi
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
    public void SmokeCheck_ReportsOk()
    {
        string report = SmokeTest.RunOcrCheck();

        Assert.StartsWith("ok ", report);
    }

    [Fact]
    public void Toolbar_HasOcrButton_ThatRaisesEvent()
    {
        using var bars = new EditorToolbars();
        bars.SetScale(1.0);
        var button = bars.Actions.Items.OfType<ToolStripButton>().Single(b => b.ToolTipText == Strings.Action_Ocr);
        int clicks = 0;
        bars.OcrClicked += (_, _) => clicks++;

        button.PerformClick();

        Assert.Equal(1, clicks);
        Assert.NotNull(button.Image);
    }
}
