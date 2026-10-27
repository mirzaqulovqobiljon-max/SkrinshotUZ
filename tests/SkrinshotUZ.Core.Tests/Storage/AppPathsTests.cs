using SkrinshotUZ.Core.Storage;

namespace SkrinshotUZ.Core.Tests.Storage;

public class AppPathsTests
{
    [Fact]
    public void DefaultScreenshotsFolder_IsInsidePictures()
    {
        Assert.Equal(Path.Combine(@"C:\Users\Ustoz\Pictures", "Skrinshot UZ"), AppPaths.DefaultScreenshotsFolder(@"C:\Users\Ustoz\Pictures"));
    }

    [Fact]
    public void LogsFolder_IsInsideLocalAppData()
    {
        Assert.Equal(Path.Combine(@"C:\Users\Ustoz\AppData\Local", "Skrinshot UZ", "logs"), AppPaths.LogsFolder(@"C:\Users\Ustoz\AppData\Local"));
    }

    [Fact]
    public void CrashLogFileName_UsesInvariantTimestamp()
    {
        Assert.Equal("xato_2026-10-27_20-38-15.log", AppPaths.CrashLogFileName(new DateTime(2026, 10, 27, 20, 38, 15)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyBaseFolder_Throws(string folder)
    {
        Assert.Throws<ArgumentException>(() => AppPaths.LogsFolder(folder));
        Assert.Throws<ArgumentException>(() => AppPaths.DefaultScreenshotsFolder(folder));
    }
}
