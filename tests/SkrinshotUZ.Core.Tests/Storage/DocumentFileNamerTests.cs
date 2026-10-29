using SkrinshotUZ.Core.Storage;

namespace SkrinshotUZ.Core.Tests.Storage;

/// <summary>OCR natijasi — Word hujjati nomi: <c>matn_2026-10-29_19-40-00.docx</c>.</summary>
public class DocumentFileNamerTests
{
    private static readonly DateTime Stamp = new(2026, 10, 29, 19, 40, 0);

    [Fact]
    public void Name_HasMatnPrefixTimestampAndDocx()
    {
        string path = ScreenshotFileNamer.CreateUniqueDocumentPath("papka", Stamp, _ => false);

        Assert.Equal(Path.Combine("papka", "matn_2026-10-29_19-40-00.docx"), path);
    }

    [Fact]
    public void Duplicate_GetsNumberSuffix()
    {
        var taken = new HashSet<string>
        {
            Path.Combine("p", "matn_2026-10-29_19-40-00.docx"),
            Path.Combine("p", "matn_2026-10-29_19-40-00_2.docx"),
        };

        Assert.Equal(Path.Combine("p", "matn_2026-10-29_19-40-00_3.docx"), ScreenshotFileNamer.CreateUniqueDocumentPath("p", Stamp, taken.Contains));
    }

    [Fact]
    public void ScreenshotNames_Unchanged()
    {
        Assert.Equal(Path.Combine("p", "skrinshot_2026-10-29_19-40-00.png"), ScreenshotFileNamer.CreateUniquePath("p", Stamp, ImageFileFormat.Png, _ => false));
    }

    [Fact]
    public void EmptyFolder_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => ScreenshotFileNamer.CreateUniqueDocumentPath(" ", Stamp, _ => false));
    }

    [Fact]
    public void OcrFolder_IsInLocalAppData()
    {
        Assert.Equal(Path.Combine("L", "Skrinshot UZ", "ocr"), AppPaths.OcrFolder("L"));
        Assert.ThrowsAny<ArgumentException>(() => AppPaths.OcrFolder(" "));
    }

    [Fact]
    public void AllTaken_Throws()
    {
        Assert.Throws<IOException>(() => ScreenshotFileNamer.CreateUniqueDocumentPath("p", Stamp, _ => true));
    }
}
