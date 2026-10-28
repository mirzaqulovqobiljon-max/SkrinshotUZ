using System.Security;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Platform;
using SkrinshotUZ.Core.Settings;

namespace SkrinshotUZ.Core.Permissions;

/// <summary>Ruxsat oynasidagi tanlov.</summary>
public enum PermissionChoice
{
    /// <summary>"Ruxsat berish".</summary>
    Allow,

    /// <summary>"Yo'q".</summary>
    Deny,

    /// <summary>Oyna javobsiz yopildi (X yoki Esc) — hech narsa saqlanmaydi, keyingi safar yana so'raladi.</summary>
    Dismissed,
}

/// <summary>PrintScreen ruxsat oynasining javobi.</summary>
/// <param name="Choice">Tanlov.</param>
/// <param name="EnableAutostart">"Kompyuter yoqilganda avtomatik ishga tushsin" belgilanganmi.</param>
public readonly record struct PrintScreenAnswer(PermissionChoice Choice, bool EnableAutostart);

/// <summary>Foydalanuvchiga savol beruvchi oynalar (Windows'da TaskDialog; testlarda soxta).</summary>
public interface IUserPrompts
{
    /// <summary>"Skrinshot UZ klaviaturadagi PrintScreen tugmasini ishlatishiga ruxsat berasizmi?"</summary>
    /// <param name="offerAutostart">"Avtomatik ishga tushirish" belgisini ko'rsatish (standart — belgilanmagan).</param>
    /// <returns>Javob.</returns>
    PrintScreenAnswer AskPrintScreenPermission(bool offerAutostart);

    /// <summary>Qirqish vositasi PrintScreen'ni egallaganini tushuntiradi va o'chirishga ruxsat so'raydi.</summary>
    /// <returns>Foydalanuvchi aniq "Ha, o'chirish" ni tanlasa <c>true</c>.</returns>
    bool AskDisableSnippingTool();

    /// <summary>Ilgari o'chirilgan Qirqish vositasi sozlamasini qayta yoqishni so'raydi.</summary>
    /// <returns>Aniq "Ha" bo'lsa <c>true</c>.</returns>
    bool AskRestoreSnippingTool();

    /// <summary>Sozlama o'chirilgani haqida xabar.</summary>
    void ShowSnippingToolDisabled();

    /// <summary>Ogohlantirish.</summary>
    /// <param name="message">Matn.</param>
    void ShowWarning(string message);
}

/// <summary>
/// Ruxsatlar mantiqi. Asosiy qoida: tizim sozlamalari (Qirqish vositasi, avtoishga tushirish)
/// FAQAT foydalanuvchining aniq roziligidan keyin o'zgartiriladi; javobsiz yopilgan oyna — rozilik emas.
/// </summary>
public sealed class PermissionFlow
{
    private readonly ISettingsStore _store;
    private readonly IUserPrompts _prompts;
    private readonly ISnippingToolSetting _snipping;
    private readonly IAutostartService _autostart;
    private bool _saveWarningShown;

    /// <summary>Yangi oqim (sozlamalar shu yerda o'qiladi).</summary>
    /// <param name="store">Sozlamalar ombori.</param>
    /// <param name="prompts">Oynalar.</param>
    /// <param name="snipping">Qirqish vositasi sozlamasi.</param>
    /// <param name="autostart">Avtoishga tushirish.</param>
    public PermissionFlow(ISettingsStore store, IUserPrompts prompts, ISnippingToolSetting snipping, IAutostartService autostart)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _snipping = snipping ?? throw new ArgumentNullException(nameof(snipping));
        _autostart = autostart ?? throw new ArgumentNullException(nameof(autostart));
        Settings = _store.Load();
    }

    /// <summary>Joriy sozlamalar.</summary>
    public AppSettings Settings { get; }

    /// <summary>PrintScreen tugmasiga ruxsat berilganmi.</summary>
    public bool PrintScreenAllowed => Settings.PrintScreenConsent == ConsentState.Allowed;

    /// <summary>Sozlamalarni saqlaydi (masalan, saqlash papkasi o'zgarganda). Xato bo'lsa — bir marta ogohlantiradi.</summary>
    public void SaveSettings() => Save();

    /// <summary>
    /// Ishga tushishda: hali so'ralmagan bo'lsa — PrintScreen ruxsatini so'raydi;
    /// ruxsat berilgan va Qirqish vositasi PrintScreen'ni egallagan bo'lsa — bir marta taklif qiladi.
    /// </summary>
    public void RunAtStartup()
    {
        if (Settings.PrintScreenConsent == ConsentState.NotAsked)
        {
            AskPrintScreen(offerAutostart: true);
        }

        if (PrintScreenAllowed && !Settings.SnippingToolPromptAnswered)
        {
            OfferSnippingToolFix();
        }
    }

    /// <summary>Menyudan "PrintScreen'dan foydalanish" yoqilmoqchi — ruxsat oynasi qayta ko'rsatiladi.</summary>
    /// <returns>Endi ruxsat berilganmi.</returns>
    public bool RequestPrintScreen()
    {
        AskPrintScreen(offerAutostart: false);
        if (PrintScreenAllowed && !Settings.SnippingToolPromptAnswered)
        {
            OfferSnippingToolFix();
        }

        return PrintScreenAllowed;
    }

    /// <summary>
    /// Menyudan PrintScreen o'chirildi. Agar Qirqish vositasi sozlamasini avval Skrinshot UZ o'chirgan bo'lsa —
    /// uni qayta yoqishni (ruxsat bilan) taklif qiladi.
    /// </summary>
    public void RevokePrintScreen()
    {
        Settings.PrintScreenConsent = ConsentState.Denied;
        if (Settings.SnippingToolDisabledByApp && _prompts.AskRestoreSnippingTool())
        {
            if (TryChangeSystemSetting(_snipping.Enable))
            {
                Settings.SnippingToolDisabledByApp = false;
            }
        }

        Save();
    }

    /// <summary>
    /// Qirqish vositasi PrintScreen'ni egallagan bo'lsa — tushuntirib, o'chirishga ruxsat so'raydi.
    /// "Yo'q" javobi ham eslab qolinadi (har safar bezovta qilinmaydi); menyudan qayta chaqirish mumkin.
    /// </summary>
    public void OfferSnippingToolFix()
    {
        if (!IsSnippingToolEnabled())
        {
            return;
        }

        bool agreed = _prompts.AskDisableSnippingTool();
        Settings.SnippingToolPromptAnswered = true;

        if (agreed && TryChangeSystemSetting(_snipping.Disable))
        {
            Settings.SnippingToolDisabledByApp = true;
            _prompts.ShowSnippingToolDisabled();
        }

        Save();
    }

    /// <summary>Qirqish vositasi PrintScreen'ni egallaganmi (reyestrni o'qib bo'lmasa — <c>false</c>).</summary>
    /// <returns>Egallagan bo'lsa <c>true</c>.</returns>
    public bool IsSnippingToolEnabled()
    {
        try
        {
            return _snipping.IsEnabled();
        }
        catch (Exception ex) when (IsAccessError(ex))
        {
            return false;
        }
    }

    /// <summary>Avtoishga tushirish yoqilganmi.</summary>
    /// <returns>Yoqilgan bo'lsa <c>true</c>.</returns>
    public bool IsAutostartEnabled()
    {
        try
        {
            return _autostart.IsEnabled();
        }
        catch (Exception ex) when (IsAccessError(ex))
        {
            return false;
        }
    }

    /// <summary>Menyudagi "Windows bilan birga ishga tushirish" belgisi (foydalanuvchining o'zi bosgan — bu rozilik).</summary>
    /// <param name="enabled">Yoqish yoki o'chirish.</param>
    /// <returns>Muvaffaqiyatli bo'lsa <c>true</c>.</returns>
    public bool SetAutostart(bool enabled) =>
        TryChangeSystemSetting(enabled ? _autostart.Enable : _autostart.Disable);

    /// <summary>
    /// Dastur o'chirilayotganda (o'rnatuvchi <c>--uninstall</c> bilan chaqiradi): o'zining avtoishga tushirish yozuvini
    /// olib tashlaydi (aks holda Windows yo'q faylni ishga tushirishga urinadi) va Qirqish vositasi sozlamasini avval
    /// Skrinshot UZ o'chirgan bo'lsa — uni qaytarishni SO'RAYDI. Jim rejimda (<paramref name="quiet"/>) tizim sozlamasi
    /// so'roqsiz o'zgartirilmaydi.
    /// </summary>
    /// <param name="quiet">Jim o'chirish (<c>/VERYSILENT</c>) — hech qanday oyna chiqmaydi.</param>
    /// <returns>Qirqish vositasi qayta yoqildimi.</returns>
    public bool CleanUpForUninstall(bool quiet)
    {
        if (IsAutostartEnabled())
        {
            try
            {
                _autostart.Disable();
            }
            catch (Exception ex) when (IsAccessError(ex))
            {
                // O'zimizning yozuvimiz — o'chirib bo'lmasa ham o'chirishni to'xtatmaymiz.
            }
        }

        bool restored = false;
        if (Settings.SnippingToolDisabledByApp && !quiet && _prompts.AskRestoreSnippingTool())
        {
            try
            {
                _snipping.Enable();
                Settings.SnippingToolDisabledByApp = false;
                restored = true;
            }
            catch (Exception ex) when (IsAccessError(ex))
            {
                _prompts.ShowWarning(Strings.Warning_SystemSettingDenied);
            }
        }

        return restored;
    }

    private void AskPrintScreen(bool offerAutostart)
    {
        var answer = _prompts.AskPrintScreenPermission(offerAutostart);
        switch (answer.Choice)
        {
            case PermissionChoice.Allow:
                Settings.PrintScreenConsent = ConsentState.Allowed;
                break;
            case PermissionChoice.Deny:
                Settings.PrintScreenConsent = ConsentState.Denied;
                break;
            default:
                // Javobsiz yopildi: hech narsa o'zgarmaydi va hech narsa yoqilmaydi.
                return;
        }

        if (offerAutostart && answer.EnableAutostart)
        {
            SetAutostart(true);
        }

        Save();
    }

    private bool TryChangeSystemSetting(Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception ex) when (IsAccessError(ex))
        {
            _prompts.ShowWarning(Strings.Warning_SystemSettingDenied);
            return false;
        }
    }

    private void Save()
    {
        try
        {
            _store.Save(Settings);
        }
        catch (Exception ex) when (IsAccessError(ex))
        {
            if (!_saveWarningShown)
            {
                _saveWarningShown = true;
                _prompts.ShowWarning(Strings.Warning_SettingsNotSaved);
            }
        }
    }

    /// <summary>Maktab kompyuterlarida administrator cheklovlari bo'lishi mumkin — bu xatolar dasturni yiqitmaydi.</summary>
    private static bool IsAccessError(Exception ex) =>
        ex is UnauthorizedAccessException or SecurityException or IOException;
}
