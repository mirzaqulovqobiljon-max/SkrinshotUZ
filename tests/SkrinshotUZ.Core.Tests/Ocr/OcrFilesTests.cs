using System.Text;
using SkrinshotUZ.Core.Ocr;

namespace SkrinshotUZ.Core.Tests.Ocr;

/// <summary>.exe ichidagi OCR fayllarini bir marta, xavfsiz chiqarish.</summary>
public sealed class OcrFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "suz-ocr-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Missing_FullBundle_Nothing()
    {
        Assert.Empty(OcrFiles.Missing(Bundle()));
    }

    [Fact]
    public void Missing_ReportsEngineLeptonicaAndEachLanguage()
    {
        var files = Bundle().Where(f => !f.RelativePath.Contains("tesseract", StringComparison.Ordinal)
            && !f.RelativePath.Contains("lepton", StringComparison.Ordinal)
            && f.RelativePath != "tessdata/rus.traineddata").ToList();

        var missing = OcrFiles.Missing(files);

        Assert.Contains("bin/tesseract*.dll", missing);
        Assert.Contains("bin/leptonica*.dll", missing);
        Assert.Contains("tessdata/rus.traineddata", missing);
        Assert.Equal(3, missing.Count);
        Assert.Equal(6, OcrFiles.Missing(Array.Empty<OcrFile>()).Count);
    }

    [Fact]
    public void EngineLibrary_FoundOnlyInBinFolder()
    {
        Assert.Equal("bin/tesseract50.dll", OcrFiles.EngineLibrary(new[] { "tessdata/tesseract.dll", "bin/leptonica-1.82.0.dll", "bin/tesseract50.dll" }));
        Assert.Null(OcrFiles.EngineLibrary(new[] { "tessdata/tesseract50.dll", "bin/sub/tesseract50.dll", "bin/tesseract50.txt" }));
    }

    [Fact]
    public void NativeLoadOrder_RuntimeFirst_ThenLeptonica_ThenTesseract()
    {
        var order = OcrFiles.NativeLoadOrder(new[]
        {
            "bin/tesseract50.dll", "bin/leptonica-1.82.0.dll", "bin/msvcp140.dll", "bin/vcruntime140_1.dll",
            "bin/vcruntime140.dll", "bin/vcomp140.dll", "tessdata/eng.traineddata", "bin/readme.txt",
        });

        Assert.Equal(new[]
        {
            "bin/vcruntime140.dll", "bin/vcruntime140_1.dll", "bin/msvcp140.dll", "bin/vcomp140.dll",
            "bin/leptonica-1.82.0.dll", "bin/tesseract50.dll",
        }, order);
    }

    [Fact]
    public void EnsureExtracted_WritesAllFilesAndMarker()
    {
        var files = Bundle();

        string dir = OcrFiles.EnsureExtracted(_root, files, "1.2.0");

        Assert.Equal(Path.Combine(_root, OcrFiles.BundleId(files, "1.2.0")), dir);
        foreach (var f in files)
        {
            string path = Path.Combine(dir, OcrFiles.ToLocal(f.RelativePath));
            Assert.Equal(Content(f.RelativePath), File.ReadAllText(path));
        }

        Assert.True(OcrFiles.IsComplete(dir, files));
        Assert.Single(Directory.GetDirectories(_root)); // vaqtinchalik papka qolmadi
    }

    [Fact]
    public void EnsureExtracted_SecondTime_DoesNotRewrite()
    {
        int opens = 0;
        var files = Bundle(() => opens++);

        OcrFiles.EnsureExtracted(_root, files, "1.2.0");
        int afterFirst = opens;
        OcrFiles.EnsureExtracted(_root, files, "1.2.0");

        Assert.Equal(files.Count, afterFirst);
        Assert.Equal(afterFirst, opens);
    }

    [Fact]
    public void EnsureExtracted_DamagedFolder_IsRepaired()
    {
        var files = Bundle();
        string dir = OcrFiles.EnsureExtracted(_root, files, "1.2.0");
        File.WriteAllText(Path.Combine(dir, "tessdata", "uzb.traineddata"), "buzilgan");   // hajm boshqacha

        Assert.False(OcrFiles.IsComplete(dir, files));
        string again = OcrFiles.EnsureExtracted(_root, files, "1.2.0");

        Assert.Equal(dir, again);
        Assert.Equal(Content("tessdata/uzb.traineddata"), File.ReadAllText(Path.Combine(dir, "tessdata", "uzb.traineddata")));
    }

    [Fact]
    public void EnsureExtracted_FolderWithoutMarker_NotTrusted()
    {
        var files = Bundle();
        string dir = OcrFiles.EnsureExtracted(_root, files, "1.2.0");
        File.Delete(Path.Combine(dir, OcrFiles.ReadyMarker));   // chiqarish yarimda uzilgan holat

        Assert.False(OcrFiles.IsComplete(dir, files));
        OcrFiles.EnsureExtracted(_root, files, "1.2.0");
        Assert.True(OcrFiles.IsComplete(dir, files));
    }

    [Fact]
    public void NewVersion_GetsNewFolder_OldOneRemoved()
    {
        var files = Bundle();
        string v1 = OcrFiles.EnsureExtracted(_root, files, "1.2.0");
        string v2 = OcrFiles.EnsureExtracted(_root, files, "1.3.0");

        Assert.NotEqual(v1, v2);
        Assert.False(Directory.Exists(v1));
        Assert.True(Directory.Exists(v2));
    }

    [Fact]
    public void LengthMismatch_Throws_AndLeavesNothingBehind()
    {
        var files = Bundle().Select(f => f.RelativePath == "tessdata/eng.traineddata" ? f with { Length = f.Length + 5 } : f).ToList();

        var ex = Assert.Throws<OcrUnavailableException>(() => OcrFiles.EnsureExtracted(_root, files, "1.2.0"));

        Assert.Contains("eng.traineddata", ex.Message);
        Assert.Empty(Directory.Exists(_root) ? Directory.GetDirectories(_root) : Array.Empty<string>());
    }

    [Fact]
    public void IncompleteBundle_Throws_WithMissingList()
    {
        var files = Bundle().Where(f => f.RelativePath != "tessdata/uzb_cyrl.traineddata").ToList();

        var ex = Assert.Throws<OcrUnavailableException>(() => OcrFiles.EnsureExtracted(_root, files, "1.2.0"));

        Assert.Contains("uzb_cyrl.traineddata", ex.Message);
        Assert.False(Directory.Exists(_root) && Directory.EnumerateFileSystemEntries(_root).Any());
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("bin/../../evil.dll")]
    [InlineData("/abs/evil.dll")]
    [InlineData("bin//x.dll")]
    [InlineData("bin/ev il.dll")]
    public void UnsafeRelativePath_Rejected(string bad)
    {
        var files = Bundle().Append(Make(bad)).ToList();

        Assert.Throws<OcrUnavailableException>(() => OcrFiles.EnsureExtracted(_root, files, "1.2.0"));
        Assert.False(System.IO.File.Exists(Path.Combine(Path.GetTempPath(), "evil.dll")));
    }

    [Fact]
    public void BundleId_StableAndOrderIndependent_ChangesWithContentOrVersion()
    {
        var a = Bundle();
        var reversed = a.AsEnumerable().Reverse().ToList();

        Assert.Equal(OcrFiles.BundleId(a, "1.2.0"), OcrFiles.BundleId(reversed, "1.2.0"));
        Assert.Equal(12, OcrFiles.BundleId(a, "1.2.0").Length);
        Assert.NotEqual(OcrFiles.BundleId(a, "1.2.0"), OcrFiles.BundleId(a, "1.2.1"));
        var bigger = a.Select(f => f.RelativePath == "bin/tesseract50.dll" ? f with { Length = 999 } : f).ToList();
        Assert.NotEqual(OcrFiles.BundleId(a, "1.2.0"), OcrFiles.BundleId(bigger, "1.2.0"));
    }

    private static string Content(string path) => "mazmun:" + path;

    private static OcrFile Make(string path, Action? onOpen = null)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Content(path));
        return new OcrFile(path, bytes.Length, () =>
        {
            onOpen?.Invoke();
            return new MemoryStream(bytes);
        });
    }

    private static List<OcrFile> Bundle(Action? onOpen = null) => new[]
    {
        "bin/vcruntime140.dll", "bin/leptonica-1.82.0.dll", "bin/tesseract50.dll",
        "tessdata/uzb.traineddata", "tessdata/uzb_cyrl.traineddata", "tessdata/rus.traineddata", "tessdata/eng.traineddata",
    }.Select(p => Make(p, onOpen)).ToList();
}
