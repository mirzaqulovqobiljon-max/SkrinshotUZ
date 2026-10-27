using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Tests.Fakes;

namespace SkrinshotUZ.Core.Tests.Platform;

public class SnippingToolSettingTests
{
    private readonly InMemoryRegistry _registry = new();

    private void Set(object value) => _registry.Values[(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName)] = value;

    [Fact]
    public void KeyAndValueName_MatchWindows()
    {
        Assert.Equal(@"Control Panel\Keyboard", SnippingToolSetting.SubKey);
        Assert.Equal("PrintScreenKeyForSnippingEnabled", SnippingToolSetting.ValueName);
    }

    [Fact]
    public void ExplicitOne_IsEnabled_OnAnyWindows()
    {
        Set(1);
        Assert.True(new SnippingToolSetting(_registry, 19045).IsEnabled());
        Assert.True(new SnippingToolSetting(_registry, 22631).IsEnabled());
    }

    [Fact]
    public void ExplicitZero_IsDisabled_OnAnyWindows()
    {
        Set(0);
        Assert.False(new SnippingToolSetting(_registry, 19045).IsEnabled());
        Assert.False(new SnippingToolSetting(_registry, 22631).IsEnabled());
    }

    [Theory]
    [InlineData(19045, false)] // Windows 10 22H2
    [InlineData(21999, false)]
    [InlineData(22000, true)]  // Windows 11 21H2
    [InlineData(26100, true)]  // Windows 11 24H2
    public void Missing_DependsOnWindowsVersion(int build, bool expected)
    {
        Assert.Equal(expected, new SnippingToolSetting(_registry, build).IsEnabled());
    }

    [Fact]
    public void UnexpectedType_TreatedAsDisabled()
    {
        Set("1");
        Assert.False(new SnippingToolSetting(_registry, 22631).IsEnabled());
    }

    [Fact]
    public void DisableAndEnable_WriteDword()
    {
        var s = new SnippingToolSetting(_registry, 22631);

        s.Disable();
        Assert.Equal(0, _registry.GetValue(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName));
        Assert.False(s.IsEnabled());

        s.Enable();
        Assert.Equal(1, _registry.GetValue(SnippingToolSetting.SubKey, SnippingToolSetting.ValueName));
        Assert.True(s.IsEnabled());
    }
}

public class AutostartServiceTests
{
    private const string Exe = @"C:\Users\O'qituvchi\AppData\Local\Programs\Skrinshot UZ\SkrinshotUZ.exe";
    private readonly InMemoryRegistry _registry = new();

    [Fact]
    public void RunKeyLocation_IsCurrentUserRun()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", AutostartService.RunSubKey);
    }

    [Fact]
    public void CommandLine_QuotesPathWithSpaces_AndAddsAutostartFlag()
    {
        Assert.Equal("\"" + Exe + "\" --autostart", new AutostartService(_registry, Exe).CommandLine);
    }

    [Fact]
    public void Enable_ThenIsEnabled_ThenDisable()
    {
        var a = new AutostartService(_registry, Exe);
        Assert.False(a.IsEnabled());

        a.Enable();
        Assert.True(a.IsEnabled());

        a.Disable();
        Assert.False(a.IsEnabled());
        a.Disable(); // yo'q qiymatni o'chirish — xatosiz
    }

    [Fact]
    public void EntryForAnotherPath_IsNotConsideredEnabled()
    {
        _registry.Values[(AutostartService.RunSubKey, AutostartService.ValueName)] = "\"D:\\eski\\SkrinshotUZ.exe\" --autostart";

        Assert.False(new AutostartService(_registry, Exe).IsEnabled());
    }

    [Fact]
    public void PathComparison_IsCaseInsensitive_LikeWindows()
    {
        _registry.Values[(AutostartService.RunSubKey, AutostartService.ValueName)] = ("\"" + Exe + "\" --AUTOSTART ").ToUpperInvariant();

        Assert.True(new AutostartService(_registry, Exe).IsEnabled());
    }
}
