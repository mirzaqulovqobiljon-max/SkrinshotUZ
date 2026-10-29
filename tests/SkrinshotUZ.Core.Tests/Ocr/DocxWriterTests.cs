using System.IO.Compression;
using System.Xml.Linq;
using SkrinshotUZ.Core.Ocr;

namespace SkrinshotUZ.Core.Tests.Ocr;

/// <summary>.docx paketi to'g'ri tuzilganmi: ZIP qismlari, bog'lanishlar, XML va matn.</summary>
public class DocxWriterTests
{
    private static readonly XNamespace W = DocxWriter.W;
    private static readonly DateTime Created = new(2026, 10, 29, 14, 40, 0, DateTimeKind.Utc);

    [Fact]
    public void Package_HasAllRequiredParts()
    {
        using var zip = Build(OcrText.ToParagraphs("Salom"));

        var names = zip.Entries.Select(e => e.FullName).ToHashSet();
        foreach (string part in new[] { "[Content_Types].xml", "_rels/.rels", "word/document.xml", "word/_rels/document.xml.rels", "word/styles.xml", "docProps/core.xml" })
        {
            Assert.Contains(part, names);
        }
    }

    [Fact]
    public void AllParts_AreWellFormedXml_WithoutBom()
    {
        using var zip = Build(OcrText.ToParagraphs("a\nb\n\nc"));

        foreach (var e in zip.Entries)
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            byte[] bytes = ms.ToArray();
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, $"{e.FullName}: BOM");
            ms.Position = 0;
            _ = XDocument.Load(ms); // xato bo'lsa — test yiqiladi
        }
    }

    [Fact]
    public void ContentTypes_And_Relationships_PointToExistingParts()
    {
        using var zip = Build(OcrText.ToParagraphs("x"));
        var names = zip.Entries.Select(e => "/" + e.FullName).ToHashSet();

        var types = Load(zip, "[Content_Types].xml");
        foreach (var o in types.Root!.Elements().Where(e => e.Name.LocalName == "Override"))
        {
            Assert.Contains((string)o.Attribute("PartName")!, names);
        }

        var rels = Load(zip, "_rels/.rels");
        var main = rels.Root!.Elements().Single(r => ((string)r.Attribute("Type")!).EndsWith("/officeDocument", StringComparison.Ordinal));
        Assert.Equal("word/document.xml", (string)main.Attribute("Target")!);

        var docRels = Load(zip, "word/_rels/document.xml.rels");
        Assert.Contains(docRels.Root!.Elements(), r => (string)r.Attribute("Target")! == "styles.xml");
    }

    [Fact]
    public void Paragraphs_And_LineBreaks_MatchInput()
    {
        using var zip = Build(OcrText.ToParagraphs("1-savol\nA) Toshkent\n\nIkkinchi"));

        var body = Load(zip, "word/document.xml").Root!.Element(W + "body")!;
        var paragraphs = body.Elements(W + "p").ToList();

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal(new[] { "1-savol", "A) Toshkent" }, LinesOf(paragraphs[0]));
        Assert.Equal(new[] { "Ikkinchi" }, LinesOf(paragraphs[1]));
        Assert.Single(paragraphs[0].Descendants(W + "br"));
    }

    [Fact]
    public void SpecialCharacters_Escaped_SpacesPreserved_UzbekCyrillicKept()
    {
        const string line = "  a < b & \"c\" > 'd'  Oʻzbek — Ўзбек 😊";
        using var zip = Build(new[] { new OcrParagraph(new[] { line }) });

        var t = Load(zip, "word/document.xml").Descendants(W + "t").Single();
        Assert.Equal(line, t.Value);
        Assert.Equal("preserve", (string)t.Attribute(XNamespace.Xml + "space")!);
    }

    [Fact]
    public void Tabs_BecomeWordTabs()
    {
        using var zip = Build(new[] { new OcrParagraph(new[] { "Ism\tFamiliya" }) });

        var run = Load(zip, "word/document.xml").Descendants(W + "r").Single();
        Assert.Equal(new[] { "t", "tab", "t" }, run.Elements().Select(e => e.Name.LocalName).ToArray());
        Assert.Equal("IsmFamiliya", string.Concat(run.Elements(W + "t").Select(e => e.Value)));
    }

    [Fact]
    public void InvalidXmlCharacters_Removed()
    {
        Assert.Equal("ab", DocxWriter.RemoveInvalidXmlChars("a\u0000\u0008￾b"));
        Assert.Equal("x😊", DocxWriter.RemoveInvalidXmlChars("x😊"));
        Assert.Equal("ok", DocxWriter.RemoveInvalidXmlChars("ok\uD800"));   // juftsiz surrogat

        using var zip = Build(new[] { new OcrParagraph(new[] { "a\u0001b" }) });
        Assert.Equal("ab", Load(zip, "word/document.xml").Descendants(W + "t").Single().Value);
    }

    [Fact]
    public void Defaults_TimesNewRoman14_UzbekLanguage_A4WithOfficeMargins()
    {
        using var zip = Build(OcrText.ToParagraphs("x"));

        var rPr = Load(zip, "word/styles.xml").Descendants(W + "rPrDefault").Single().Element(W + "rPr")!;
        Assert.Equal("Times New Roman", (string)rPr.Element(W + "rFonts")!.Attribute(W + "ascii")!);
        Assert.Equal("28", (string)rPr.Element(W + "sz")!.Attribute(W + "val")!);   // yarim punktlarda
        Assert.Equal("uz-Latn-UZ", (string)rPr.Element(W + "lang")!.Attribute(W + "val")!);

        var sect = Load(zip, "word/document.xml").Descendants(W + "sectPr").Single();
        Assert.Equal("11906", (string)sect.Element(W + "pgSz")!.Attribute(W + "w")!);   // 210 mm
        Assert.Equal("16838", (string)sect.Element(W + "pgSz")!.Attribute(W + "h")!);   // 297 mm
        Assert.Equal("1701", (string)sect.Element(W + "pgMar")!.Attribute(W + "left")!);   // 30 mm
    }

    [Fact]
    public void CustomFont_Applied()
    {
        using var zip = Build(OcrText.ToParagraphs("x"), new DocxOptions("Arial", 12.5, "ru-RU"));

        var rPr = Load(zip, "word/styles.xml").Descendants(W + "rPrDefault").Single().Element(W + "rPr")!;
        Assert.Equal("Arial", (string)rPr.Element(W + "rFonts")!.Attribute(W + "hAnsi")!);
        Assert.Equal("25", (string)rPr.Element(W + "sz")!.Attribute(W + "val")!);
        Assert.Equal("ru-RU", (string)rPr.Element(W + "lang")!.Attribute(W + "val")!);
    }

    [Theory]
    [InlineData("", 14.0)]
    [InlineData("Arial", 0.0)]
    [InlineData("Arial", 1000.0)]
    public void InvalidOptions_Throw(string font, double size)
    {
        Assert.Throws<ArgumentException>(() => DocxWriter.Write(new MemoryStream(), OcrText.ToParagraphs("x"), Created, new DocxOptions(font, size)));
    }

    [Fact]
    public void NoParagraphs_StillValidDocumentWithOneEmptyParagraph()
    {
        using var zip = Build(Array.Empty<OcrParagraph>());

        Assert.Single(Load(zip, "word/document.xml").Descendants(W + "p"));
    }

    [Fact]
    public void CoreProperties_HaveCreatedDateAndNoPersonalData()
    {
        using var zip = Build(OcrText.ToParagraphs("x"));

        var core = Load(zip, "docProps/core.xml");
        XNamespace dcterms = "http://purl.org/dc/terms/";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        Assert.Equal("2026-10-29T14:40:00Z", core.Descendants(dcterms + "created").Single().Value);
        Assert.Equal("Skrinshot UZ", core.Descendants(dc + "creator").Single().Value);
    }

    [Fact]
    public void WriteFile_CreatesFile_AndNeverOverwrites()
    {
        string dir = Path.Combine(Path.GetTempPath(), "suz-docx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "matn.docx");
            DocxWriter.WriteFile(path, OcrText.ToParagraphs("Salom"), Created);
            long size = new FileInfo(path).Length;
            Assert.True(size > 500, $"hajm {size}");

            using (var zip = ZipFile.OpenRead(path))
            {
                Assert.Equal("Salom", Load(zip, "word/document.xml").Descendants(W + "t").Single().Value);
            }

            Assert.ThrowsAny<IOException>(() => DocxWriter.WriteFile(path, OcrText.ToParagraphs("Boshqa"), Created));
            Assert.Equal(size, new FileInfo(path).Length); // mavjud fayl buzilmadi va o'chirilmadi
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteFile_FailureInTheMiddle_LeavesNoHalfFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "suz-docx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "matn.docx");
            Assert.Throws<ArgumentException>(() => DocxWriter.WriteFile(path, OcrText.ToParagraphs("x"), Created, new DocxOptions(string.Empty)));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static ZipArchive Build(IReadOnlyList<OcrParagraph> paragraphs, DocxOptions? options = null)
    {
        var ms = new MemoryStream();
        DocxWriter.Write(ms, paragraphs, Created, options);
        ms.Position = 0;
        return new ZipArchive(ms, ZipArchiveMode.Read);
    }

    private static XDocument Load(ZipArchive zip, string name)
    {
        using var s = zip.GetEntry(name)!.Open();
        return XDocument.Load(s);
    }

    /// <summary>Xatboshidagi qatorlar (w:br bo'yicha ajratilgan).</summary>
    private static string[] LinesOf(XElement paragraph)
    {
        var lines = new List<string> { string.Empty };
        foreach (var e in paragraph.Descendants())
        {
            if (e.Name == W + "br")
            {
                lines.Add(string.Empty);
            }
            else if (e.Name == W + "t")
            {
                lines[^1] += e.Value;
            }
        }

        return lines.ToArray();
    }
}
