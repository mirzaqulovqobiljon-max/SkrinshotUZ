# Skrinshot UZ

O'zbek maktablari uchun Windows skrinshot dasturi (Lightshot analogi). To'liq oflayn — hech qanday rasm internetga yuklanmaydi.

> Holat: **1-bosqich** — loyiha tuzilmasi, chop etish maketi hisoblari, testlar, CI. To'liq qo'llanma 7-bosqichda yoziladi.

## Tuzilma

```
SkrinshotUZ.sln
├─ src/SkrinshotUZ.Core/          platformaga bog'liq bo'lmagan mantiq (to'liq unit-test qilinadi)
│  ├─ Printing/                   A4 to'liq, A4 yarmi, asl o'lcham, ko'p varaqqa bo'lish (mm)
│  ├─ Storage/                    fayl nomlari, papkalar
│  ├─ Startup/                    buyruq qatori parametrlari
│  └─ Localization/Strings.resx   interfeys matnlari (o'zbek, lotin)
├─ src/SkrinshotUZ/               WinForms ilova (tray, keyin: PrintScreen, tahrirlash, chop etish)
├─ tests/SkrinshotUZ.Core.Tests/  xUnit testlar
└─ .github/workflows/build.yml    Windows'da build → test → .exe (x64, x86) → tutun testi
```

## .exe ni olish (GitHub Actions)

1. GitHub'da yangi repozitoriy yarating va shu papkani unga yuklang (`git push`).
2. Repozitoriyda **Actions** bo'limini oching → "Build va test" ishga tushadi (yoki **Run workflow** tugmasi).
3. Yashil ✓ belgisi chiqqach, ishga kiring → pastda **Artifacts**:
   - `SkrinshotUZ-…-win-x64` — 64-bit Windows 10/11 uchun
   - `SkrinshotUZ-…-win-x86` — 32-bit eski kompyuterlar uchun
4. Zip'ni oching — ichida bitta `SkrinshotUZ.exe` (kompyuterga .NET o'rnatish shart emas).

## Mahalliy yig'ish (Windows, .NET 8 SDK)

```powershell
dotnet build SkrinshotUZ.sln -c Release -warnaserror
dotnet test SkrinshotUZ.sln -c Release
dotnet publish src/SkrinshotUZ/SkrinshotUZ.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```

Tutun testi (dastur 3 soniya ishlab, xotira hisobotini yozadi va yopiladi):

```powershell
publish\win-x64\SkrinshotUZ.exe --smoke-test --smoke-report smoke.txt
```
