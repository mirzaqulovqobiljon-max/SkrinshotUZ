using System.Globalization;
using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.Core.Printing;

/// <summary>Chop etish oynasidagi matnlar (o'lchamlar o'zbekcha: o'nlik vergul bilan — "190,0 × 106,9 mm").</summary>
public static class PrintText
{
    private static readonly NumberFormatInfo Uzbek = new() { NumberDecimalSeparator = ",", NegativeSign = "-" };

    /// <summary>Millimetr: bitta o'nlik xona bilan.</summary>
    /// <param name="mm">Qiymat.</param>
    /// <returns>"106,9".</returns>
    public static string Mm(double mm) => mm.ToString("0.0", Uzbek);

    /// <summary>O'lcham: "190,0 × 106,9 mm".</summary>
    /// <param name="size">O'lcham.</param>
    /// <returns>Matn.</returns>
    public static string Size(SizeMm size) => Mm(size.Width) + " × " + Mm(size.Height) + " mm";

    /// <summary>Rasmning qog'ozdagi o'lchami va masshtabi.</summary>
    /// <param name="plan">Reja.</param>
    /// <returns>"Rasm qog'ozda: 190,0 × 106,9 mm · asl o'lchamning 75%".</returns>
    public static string SizeInfo(PrintPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        long percent = (long)Math.Round(plan.ScalePercent, MidpointRounding.AwayFromZero);
        return percent == 100
            ? string.Format(CultureInfo.InvariantCulture, Strings.Print_SizeInfoActual, Size(plan.ImageSizeMm))
            : string.Format(CultureInfo.InvariantCulture, Strings.Print_SizeInfo, Size(plan.ImageSizeMm), percent);
    }

    /// <summary>Varaqlar haqida: "A4 albom · 2 varaq × 3 nusxa".</summary>
    /// <param name="plan">Reja.</param>
    /// <param name="copies">Nusxalar soni.</param>
    /// <returns>Matn.</returns>
    public static string PagesInfo(PrintPlan plan, int copies)
    {
        ArgumentNullException.ThrowIfNull(plan);
        string orientation = plan.Pages[0].Orientation == PageOrientation.Landscape ? Strings.Print_Landscape : Strings.Print_Portrait;
        string text = string.Format(CultureInfo.InvariantCulture, Strings.Print_PagesInfo, orientation, plan.Pages.Count);
        return copies > 1 ? text + string.Format(CultureInfo.InvariantCulture, Strings.Print_CopiesSuffix, copies) : text;
    }
}
