using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Ocr;

namespace SkrinshotUZ.Ocr;

/// <summary>
/// Tesseract 5 (C API) bilan ishlash. Kutubxonalar to'liq yo'l bilan, bog'liqliklari tartibida yuklanadi
/// (tizimdagi boshqa nusxalar yoki PATH'dagi DLL'lar aralashmaydi). Har bir tanish uchun yangi dvigatel yaratiladi va
/// darhol yo'q qilinadi — dastur fonda ko'p xotira egallab turmaydi.
/// </summary>
internal sealed class TesseractOcr
{
    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

    private static readonly object LoadGate = new();
    private static readonly object CwdGate = new();
    private static IntPtr _engine;

    private readonly string _directory;
    private readonly TessVersionFn _version;
    private readonly TessBaseAPICreateFn _create;
    private readonly TessBaseAPIInit3Fn _init3;
    private readonly TessBaseAPISetVariableFn _setVariable;
    private readonly TessBaseAPISetImageFn _setImage;
    private readonly TessBaseAPISetSourceResolutionFn _setSourceResolution;
    private readonly TessBaseAPIGetUTF8TextFn _getText;
    private readonly TessDeleteTextFn _deleteText;
    private readonly TessBaseAPIEndFn _end;
    private readonly TessBaseAPIDeleteFn _delete;

    private TesseractOcr(string directory, IntPtr engine)
    {
        _directory = directory;
        _version = Export<TessVersionFn>(engine, "TessVersion");
        _create = Export<TessBaseAPICreateFn>(engine, "TessBaseAPICreate");
        _init3 = Export<TessBaseAPIInit3Fn>(engine, "TessBaseAPIInit3");
        _setVariable = Export<TessBaseAPISetVariableFn>(engine, "TessBaseAPISetVariable");
        _setImage = Export<TessBaseAPISetImageFn>(engine, "TessBaseAPISetImage");
        _setSourceResolution = Export<TessBaseAPISetSourceResolutionFn>(engine, "TessBaseAPISetSourceResolution");
        _getText = Export<TessBaseAPIGetUTF8TextFn>(engine, "TessBaseAPIGetUTF8Text");
        _deleteText = Export<TessDeleteTextFn>(engine, "TessDeleteText");
        _end = Export<TessBaseAPIEndFn>(engine, "TessBaseAPIEnd");
        _delete = Export<TessBaseAPIDeleteFn>(engine, "TessBaseAPIDelete");
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr TessVersionFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr TessBaseAPICreateFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TessBaseAPIInit3Fn(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string datapath, [MarshalAs(UnmanagedType.LPUTF8Str)] string language);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TessBaseAPISetVariableFn(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TessBaseAPISetImageFn(IntPtr handle, IntPtr data, int width, int height, int bytesPerPixel, int bytesPerLine);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TessBaseAPISetSourceResolutionFn(IntPtr handle, int ppi);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr TessBaseAPIGetUTF8TextFn(IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TessDeleteTextFn(IntPtr text);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TessBaseAPIEndFn(IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TessBaseAPIDeleteFn(IntPtr handle);

    /// <summary>Tesseract versiyasi (masalan, "5.0.1").</summary>
    public string Version => Marshal.PtrToStringUTF8(_version()) ?? "?";

    /// <summary>
    /// Chiqarilgan papkadagi kutubxonalarni yuklaydi (DLL'lar jarayon davomida bir marta yuklanadi). <c>bin\</c> — DLL'lar,
    /// <c>tessdata\</c> — til modellari (har bir tanishda shu papkadan o'qiladi).
    /// </summary>
    /// <exception cref="OcrUnavailableException">Kutubxona yuklanmadi (buzilgan, boshqa arxitektura va h.k.).</exception>
    public static TesseractOcr Load(string directory)
    {
        lock (LoadGate)
        {
            if (_engine != IntPtr.Zero)
            {
                return Create(directory, _engine);
            }

            string bin = Path.Combine(directory, OcrFiles.BinFolder);
            var names = Directory.EnumerateFiles(bin, "*.dll").Select(f => OcrFiles.BinFolder + "/" + Path.GetFileName(f)).ToList();
            string? engineName = OcrFiles.EngineLibrary(names);
            if (engineName is null)
            {
                throw new OcrUnavailableException("Tesseract kutubxonasi topilmadi: " + bin);
            }

            IntPtr engine = IntPtr.Zero;
            foreach (string rel in OcrFiles.NativeLoadOrder(names))
            {
                string path = Path.Combine(directory, OcrFiles.ToLocal(rel));
                IntPtr h = LoadLibraryExW(path, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
                if (h == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new OcrUnavailableException($"{Path.GetFileName(path)} yuklanmadi: {new Win32Exception(error).Message} ({error})");
                }

                if (string.Equals(rel, engineName, StringComparison.OrdinalIgnoreCase))
                {
                    engine = h;
                }
            }

            var ocr = Create(directory, engine);
            _engine = engine;
            return ocr;
        }
    }

    /// <summary>Kulrang rasmdagi matnni aniqlaydi.</summary>
    /// <param name="gray">Piksellar (1 bayt/piksel).</param>
    /// <param name="width">Kenglik.</param>
    /// <param name="height">Balandlik.</param>
    /// <param name="ppi">Rasm DPI'si (Tesseract harf o'lchamini baholash uchun).</param>
    /// <returns>UTF-8 dan o'girilgan matn.</returns>
    public string Recognize(byte[] gray, int width, int height, int ppi)
    {
        ArgumentNullException.ThrowIfNull(gray);
        if (gray.Length != width * height)
        {
            throw new ArgumentException("Rasm o'lchami noto'g'ri.", nameof(gray));
        }

        IntPtr api = _create();
        if (api == IntPtr.Zero)
        {
            throw new OcrUnavailableException("Tesseract dvigateli yaratilmadi.");
        }

        var pin = GCHandle.Alloc(gray, GCHandleType.Pinned);
        try
        {
            Init(api);
            _setVariable(api, "preserve_interword_spaces", "1"); // jadval ustunlari orasidagi bo'shliqlar saqlansin
            _setImage(api, pin.AddrOfPinnedObject(), width, height, 1, width);
            _setSourceResolution(api, ppi);
            IntPtr text = _getText(api);
            if (text == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUTF8(text) ?? string.Empty;
            }
            finally
            {
                _deleteText(text);
            }
        }
        finally
        {
            pin.Free();
            _end(api);
            _delete(api);
        }
    }

    /// <summary>
    /// Bitmap'dagi matnni aniqlaydi: kulrangga o'tkazish → kattalashtirish (<see cref="OcrImagePlan"/>) → Tesseract.
    /// </summary>
    /// <param name="image">Rasm (chaqiruvchi egalik qiladi; boshqa oqimda bir vaqtda ishlatilmasin).</param>
    /// <param name="dpi">Rasm olingan monitor DPI'si.</param>
    /// <returns>Matn.</returns>
    public string Recognize(Bitmap image, double dpi)
    {
        ArgumentNullException.ThrowIfNull(image);
        int w = image.Width, h = image.Height;
        byte[] gray;
        var data = image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = Math.Abs(data.Stride);
            var buffer = new byte[stride * h];
            if (data.Stride > 0)
            {
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            }
            else
            {
                // Pastdan yuqoriga saqlangan rasm (manfiy stride) — qatorlar bittalab.
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(data.Scan0 + (y * data.Stride), buffer, y * stride, stride);
                }
            }

            gray = GrayImage.FromBgra(buffer, stride, w, h);
        }
        finally
        {
            image.UnlockBits(data);
        }

        int scale = OcrImagePlan.ChooseScale(w, h, dpi);
        byte[] scaled = GrayImage.Upscale(gray, w, h, scale);
        return Recognize(scaled, w * scale, h * scale, OcrImagePlan.SourceResolution(dpi, scale));
    }

    private static TesseractOcr Create(string directory, IntPtr engine)
    {
        try
        {
            return new TesseractOcr(directory, engine);
        }
        catch (EntryPointNotFoundException ex)
        {
            throw new OcrUnavailableException("Tesseract kutubxonasi mos emas: " + ex.Message, ex);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

    private static T Export<T>(IntPtr library, string name)
        where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    /// <summary>
    /// Til modellarini yuklash. Tesseract fayl yo'llarini ANSI kodlashda ochadi — foydalanuvchi nomi kirill yoki
    /// o'zbekcha harflarda bo'lsa (C:\Users\Ўқитувчи) to'liq yo'l buziladi. Shuning uchun joriy papka vaqtincha
    /// OCR papkasiga o'tkaziladi va Tesseract'ga faqat lotin harfli nisbiy yo'l beriladi.
    /// </summary>
    private void Init(IntPtr api)
    {
        int rc;
        lock (CwdGate)
        {
            string previous = Environment.CurrentDirectory;
            Environment.CurrentDirectory = _directory;
            try
            {
                rc = _init3(api, OcrFiles.TessdataFolder + "/", OcrLanguages.All);
            }
            finally
            {
                Environment.CurrentDirectory = previous;
            }
        }

        if (rc != 0)
        {
            throw new OcrUnavailableException("Til modellari yuklanmadi (" + OcrLanguages.All + ").");
        }
    }
}
