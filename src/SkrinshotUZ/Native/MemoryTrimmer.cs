namespace SkrinshotUZ.Native;

/// <summary>
/// Tray'da kutib turgan dastur uchun xotirani bo'shatish (talab: &lt;40 MB).
/// Ishga tushish va oynalar uchun bir martalik kerak bo'lgan sahifalar (JIT, oyna resurslari)
/// Windows'ga qaytariladi; kerak bo'lsa, ular xotiradan tez qayta yuklanadi.
/// Faqat jim turgan paytlarda chaqiriladi — skrinshot olish tezligiga ta'sir qilmaydi.
/// </summary>
internal static class MemoryTrimmer
{
    public static void Trim()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
    }
}
