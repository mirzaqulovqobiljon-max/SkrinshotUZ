using System.Globalization;
using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Core.Tests.Localization;

/// <summary>"Matnni Word'ga" (OCR) matnlari.</summary>
public class OcrStringsTests
{
    [Fact]
    public void ToolbarTooltip_ShowsShortcut()
    {
        Assert.Equal("Matnni Word'ga (Ctrl+T)", Strings.Action_Ocr);
        Assert.Equal("Matnni Word'ga", Strings.History_Ocr);
    }

    [Fact]
    public void Messages_WithFileName_FormatCorrectly()
    {
        foreach (string template in new[] { Strings.Ocr_Done, Strings.Ocr_SavedNoViewer, Strings.Ocr_Failed })
        {
            string text = string.Format(CultureInfo.InvariantCulture, template, "matn_2026-10-29_19-40-00.docx");
            Assert.Contains("matn_2026-10-29_19-40-00.docx", text);
        }
    }

    [Fact]
    public void BalloonTexts_FitWindowsLimit()
    {
        // Windows bildirishnoma matni 255 belgigacha; uzun fayl nomi bilan ham sig'sin.
        string name = new string('x', 60) + ".docx";
        Assert.InRange(string.Format(CultureInfo.InvariantCulture, Strings.Ocr_Done, name).Length, 1, 255);
        Assert.InRange(string.Format(CultureInfo.InvariantCulture, Strings.Ocr_SavedNoViewer, name).Length, 1, 255);
    }
}
