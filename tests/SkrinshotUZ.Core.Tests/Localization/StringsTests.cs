using System.Collections;
using System.Globalization;
using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Core.Tests.Localization;

/// <summary>Interfeys matnlari (o'zbek, lotin) resurs fayldan to'g'ri o'qiladi.</summary>
public class StringsTests
{
    [Fact]
    public void PermissionQuestion_IsExactlyAsInSpecification()
    {
        Assert.Equal(
            "Skrinshot UZ klaviaturadagi PrintScreen tugmasini ishlatishiga ruxsat berasizmi?",
            Strings.Permission_PrintScreen_Question);
        Assert.Equal("Ruxsat berish", Strings.Permission_Allow);
        Assert.Equal("Yo'q", Strings.Permission_Deny);
    }

    [Fact]
    public void AllStrings_AreNonEmpty()
    {
        var set = Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);

        int count = 0;
        foreach (DictionaryEntry e in set)
        {
            count++;
            Assert.False(string.IsNullOrWhiteSpace(e.Value as string), $"'{e.Key}' bo'sh.");
        }

        Assert.True(count >= 8, $"Resurslar soni kutilganidan kam: {count}");
    }

    [Fact]
    public void TrayTooltip_FitsWindowsLimitOf63Chars()
    {
        // NotifyIcon.Text uzunligi cheklangan — oshsa, dastur ishga tushishda xato beradi.
        Assert.InRange(Strings.Tray_Tooltip.Length, 1, 63);
    }

    [Fact]
    public void CrashMessage_HasPlaceholderForLogPath()
    {
        string text = string.Format(CultureInfo.InvariantCulture, Strings.Crash_Message, @"C:\log.txt");

        Assert.Contains(@"C:\log.txt", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownUiCulture_FallsBackToUzbekLatin()
    {
        var saved = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("Chiqish", Strings.Menu_Exit);
        }
        finally
        {
            CultureInfo.CurrentUICulture = saved;
        }
    }
}
