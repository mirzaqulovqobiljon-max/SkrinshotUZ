namespace SkrinshotUZ.Core.Startup;

/// <summary>Buyruq qatori parametrlari.</summary>
/// <param name="SmokeTest">
/// <c>--smoke-test</c>: CI uchun tekshiruv rejimi — dastur ishga tushadi, 3 soniyadan keyin
/// xotira hisobotini yozib, o'zi yopiladi. Hech qanday oyna yoki savol chiqarmaydi.
/// </param>
/// <param name="SmokeReportPath"><c>--smoke-report &lt;fayl&gt;</c>: hisobot fayli yo'li.</param>
/// <param name="Autostart"><c>--autostart</c>: Windows bilan birga ishga tushirilgan (xush kelibsiz xabari chiqmaydi).</param>
public sealed record StartupOptions(bool SmokeTest, string? SmokeReportPath, bool Autostart)
{
    /// <summary>Parametrsiz oddiy ishga tushirish.</summary>
    public static StartupOptions Default { get; } = new(false, null, false);

    /// <summary>Buyruq qatorini tahlil qiladi. Noma'lum parametrlar e'tiborsiz qoldiriladi.</summary>
    /// <param name="args">Parametrlar.</param>
    /// <returns>Natija.</returns>
    /// <exception cref="ArgumentException"><c>--smoke-report</c> dan keyin yo'l berilmagan.</exception>
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        bool smoke = false;
        bool autostart = false;
        string? report = null;

        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            if (string.Equals(a, "--smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                smoke = true;
            }
            else if (string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase))
            {
                autostart = true;
            }
            else if (string.Equals(a, "--smoke-report", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("--smoke-report parametridan keyin fayl yo'li bo'lishi kerak.", nameof(args));
                }

                report = args[++i];
            }
        }

        return new StartupOptions(smoke, report, autostart);
    }
}
