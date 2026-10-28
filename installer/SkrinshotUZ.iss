; =====================================================================================
;  Skrinshot UZ — o'rnatuvchi (Inno Setup 6.5+)
;
;  Natija: SkrinshotUZ_Setup.exe — bitta fayl, 64-bit va 32-bit Windows uchun:
;  64-bit kompyuterga win-x64, 32-bit (eski maktab kompyuterlari) ga win-x86 .exe o'rnatiladi.
;
;  Qoidalar (loyiha talabi):
;   * Ruxsatlar (PrintScreen, Windows bilan birga ishga tushirish, Qirqish vositasi) o'rnatuvchida
;     SO'RALMAYDI — dastur birinchi ochilganda bir joyda so'raladi. O'rnatuvchi reyestrda hech qanday
;     tizim sozlamasini o'zgartirmaydi.
;   * Administrator huquqi shart emas: standart holatda faqat joriy foydalanuvchi uchun o'rnatiladi
;     (%LOCALAPPDATA%\Programs\Skrinshot UZ). IT xodimi xohlasa, "barcha foydalanuvchilar uchun" tanlaydi.
;   * O'chirishda skrinshotlar (Rasmlar\Skrinshot UZ) HECH QACHON o'chirilmaydi.
;
;  Yig'ish (CI'da avtomatik):
;    iscc /DAppVersion=1.0.0 /DAppNumericVersion=1.0.0.0 /DSourceX64=publish\win-x64 /DSourceX86=publish\win-x86 installer\SkrinshotUZ.iss
; =====================================================================================

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppNumericVersion
  #define AppNumericVersion "1.0.0.0"
#endif
#ifndef SourceX64
  #define SourceX64 "..\publish\win-x64"
#endif
#ifndef SourceX86
  #define SourceX86 "..\publish\win-x86"
#endif
#ifndef OutputDir
  #define OutputDir "..\publish\setup"
#endif

#define AppName "Skrinshot UZ"
#define AppExeName "SkrinshotUZ.exe"
#define AppPublisher "Skrinshot UZ"
#define AppUrl "https://github.com/mirzaqulovqobiljon-max/SkrinshotUZ"

[Setup]
; AppId — o'zgarmas: yangi versiya eskisining ustiga o'rnatiladi (yangilash).
AppId={{45B6F012-9A70-43A6-901D-74D221DC5E30}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppComments=O'zbek maktablari uchun skrinshot dasturi. To'liq oflayn ishlaydi.
VersionInfoVersion={#AppNumericVersion}
VersionInfoProductVersion={#AppNumericVersion}
VersionInfoDescription={#AppName} o'rnatuvchisi
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=© 2026 {#AppPublisher}

DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableReadyPage=no
UsePreviousAppDir=yes

; Administrator huquqisiz (maktab kompyuterlarida o'qituvchida ko'pincha admin yo'q).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
UsedUserAreasWarning=no

; Bitta o'rnatuvchi — ikkala arxitektura (ARM64 Windows'da x64 emulyatsiya bilan ishlaydi).
ArchitecturesAllowed=x86compatible x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

WizardStyle=modern
SetupIconFile=..\src\SkrinshotUZ\Resources\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

OutputDir={#OutputDir}
OutputBaseFilename=SkrinshotUZ_Setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Fayl band bo'lsa (masalan, dastur yopilmay qolgan) — Windows Restart Manager orqali yopish.
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no

[Languages]
; Asosi — inglizcha Default.isl, ustidan to'liq o'zbekcha tarjima (yangi Inno versiyasidagi xabar ham yo'qolmaydi).
Name: "uz"; MessagesFile: "compiler:Default.isl,Uzbek.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceX64}\{#AppExeName}"; DestDir: "{app}"; Check: Is64BitInstallMode; Flags: ignoreversion
Source: "{#SourceX86}\{#AppExeName}"; DestDir: "{app}"; Check: not Is64BitInstallMode; Flags: ignoreversion solidbreak

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Ekran rasmini olish (PrintScreen)"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Ekran rasmini olish (PrintScreen)"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Faqat dastur sozlamalari, tarix ro'yxati va xatolik loglari. Skrinshotlarning o'zi (Rasmlar\Skrinshot UZ) qoladi.
Type: filesandordirs; Name: "{userappdata}\{#AppName}"

[Code]
const
  ExitTimeoutNote = 'Dastur 10 soniya ichida yopilmasa, Restart Manager yopadi';

function InstalledExe(): String;
begin
  Result := ExpandConstant('{app}\{#AppExeName}');
end;

{ Ishlab turgan Skrinshot UZ'ni yopadi (yangilashdan oldin): SkrinshotUZ.exe --exit — u nusxaga signal beradi
  va yopilishini kutadi. Eski fayl yo'q bo'lsa (birinchi o'rnatish) — hech narsa qilinmaydi. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if FileExists(InstalledExe()) then
  begin
    Log('Ishlab turgan nusxa yopilmoqda: ' + InstalledExe() + ' --exit (' + ExitTimeoutNote + ')');
    if not Exec(InstalledExe(), '--exit', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      Log('--exit ishga tushmadi: ' + SysErrorMessage(Code))
    else
      Log('--exit natijasi: ' + IntToStr(Code));
  end;
end;

{ O'chirish tasdiqlangandan keyin, fayllar o'chirilishidan OLDIN: SkrinshotUZ.exe --uninstall
  — ishlab turgan nusxani yopadi, "Windows bilan birga ishga tushirish" yozuvini olib tashlaydi va,
  Qirqish vositasi sozlamasini Skrinshot UZ o'chirgan bo'lsa, uni qaytarishni SO'RAYDI.
  Jim o'chirishda (--quiet) hech narsa so'ralmaydi va tizim sozlamasi o'zgartirilmaydi. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
  Params: String;
begin
  if (CurUninstallStep = usUninstall) and FileExists(InstalledExe()) then
  begin
    Params := '--uninstall';
    if UninstallSilent then
      Params := Params + ' --quiet';
    if not Exec(InstalledExe(), Params, '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then
      Log('--uninstall ishga tushmadi: ' + SysErrorMessage(Code))
    else
      Log('--uninstall natijasi: ' + IntToStr(Code));
  end;
end;
