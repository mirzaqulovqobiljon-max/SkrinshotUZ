using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace SkrinshotUZ.Core.Ocr;

/// <summary>Word hujjati ko'rinishi.</summary>
/// <param name="FontName">Shrift (maktab hujjatlari standarti — Times New Roman).</param>
/// <param name="FontSizePt">Shrift o'lchami, pt.</param>
/// <param name="Language">Imlo tekshiruvi tili (BCP-47).</param>
public sealed record DocxOptions(string FontName = "Times New Roman", double FontSizePt = 14, string Language = "uz-Latn-UZ");

/// <summary>
/// Oddiy .docx (Office Open XML) yozuvchi: Word, LibreOffice va WPS ochadigan minimal to'g'ri paket.
/// Tashqi kutubxonasiz (faqat ZIP + XML) — dastur hajmi oshmaydi va Word o'rnatilmagan kompyuterda ham ishlaydi.
/// Varaq — A4, maydonlar ish yuritish standartida: chap 30 mm, o'ng 15 mm, yuqori/past 20 mm.
/// </summary>
public static class DocxWriter
{
    /// <summary>WordprocessingML nomlar fazosi.</summary>
    public const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private const string PackageRels = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string OfficeDocRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string CorePropsRel = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";
    private const string StylesRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";

    /// <summary>Hujjatni oqimga yozadi (oqim ochiq qoladi).</summary>
    /// <param name="output">Yoziladigan oqim.</param>
    /// <param name="paragraphs">Xatboshilar (bo'sh bo'lsa — bitta bo'sh xatboshi).</param>
    /// <param name="created">Yaratilgan vaqt (UTC) — hujjat xususiyatlarida.</param>
    /// <param name="options">Ko'rinish (standart: Times New Roman 14).</param>
    public static void Write(Stream output, IReadOnlyList<OcrParagraph> paragraphs, DateTime created, DocxOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(paragraphs);
        options ??= new DocxOptions();
        if (string.IsNullOrWhiteSpace(options.FontName) || !(options.FontSizePt is >= 1 and <= 400))
        {
            throw new ArgumentException("Shrift nomi bo'sh yoki o'lchami noto'g'ri.", nameof(options));
        }

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        AddXml(zip, "[Content_Types].xml", w =>
        {
            w.WriteStartElement("Types", ContentTypes);
            Default(w, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            Default(w, "xml", "application/xml");
            Override(w, "/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
            Override(w, "/word/styles.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml");
            Override(w, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
            w.WriteEndElement();
        });

        AddXml(zip, "_rels/.rels", w =>
        {
            w.WriteStartElement("Relationships", PackageRels);
            Relationship(w, "rId1", OfficeDocRel, "word/document.xml");
            Relationship(w, "rId2", CorePropsRel, "docProps/core.xml");
            w.WriteEndElement();
        });

        AddXml(zip, "word/_rels/document.xml.rels", w =>
        {
            w.WriteStartElement("Relationships", PackageRels);
            Relationship(w, "rId1", StylesRel, "styles.xml");
            w.WriteEndElement();
        });

        AddXml(zip, "docProps/core.xml", w => WriteCore(w, created));
        AddXml(zip, "word/styles.xml", w => WriteStyles(w, options));
        AddXml(zip, "word/document.xml", w => WriteDocument(w, paragraphs));
    }

    /// <summary>Hujjatni yangi faylga yozadi (mavjud fayl ustidan yozilmaydi).</summary>
    /// <param name="path">To'liq yo'l (.docx).</param>
    /// <param name="paragraphs">Xatboshilar.</param>
    /// <param name="created">Yaratilgan vaqt (UTC).</param>
    /// <param name="options">Ko'rinish.</param>
    public static void WriteFile(string path, IReadOnlyList<OcrParagraph> paragraphs, DateTime created, DocxOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // CreateNew: fayl mavjud bo'lsa — xato (birovning faylini buzmaymiz va uni o'chirmaymiz).
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            using (file)
            {
                Write(file, paragraphs, created, options);
            }
        }
        catch
        {
            TryDelete(path); // yarim yozilgan fayl qolmasin
            throw;
        }
    }

    /// <summary>XML'da ruxsat etilmagan belgilarni olib tashlaydi (OCR ba'zan ularni chiqaradi).</summary>
    /// <param name="text">Matn.</param>
    /// <returns>Toza matn.</returns>
    public static string RemoveInvalidXmlChars(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                sb.Append(c).Append(text[i + 1]);
                i++;
            }
            else if (XmlConvert.IsXmlChar(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // fayl qolsa ham asosiy xato muhimroq
        }
    }

    private static void AddXml(ZipArchive zip, string name, Action<XmlWriter> body)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CheckCharacters = true };
        using var w = XmlWriter.Create(stream, settings);
        w.WriteStartDocument(standalone: true);
        body(w);
        w.WriteEndDocument();
    }

    private static void Default(XmlWriter w, string ext, string type)
    {
        w.WriteStartElement("Default", ContentTypes);
        w.WriteAttributeString("Extension", ext);
        w.WriteAttributeString("ContentType", type);
        w.WriteEndElement();
    }

    private static void Override(XmlWriter w, string part, string type)
    {
        w.WriteStartElement("Override", ContentTypes);
        w.WriteAttributeString("PartName", part);
        w.WriteAttributeString("ContentType", type);
        w.WriteEndElement();
    }

    private static void Relationship(XmlWriter w, string id, string type, string target)
    {
        w.WriteStartElement("Relationship", PackageRels);
        w.WriteAttributeString("Id", id);
        w.WriteAttributeString("Type", type);
        w.WriteAttributeString("Target", target);
        w.WriteEndElement();
    }

    private static void WriteCore(XmlWriter w, DateTime created)
    {
        const string cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        const string dc = "http://purl.org/dc/elements/1.1/";
        const string dcterms = "http://purl.org/dc/terms/";
        const string xsi = "http://www.w3.org/2001/XMLSchema-instance";
        string stamp = created.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        w.WriteStartElement("cp", "coreProperties", cp);
        w.WriteAttributeString("xmlns", "dc", null, dc);
        w.WriteAttributeString("xmlns", "dcterms", null, dcterms);
        w.WriteAttributeString("xmlns", "xsi", null, xsi);
        w.WriteElementString("dc", "title", dc, "Skrinshot UZ — aniqlangan matn");
        w.WriteElementString("dc", "creator", dc, "Skrinshot UZ");
        foreach (string name in new[] { "created", "modified" })
        {
            w.WriteStartElement("dcterms", name, dcterms);
            w.WriteAttributeString("xsi", "type", xsi, "dcterms:W3CDTF");
            w.WriteString(stamp);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void WriteStyles(XmlWriter w, DocxOptions o)
    {
        string halfPoints = ((int)Math.Round(o.FontSizePt * 2)).ToString(CultureInfo.InvariantCulture);
        w.WriteStartElement("w", "styles", W);
        w.WriteStartElement("w", "docDefaults", W);

        w.WriteStartElement("w", "rPrDefault", W);
        w.WriteStartElement("w", "rPr", W);
        w.WriteStartElement("w", "rFonts", W);
        foreach (string a in new[] { "ascii", "hAnsi", "cs", "eastAsia" })
        {
            w.WriteAttributeString("w", a, W, o.FontName);
        }

        w.WriteEndElement();
        Val(w, "sz", halfPoints);
        Val(w, "szCs", halfPoints);
        Val(w, "lang", o.Language);
        w.WriteEndElement(); // rPr
        w.WriteEndElement(); // rPrDefault

        w.WriteStartElement("w", "pPrDefault", W);
        w.WriteStartElement("w", "pPr", W);
        w.WriteStartElement("w", "spacing", W);
        w.WriteAttributeString("w", "after", W, "120");
        w.WriteAttributeString("w", "line", W, "276");
        w.WriteAttributeString("w", "lineRule", W, "auto");
        w.WriteEndElement();
        w.WriteEndElement(); // pPr
        w.WriteEndElement(); // pPrDefault

        w.WriteEndElement(); // docDefaults

        w.WriteStartElement("w", "style", W);
        w.WriteAttributeString("w", "type", W, "paragraph");
        w.WriteAttributeString("w", "default", W, "1");
        w.WriteAttributeString("w", "styleId", W, "Normal");
        Val(w, "name", "Normal");
        w.WriteEndElement();

        w.WriteEndElement(); // styles
    }

    private static void WriteDocument(XmlWriter w, IReadOnlyList<OcrParagraph> paragraphs)
    {
        w.WriteStartElement("w", "document", W);
        w.WriteStartElement("w", "body", W);

        if (paragraphs.Count == 0)
        {
            w.WriteElementString("w", "p", W, string.Empty);
        }

        foreach (var p in paragraphs)
        {
            w.WriteStartElement("w", "p", W);
            for (int i = 0; i < p.Lines.Count; i++)
            {
                if (i > 0)
                {
                    w.WriteStartElement("w", "r", W);
                    w.WriteElementString("w", "br", W, null);
                    w.WriteEndElement();
                }

                WriteRuns(w, RemoveInvalidXmlChars(p.Lines[i]));
            }

            w.WriteEndElement(); // p
        }

        // A4, kitob holati; maydonlar: yuqori/past 20 mm, chap 30 mm, o'ng 15 mm (1 mm = 56.7 twip).
        w.WriteStartElement("w", "sectPr", W);
        w.WriteStartElement("w", "pgSz", W);
        w.WriteAttributeString("w", "w", W, "11906");
        w.WriteAttributeString("w", "h", W, "16838");
        w.WriteEndElement();
        w.WriteStartElement("w", "pgMar", W);
        w.WriteAttributeString("w", "top", W, "1134");
        w.WriteAttributeString("w", "right", W, "850");
        w.WriteAttributeString("w", "bottom", W, "1134");
        w.WriteAttributeString("w", "left", W, "1701");
        w.WriteAttributeString("w", "header", W, "709");
        w.WriteAttributeString("w", "footer", W, "709");
        w.WriteAttributeString("w", "gutter", W, "0");
        w.WriteEndElement();
        w.WriteEndElement(); // sectPr

        w.WriteEndElement(); // body
        w.WriteEndElement(); // document
    }

    /// <summary>Qatorni Word "run"lariga aylantiradi: tab — <c>w:tab</c>, qolgani — <c>w:t</c> (bo'shliqlar saqlanadi).</summary>
    private static void WriteRuns(XmlWriter w, string line)
    {
        string[] parts = line.Split('\t');
        w.WriteStartElement("w", "r", W);
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                w.WriteElementString("w", "tab", W, null);
            }

            if (parts[i].Length > 0)
            {
                w.WriteStartElement("w", "t", W);
                w.WriteAttributeString("xml", "space", null, "preserve");
                w.WriteString(parts[i]);
                w.WriteEndElement();
            }
        }

        w.WriteEndElement();
    }

    private static void Val(XmlWriter w, string element, string value)
    {
        w.WriteStartElement("w", element, W);
        w.WriteAttributeString("w", "val", W, value);
        w.WriteEndElement();
    }
}
