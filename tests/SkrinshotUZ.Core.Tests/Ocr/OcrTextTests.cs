using SkrinshotUZ.Core.Ocr;

namespace SkrinshotUZ.Core.Tests.Ocr;

/// <summary>Tesseract matnini xatboshilarga ajratish.</summary>
public class OcrTextTests
{
    [Fact]
    public void EmptyOrNull_NoParagraphs()
    {
        Assert.Empty(OcrText.ToParagraphs(null));
        Assert.Empty(OcrText.ToParagraphs(string.Empty));
        Assert.Empty(OcrText.ToParagraphs("\n\n  \n\f"));
    }

    [Fact]
    public void BlankLine_SeparatesParagraphs_LinesKeptInside()
    {
        var p = OcrText.ToParagraphs("1-savol: Poytaxt?\nA) Toshkent\nB) Samarqand\n\nIkkinchi xatboshi\n");

        Assert.Equal(2, p.Count);
        Assert.Equal(new[] { "1-savol: Poytaxt?", "A) Toshkent", "B) Samarqand" }, p[0].Lines);
        Assert.Equal(new[] { "Ikkinchi xatboshi" }, p[1].Lines);
    }

    [Fact]
    public void WindowsAndOldMacLineEndings_Handled()
    {
        var p = OcrText.ToParagraphs("bir\r\nikki\r\rUch");

        Assert.Equal(2, p.Count);
        Assert.Equal(new[] { "bir", "ikki" }, p[0].Lines);
        Assert.Equal(new[] { "Uch" }, p[1].Lines);
    }

    [Fact]
    public void FormFeedAndControlChars_Removed_TabKept_TrailingSpacesTrimmed()
    {
        var p = OcrText.ToParagraphs("Ism\tFamiliya   \n\u0001Ali\u0007\tValiyev\f");

        Assert.Single(p);
        Assert.Equal(new[] { "Ism\tFamiliya", "Ali\tValiyev" }, p[0].Lines);
    }

    [Fact]
    public void WhitespaceOnlyLine_CountsAsBlank()
    {
        var p = OcrText.ToParagraphs("a\n   \t \nb");

        Assert.Equal(2, p.Count);
    }

    [Fact]
    public void UzbekAndCyrillicText_PreservedExactly()
    {
        const string text = "Oʻzbekiston — gʻalaba\nЎзбекистон Республикаси";
        var p = OcrText.ToParagraphs(text);

        Assert.Equal(new[] { "Oʻzbekiston — gʻalaba", "Ўзбекистон Республикаси" }, p[0].Lines);
    }

    [Fact]
    public void CountVisibleChars_IgnoresWhitespace()
    {
        var p = OcrText.ToParagraphs("a b\n\n c\t");

        Assert.Equal(3, OcrText.CountVisibleChars(p));
        Assert.Equal(0, OcrText.CountVisibleChars(OcrText.ToParagraphs(" \n ")));
    }
}
