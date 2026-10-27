namespace SkrinshotUZ;

/// <summary>Dastur ikonkasini resursdan yuklash.</summary>
internal static class AppIcon
{
    private const string ResourceName = "SkrinshotUZ.app.ico";

    /// <summary>
    /// Tray uchun kichik ikonka — joriy DPI'ga mos o'lchamda (100% da 16 px, 150% da 24 px va h.k.),
    /// shuning uchun ikonka xira ko'rinmaydi.
    /// </summary>
    /// <returns>Yangi <see cref="Icon"/> (chaqiruvchi dispose qiladi).</returns>
    public static Icon LoadSmall()
    {
        using Stream? stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            // Bo'lmasligi kerak, lekin dastur ishga tushmay qolmasin.
            return (Icon)SystemIcons.Application.Clone();
        }

        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
