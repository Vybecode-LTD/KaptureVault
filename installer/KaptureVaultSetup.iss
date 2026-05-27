; KaptureVault Installer — Inno Setup 6+
;
; Build:
;   1. dotnet publish KaptureVault.csproj -c Release -r win-x64 --self-contained true
;        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
;        -o publish\win-x64
;   2. ISCC installer\KaptureVaultSetup.iss
;
; Output: installer\output\KaptureVaultSetup-1.0.0-x64.exe

#define MyAppName      "KaptureVault"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "Vybecode Ltd"
#define MyAppURL       "https://kapture.tools"
#define MyAppExeName   "KaptureVault.exe"
#define PublishDir     "..\publish\win-x64"

; ─────────────────────────────────────────────────────────────────────────────
[Setup]
AppId={{4F2A1D3E-7B8C-4E9F-A012-3B4C5D6E7F80}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Default install location — user can change on the directory page
DefaultDirName={autopf}\{#MyAppName}
; Default Start Menu folder — user can change on the program group page
DefaultGroupName={#MyAppName}

; ── Wizard pages ──────────────────────────────────────────────────────────────
; All three suppression flags are off so every page shows:
;   Welcome → License → Select Destination → Select Components → Ready → Installing → Finish
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
DisableReadyPage=no
DisableFinishedPage=no
; Show the chosen install path on the Ready to Install summary page
AlwaysShowDirOnReadyPage=yes
AlwaysShowGroupOnReadyPage=yes

; ── Output ────────────────────────────────────────────────────────────────────
OutputDir=output
OutputBaseFilename=KaptureVaultSetup-{#MyAppVersion}-x64
Compression=lzma2/ultra64
SolidCompression=yes

; ── UI ────────────────────────────────────────────────────────────────────────
WizardStyle=modern
; Wizard imagery — KV brand assets
SetupIconFile=..\Assets\app.ico
WizardImageFile=..\Assets\installer-wizard.bmp
WizardSmallImageFile=..\Assets\installer-banner.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}

; ── Platform ──────────────────────────────────────────────────────────────────
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; ─────────────────────────────────────────────────────────────────────────────
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ─────────────────────────────────────────────────────────────────────────────
[Tasks]
; Start Menu shortcut — checked by default (no flag needed; Inno default is checked)
Name: "startmenuicon"; \
  Description: "Create a &Start Menu shortcut"; \
  GroupDescription: "Shortcuts:"

; Desktop shortcut — unchecked by default; user opts in
Name: "desktopicon"; \
  Description: "Create a &Desktop shortcut"; \
  GroupDescription: "Shortcuts:"; \
  Flags: unchecked

; Startup — KaptureVault is a background tray app so startup makes sense;
; uses Task Scheduler (not the Run registry key) so it launches correctly
; even though the exe requests administrator privileges.
Name: "startup"; \
  Description: "Start KaptureVault automatically when &Windows starts (recommended)"; \
  GroupDescription: "Startup:"

; ─────────────────────────────────────────────────────────────────────────────
[Files]
; Main application files (single-file exe + native DLLs)
Source: "{#PublishDir}\*"; DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; Google OAuth credentials — bundled if present in project root (gitignored)
#define CredFile "..\client_secret.json"
#if FileExists(CredFile)
Source: "{#CredFile}"; DestDir: "{app}"; Flags: ignoreversion
#endif

; ─────────────────────────────────────────────────────────────────────────────
[Icons]
; Start Menu shortcuts (only if user kept that task checked)
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExeName}";   Tasks: startmenuicon
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}";          Tasks: startmenuicon

; Desktop shortcut (only if user checked that task)
Name: "{autodesktop}\{#MyAppName}";     Filename: "{app}\{#MyAppExeName}";   Tasks: desktopicon

; ─────────────────────────────────────────────────────────────────────────────
[Run]
; Register a Task Scheduler job for startup.
; /rl highest  — runs with the same elevation level as the logged-in user
; /sc onlogon  — triggers on any user logon
; /f           — force-overwrites if the task already exists
Filename: "schtasks.exe"; \
  Parameters: "/create /tn ""{#MyAppName}"" /tr """"""{app}\{#MyAppExeName}"""""" /sc onlogon /rl highest /f"; \
  Flags: runhidden waituntilterminated; \
  StatusMsg: "Registering startup task..."; \
  Tasks: startup

; Offer to launch KaptureVault immediately after install finishes
Filename: "{app}\{#MyAppExeName}"; \
  Description: "Launch {#MyAppName} now"; \
  Flags: nowait postinstall skipifsilent

; ─────────────────────────────────────────────────────────────────────────────
[UninstallRun]
; Remove the startup task when uninstalling
Filename: "schtasks.exe"; \
  Parameters: "/delete /tn ""{#MyAppName}"" /f"; \
  Flags: runhidden; \
  RunOnceId: "RemoveStartupTask"

; ─────────────────────────────────────────────────────────────────────────────
; User data lives in %LOCALAPPDATA%\KaptureVault\ — NOT removed automatically.
; The [Code] section below offers the user a choice during uninstallation.
[UninstallDelete]
Type: filesandordirs; Name: "{app}"

; ─────────────────────────────────────────────────────────────────────────────
[Code]

{ Called after the main uninstall step completes (files removed, shortcuts gone,
  startup task deleted). The wizard is still open so the MsgBox sits cleanly over it. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir  : String;
  Response : Integer;
begin
  if CurUninstallStep <> usPostUninstall then Exit;

  DataDir := ExpandConstant('{localappdata}\KaptureVault');
  if not DirExists(DataDir) then Exit;

  Response := MsgBox(
    'KaptureVault has been removed.' + #13#10 + #13#10 +
    'Would you also like to permanently delete all vault data?' + #13#10 + #13#10 +
    'This includes:' + #13#10 +
    '   - Captured keystrokes, clipboard history, and screenshots' + #13#10 +
    '   - The encrypted vault database' + #13#10 +
    '   - Encryption keys and Google Drive sync tokens' + #13#10 + #13#10 +
    'Location: ' + DataDir + #13#10 + #13#10 +
    'This cannot be undone. Click No to keep your data.',
    mbConfirmation,
    MB_YESNO or $100   { $100 = MB_DEFBUTTON2 — No is the safe default }
  );

  if Response = IDYES then
    DelTree(DataDir, True, True, True);
end;
