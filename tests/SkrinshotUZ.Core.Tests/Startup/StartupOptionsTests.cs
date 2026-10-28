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

    [Fact]
    public void InstallerFlags_AreParsed_CaseInsensitive()
    {
        var o = StartupOptions.Parse(new[] { "--UNINSTALL", "--quiet" });

        Assert.True(o.Uninstall);
        Assert.True(o.Quiet);
        Assert.False(o.Exit);
        Assert.False(o.SmokeTest);

        var e = StartupOptions.Parse(new[] { "--exit" });
        Assert.True(e.Exit);
        Assert.False(e.Uninstall);
        Assert.False(e.Quiet);
    }

    [Fact]
    public void Default_HasNoInstallerFlags()
    {
        Assert.False(StartupOptions.Default.Uninstall);
        Assert.False(StartupOptions.Default.Exit);
        Assert.False(StartupOptions.Default.Quiet);
        Assert.False(StartupOptions.Parse(new[] { "--autostart" }).Uninstall);
    }
}
