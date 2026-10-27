namespace SkrinshotUZ.Core.Platform;

/// <summary>
/// Joriy foydalanuvchi reyestri (HKEY_CURRENT_USER) bilan ishlash. Faqat HKCU — administrator huquqi kerak emas
/// va boshqa foydalanuvchilarga ta'sir qilmaydi. Testlarda xotiradagi soxta amalga oshirish ishlatiladi.
/// </summary>
public interface IRegistryStore
{
    /// <summary>Qiymatni o'qiydi.</summary>
    /// <param name="subKey">Kalit, masalan <c>Control Panel\Keyboard</c>.</param>
    /// <param name="name">Qiymat nomi.</param>
    /// <returns>Qiymat yoki <c>null</c> (yo'q bo'lsa).</returns>
    object? GetValue(string subKey, string name);

    /// <summary>DWORD qiymat yozadi (kalit yo'q bo'lsa yaratiladi).</summary>
    /// <param name="subKey">Kalit.</param>
    /// <param name="name">Nom.</param>
    /// <param name="value">Qiymat.</param>
    void SetDword(string subKey, string name, int value);

    /// <summary>Matn qiymat yozadi (kalit yo'q bo'lsa yaratiladi).</summary>
    /// <param name="subKey">Kalit.</param>
    /// <param name="name">Nom.</param>
    /// <param name="value">Qiymat.</param>
    void SetString(string subKey, string name, string value);

    /// <summary>Qiymatni o'chiradi (yo'q bo'lsa — hech narsa qilmaydi).</summary>
    /// <param name="subKey">Kalit.</param>
    /// <param name="name">Nom.</param>
    void DeleteValue(string subKey, string name);
}
