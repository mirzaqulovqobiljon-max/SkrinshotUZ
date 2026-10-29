# Skrinshot UZ

O'zbek maktablari uchun Windows skrinshot dasturi (Lightshot analogi). **PrintScreen** tugmasini bosing, ekranning kerakli qismini tanlang, ustiga chizing va saqlang, nusxa oling yoki chop eting.

- **Skrinshotlar hech qachon internetga yuborilmaydi** (bolalar ma'lumoti xavfsizligi). Barcha ish — kompyuterning o'zida.
- **Interfeys to'liq o'zbek tilida.**
- **Windows 10 va 11**, 64-bit va 32-bit.
- **Kompyuterga .NET o'rnatish shart emas.**
- **Yengil:** fonda 15–25 MB xotira ishlatadi.

---

## O'qituvchilar uchun qo'llanma

### 1. O'rnatish

1. [Releases](../../releases/latest) sahifasidan `SkrinshotUZ_Setup.exe` faylini yuklab oling (to'g'ridan-to'g'ri havola: [SkrinshotUZ_Setup.exe](https://github.com/mirzaqulovqobiljon-max/SkrinshotUZ/releases/latest/download/SkrinshotUZ_Setup.exe)).
2. Faylni ishga tushiring.
   - **"Windows kompyuteringizni himoya qildi"** degan ko'k oyna chiqsa, **"Batafsil" → "Baribir ishga tushirish"** ni bosing. Dastur raqamli imzosiz bo'lgani uchun Windows shunday ogohlantiradi.
3. **Keyingi → O'rnatish → Tayyor.**

O'rnatish uchun **administrator huquqi shart emas**: dastur faqat siz uchun o'rnatiladi. Boshlash menyusida va ish stolida "Skrinshot UZ" yorlig'i paydo bo'ladi.

> **O'rnatmasdan ishlatish ham mumkin.** `SkrinshotUZ-x64.exe` (yoki eski 32-bit kompyuterlar uchun `SkrinshotUZ-x86.exe`) faylini istalgan papkaga yoki fleshkaga qo'yib, to'g'ridan-to'g'ri ishga tushiring.

### 2. Birinchi ishga tushirish

Dastur faqat bitta savol beradi:

> **Skrinshot UZ klaviaturadagi PrintScreen tugmasini ishlatishiga ruxsat berasizmi?** [Ruxsat berish] [Yo'q]

- **Ruxsat berish:** PrintScreen bosilganda Skrinshot UZ ochiladi. Shu oynada **"Kompyuter yoqilganda avtomatik ishga tushsin"** belgisi ham bor. U standart holatda belgilanmagan va o'zingiz tanlaysiz.
- **Yo'q:** dastur faqat **Ctrl+Shift+S** va tray belgisi orqali ishlaydi.
- **Windows 11'da** PrintScreen'ni "Qirqish vositasi" (Snipping Tool) egallagan bo'lishi mumkin. Shunday bo'lsa, dastur buni tushuntirib, sozlamani o'chirishga **alohida ruxsat so'raydi**. Siz rozi bo'lmaguningizcha hech narsa o'zgarmaydi.

Barcha javoblarni keyin ham o'zgartirish mumkin: soat yonidagi Skrinshot UZ belgisini o'ng tugma bilan bosing.

### 3. Tugmalar

| Tugma | Nima qiladi |
|---|---|
| **PrintScreen** | Ekran xiralashadi: kerakli sohani sichqoncha bilan tanlang |
| **Alt + PrintScreen** | Faol oyna avtomatik tanlanadi |
| **Shift + PrintScreen** | Butun ekran hech qanday oynasiz darhol saqlanadi |
| **Ctrl + Shift + S** | PrintScreen o'rniga (ruxsat bermagan bo'lsangiz ham ishlaydi) |

**Soha tanlangandan keyin:**

| Tugma | Nima qiladi |
|---|---|
| **Ctrl + C** yoki **Enter** | Nusxa olish (Word, Paint, Telegram'ga Ctrl+V bilan qo'yish mumkin) |
| **Ctrl + S** | Saqlash (PNG yoki JPG) |
| **Ctrl + P** | Chop etish |
| **Ctrl + Z / Ctrl + Y** | Bekor qilish / qaytarish |
| **Delete** | Tanlangan chizmani o'chirish |
| **Esc** | Avval chizmadan tanlovni olib tashlaydi, keyin bekor qiladi |
| **P, L, A, R, M, T** | Qalam, chiziq, strelka, to'rtburchak, marker, matn |

**Chizmalar bilan ishlash:**
- Chizilgan narsani sichqoncha bilan **surish** mumkin.
- Strelka va chiziqning **ikkala uchini** cho'zish mumkin.
- To'rtburchakni **burchagidan** kattalashtirish mumkin.
- Matnni qayta tahrirlash uchun ustiga **ikki marta bosing**.
- **Shift** bosib turilsa, chiziq 45° qadam bilan, to'rtburchak esa kvadrat bo'lib chiziladi.

### 4. Saqlash va tarix

- **Saqlash joyi:** standart holatda `Rasmlar\Skrinshot UZ` papkasi, fayl nomi `skrinshot_2026-10-27_20-38-15.png` ko'rinishida. Oxirgi tanlangan papka va format eslab qolinadi.
- **"Saqlandi" xabari:** ustiga bosilsa, papka ochilib, fayl belgilangan holda ko'rinadi.
- **"Oxirgi skrinshotlar" oynasi** (tray menyusida): saqlangan rasmlar rasmchalari bilan. Ulardan birini ochish, nusxa olish, chop etish yoki papkasini ko'rsatish mumkin.

### 5. Chop etish (Ctrl + P)

Chop etish oynasida chapda sozlamalar, o'ngda esa A4 varaq qanday chiqishi ko'rinadi. Pastda rasmning qog'ozdagi aniq o'lchami millimetrda yoziladi.

| Rejim | Natija |
|---|---|
| **A4 to'liq varaq** | Rasm butun varaqqa, proporsiyasi buzilmasdan. Yo'nalish avtomatik tanlanadi (keng rasm — albom, baland rasm — kitob), qo'lda ham o'zgartirish mumkin |
| **A4 yarmi** | Varaqning yuqori yarmiga (210 × 148,5 mm) |
| **Bir varaqqa 2 ta** | Yuqori va pastki yarmiga, o'rtada qirqish uchun punktir chiziq. Standart holatda bir xil rasm; pastki yarmiga tarixdan boshqa rasm tanlash mumkin |
| **Asl o'lchamda** | Qog'ozda ekrandagi kattalikda (Windows masshtabi hisobga olinadi). Varaqqa sig'masa, tanlov beriladi: "Varaqqa sig'dirish" yoki "Bir nechta varaqqa bo'lish" |

**Qo'shimcha sozlamalar:** maydon (0/5/10/15 mm), oq-qora (rangli siyohni tejash), rasm ostiga sana va sarlavha (masalan, "7-B sinf, Algebra"), nusxalar soni, printer.

**PDF ga saqlash:** printer o'rniga PDF faylga saqlaydi (Windows'dagi "Microsoft Print to PDF" orqali).

### 6. O'chirish

**Windows sozlamalari → Ilovalar → Skrinshot UZ → O'chirish.**

- **O'chiriladi:** dastur, yorliqlar, sozlamalar va "Windows bilan birga ishga tushirish" yozuvi.
- **Qirqish vositasi:** agar PrintScreen sozlamasini Skrinshot UZ o'chirgan bo'lsa, uni qaytarish **so'raladi**.
- **Qoladi:** saqlangan skrinshotlaringiz (`Rasmlar\Skrinshot UZ`) hech qachon o'chirilmaydi.

### Reklama

1.1.0 versiyadan boshlab dastur kuniga **bir marta** (ishga tushganda, agar internet bo'lsa) ekranning pastki o'ng
burchagida kichik reklama oynasini ko'rsatadi. Oyna "Reklama" deb belgilangan, hech qachon ish oynangizni to'smaydi
(fokusni olmaydi), "Yopish" tugmasi bilan yopiladi va 60 soniyadan keyin o'zi yo'qoladi. Skrinshot olinayotganda u
yashiriladi, ya'ni rasmga tushmaydi. Internet bo'lmasa, hech narsa chiqmaydi va dastur odatdagidek ishlayveradi.

### Maxfiylik

- **Skrinshotlar, fayllar va kompyuter haqidagi ma'lumot hech qachon internetga yuborilmaydi.**
- Internetga faqat reklama uchun chiqiladi (kuniga bir marta):
  - reklamalar ro'yxati va rasmi yuklab olinadi;
  - serverga faqat "N-reklama ko'rildi" yoki "bosildi" degan umumiy hisoblagich yuboriladi.

  Foydalanuvchi, kompyuter nomi yoki biror identifikator yuborilmaydi; server faqat umumiy sonlarni saqlaydi. Har qanday internet so'rovidagi kabi, server IP manzilni ko'radi, lekin uni saqlamaydi.
- Dastur yangilanishlarni o'zi tekshirmaydi.
- Rasmlar faqat siz tanlagan papkada saqlanadi.
- Sozlamalar `%APPDATA%\Skrinshot UZ` papkasida turadi.
- Xatolik bo'lsa, `logs` papkasiga faqat texnik ma'lumot yoziladi (rasm emas).

---

## Dasturchilar uchun

### Tuzilma

```
SkrinshotUZ.sln
├─ src/SkrinshotUZ.Core/            platformaga bog'liq bo'lmagan mantiq (to'liq unit-test qilinadi)
│  ├─ Printing/                     chop etish rejasi (mm): A4, A4 yarmi, 2 ta, asl o'lcham, ko'p varaq
│  ├─ Editing/                      chizmalar, tahrirlash, bekor qilish/qaytarish
│  ├─ Capture/                      monitorlar, DPI, soha tanlash
│  ├─ Hotkeys/                      IHotkeyService, hook filtri, HotkeyManager
│  ├─ Permissions/                  ruxsat oqimi (PrintScreen, Qirqish vositasi, avtoishga tushirish, o'chirish)
│  ├─ Storage/ Settings/            fayl nomlari, saqlash, tarix, settings.json
│  └─ Localization/Strings.resx     interfeys matnlari (kirill/rus tili — yangi .resx qo'shish kifoya)
├─ src/SkrinshotUZ/                 WinForms ilova: tray, overlay, chop etish, WinAPI (Native/)
├─ tests/SkrinshotUZ.Core.Tests/    xUnit (mantiq)
├─ tests/SkrinshotUZ.Windows.Tests/ xUnit (Windows: ekranni olish, chizish, 300 DPI chop etish ±0,5 mm)
├─ installer/                       Inno Setup skripti va to'liq o'zbekcha tarjima (Uzbek.isl)
└─ .github/workflows/build.yml      build → test → .exe → tutun testi → o'rnatuvchi → o'rnatish testi
```

### .exe va o'rnatuvchini olish (GitHub Actions)

1. Kodni GitHub'ga yuklang (`git push`). **Actions** bo'limida "Build va test" o'zi ishga tushadi.
2. Yashil ✓ chiqqach, ishni oching. Pastdagi **Artifacts** bo'limida:
   - `SkrinshotUZ_Setup-…`: o'rnatuvchi (x64 + x86);
   - `SkrinshotUZ-…-win-x64` va `-win-x86`: o'rnatmasdan ishlaydigan bitta .exe.
3. **Rasmiy versiya chiqarish** uchun teg qo'ying:
   ```
   git tag v1.0.0 && git push origin v1.0.0
   ```
   Fayllar **Releases** sahifasiga avtomatik qo'yiladi (SHA256 bilan). Nomlari o'zgarmas, shuning uchun saytga
   qo'yiladigan doimiy havola har doim eng yangi versiyani beradi:
   `https://github.com/mirzaqulovqobiljon-max/SkrinshotUZ/releases/latest/download/SkrinshotUZ_Setup.exe`

CI har safar quyidagilarni tekshiradi:
- build ogohlantirishsiz;
- barcha testlar o'tadi;
- .exe haqiqatan ishga tushadi va xotira 40 MB dan kam;
- skrinshot → chizma → bufer → PNG/JPG → tarix → chop etish → haqiqiy PDF zanjiri ishlaydi;
- `Uzbek.isl` o'rnatilgan Inno Setup versiyasiga to'liq mos;
- o'rnatuvchi administratorsiz o'rnatadi, ishlab turgan dasturni yangilashda o'zi yopadi va o'chirganda skrinshotlarni qoldiradi.

### Reklama tizimi

To'liq sozlash qo'llanmasi: [docs/reklama-sozlash.md](docs/reklama-sozlash.md).

- `server/supabase.sql` — Supabase sxemasi: jadvallar, huquqlar (RLS), hisoblagich.
- `admin/reklama-admin.html` — admin sahifa (bitta fayl, saytga yuklanadi).
- Dasturga Supabase manzili va **ommaviy** kaliti GitHub Variables orqali beriladi: `ADS_SUPABASE_URL`, `ADS_SUPABASE_KEY`. Bo'sh bo'lsa, reklama o'chiq va dastur internetga umuman chiqmaydi.
- Testlar:
  - `server/tests` — PostgreSQL'da 42 ta huquq tekshiruvi;
  - `admin/tests` — Chromium'da 16 ta admin sahifa testi;
  - `tests/…/Ads` — dastur qismi.

### Mahalliy yig'ish (Windows, .NET 8 SDK)

```powershell
dotnet build SkrinshotUZ.sln -c Release -warnaserror
dotnet test SkrinshotUZ.sln -c Release
dotnet publish src/SkrinshotUZ/SkrinshotUZ.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
dotnet publish src/SkrinshotUZ/SkrinshotUZ.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x86
iscc installer\SkrinshotUZ.iss        # Inno Setup 6.5+ → publish\setup\SkrinshotUZ_Setup.exe
```

**Buyruq qatori parametrlari:**

| Parametr | Vazifasi |
|---|---|
| `--autostart` | Windows bilan birga ishga tushganda (xush kelibsiz xabari chiqmaydi) |
| `--smoke-test --smoke-report fayl.txt` | CI tekshiruvi (oynasiz, o'zi yopiladi) |
| `--exit` | Ishlab turgan nusxani yopadi (o'rnatuvchi yangilashdan oldin chaqiradi) |
| `--uninstall [--quiet]` | O'chirishdan oldin tozalash (o'rnatuvchi chaqiradi) |

**Jim o'rnatish** (IT xodimlari uchun): `SkrinshotUZ_Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`. Barcha foydalanuvchilar uchun o'rnatish: `/ALLUSERS` (administrator huquqi kerak).
