using SkrinshotUZ.Core.Startup;

namespace SkrinshotUZ.Core.Tests.Startup;

public class StartupOptionsTests
{
    [Fact]
    public void NoArguments_IsDefault()
    {
        Assert.Equal(StartupOptions.Default, StartupOptions.Parse(Array.Empty<string>()));
    }

    [Fact]
    public void SmokeTestWithReport_IsParsed()
    {
        var o = StartupOptions.Parse(new[] { "--smoke-test", "--smoke-report", @"C:\temp\smoke.txt" });

        Assert.True(o.SmokeTest);
        Assert.Equal(@"C:\temp\smoke.txt", o.SmokeReportPath);
        Assert.False(o.Autostart);
    }

    [Fact]
    public void Autostart_IsParsed_CaseInsensitive()
    {
        Assert.True(StartupOptions.Parse(new[] { "--AUTOSTART" }).Autostart);
    }

    [Fact]
    public void UnknownArguments_AreIgnored()
    {
        Assert.Equal(StartupOptions.Default, StartupOptions.Parse(new[] { "/foo", "bar", "--nimadir" }));
    }

    [Fact]
    public void SmokeReportWithoutPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => StartupOptions.Parse(new[] { "--smoke-report" }));
        Assert.Throws<ArgumentException>(() => StartupOptions.Parse(new[] { "--smoke-report", "--smoke-test" }));
        Assert.Throws<ArgumentException>(() => StartupOptions.Parse(new[] { "--smoke-report", "  " }));
    }
}
