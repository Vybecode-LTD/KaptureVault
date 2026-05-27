; KaptureVault Installer — Inno Setup Script
; Requires Inno Setup 6+ (https://jrsoftware.org/isinfo.php)
;
; Build steps:
;   1. dotnet publish (see README section in this file)
;   2. ISCC installer\KaptureVaultSetup.iss
;      (or: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\KaptureVaultSetup.iss)
;
; Output: installer\output\KaptureVaultSetup-1.0.0-x64.exe

#define MyAppName      "KaptureVault"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "VybeCode"
#define MyAppURL       "https://github.com/VybeCodeLTD"
#define MyAppExeName   "KaptureVault.exe"
; Path to dotnet publish output — relative to this .iss file location (installer\)
#define PublishDir     "..\publish\win-x64"

[Setup]
; Fresh GUID — must not match the Kapture installer AppId
AppId={{4F2A1D3E-7B8C-4E9F-A012-3B4C5D6E7F80}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
; Install to Program Files\KaptureVault by default
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Don't show the "Select Start Menu Folder" page — we control it in [Icons]
DisableProgramGroupPage=yes
; Installer output
OutputDir=output
OutputBaseFilename=KaptureVaultSetup-{#MyAppVersion}-x64
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; UI
WizardStyle=modern
; Require admin for install (matches app.manifest requireAdministrator)
PrivilegesRequired=admin
; Target 64-bit Windows only
ArchitecturesInstallIn64BitMode=x64compatible
; Branding
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; Windows 10 minimum (matches app.manifest supportedOS)
MinVersion=10.0
; Sign the uninstaller alongside the installer (leave blank if not signing)
; SignTool=AzureSign

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Desktop shortcut — optional, defaults to unchecked
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Single-file publish produces KaptureVault.exe plus a small number of native DLLs
; (e.g. e_sqlite3.dll, libSkiaSharp.dll) that cannot be bundled into the exe.
; recursesubdirs + createallsubdirs handles any nested runtime folders safely.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcut
Name: "{group}\{#MyAppName}";            Filename: "{app}\{#MyAppExeName}"
; Start Menu uninstall shortcut
Name: "{group}\Uninstall {#MyAppName}";  Filename: "{uninstallexe}"
; Desktop shortcut (only if user checked the task above)
Name: "{autodesktop}\{#MyAppName}";      Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; ---------------------------------------------------------------------------
; [Run] section intentionally omitted.
; KaptureVault is a tray/background app — the user launches it manually.
; Auto-launching after install is undesirable for an elevated tray process.
; ---------------------------------------------------------------------------

; ---------------------------------------------------------------------------
; User data note:
; KaptureVault stores data in %LOCALAPPDATA%\KaptureVault\ (user-owned).
; This data is NOT removed by the uninstaller so no vault data is lost.
; Users can manually delete that folder if they want a clean removal.
; ---------------------------------------------------------------------------

[UninstallDelete]
; Remove any log/cache files written next to the exe during operation,
; but do NOT touch %LOCALAPPDATA%\KaptureVault\ (vault data stays).
Type: filesandordirs; Name: "{app}"
