; KaptureVault Installer â€” Inno Setup 6+
;
; Build:
;   1. dotnet publish KaptureVault.csproj -c Release -r win-x64 --self-contained true
;        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
;        -o publish\win-x64
;   2. ISCC installer\KaptureVaultSetup.iss
;
; Output: installer\output\KaptureVaultSetup-1.0.0-x64.exe

#define MyAppName      "KaptureVault"
#define MyAppVersion "1.0.6"
#define MyAppPublisher "Vybecode Ltd"
#define MyAppURL       "https://kapture.tools"
#define MyAppExeName   "KaptureVault.exe"
#define PublishDir     "..\publish\win-x64"

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Setup]
AppId={{4F2A1D3E-7B8C-4E9F-A012-3B4C5D6E7F80}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Default install location â€” user can change on the directory page
DefaultDirName={autopf}\{#MyAppName}
; Default Start Menu folder â€” user can change on the program group page
DefaultGroupName={#MyAppName}

; â”€â”€ Wizard pages â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
; All three suppression flags are off so every page shows:
;   Welcome â†’ License â†’ Select Destination â†’ Select Components â†’ Ready â†’ Installing â†’ Finish
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
DisableReadyPage=no
DisableFinishedPage=no
; Show the chosen install path on the Ready to Install summary page
AlwaysShowDirOnReadyPage=yes
AlwaysShowGroupOnReadyPage=yes

; â”€â”€ Output â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
OutputDir=output
OutputBaseFilename=KaptureVaultSetup-{#MyAppVersion}-x64
Compression=lzma2/ultra64
SolidCompression=yes

; â”€â”€ UI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
WizardStyle=modern
; Wizard imagery â€” KV brand assets
SetupIconFile=..\Assets\app.ico
WizardImageFile=..\Assets\installer-wizard.bmp
WizardSmallImageFile=..\Assets\installer-banner.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}

; â”€â”€ Platform â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
; Admin is needed to write to Program Files â€” UAC is triggered automatically.
; The app itself runs as the invoking user (asInvoker in app.manifest).
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline dialog
; HKCU is used intentionally for the per-user startup Run key â€” suppress the
; "admin installer writing to HKCU" warning since this is the desired behaviour.
UsedUserAreasWarning=no
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Tasks]
; Start Menu shortcut â€” checked by default (no flag needed; Inno default is checked)
Name: "startmenuicon"; \
  Description: "Create a &Start Menu shortcut"; \
  GroupDescription: "Shortcuts:"

; Desktop shortcut â€” unchecked by default; user opts in
Name: "desktopicon"; \
  Description: "Create a &Desktop shortcut"; \
  GroupDescription: "Shortcuts:"; \
  Flags: unchecked

; Startup â€” KaptureVault is a background tray app so startup makes sense;
; uses Task Scheduler (not the Run registry key) so it launches correctly
; even though the exe requests administrator privileges.
Name: "startup"; \
  Description: "Start KaptureVault automatically when &Windows starts (recommended)"; \
  GroupDescription: "Startup:"

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Files]
; Main application files (single-file exe + native DLLs)
Source: "{#PublishDir}\*"; DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; Google OAuth credentials â€” bundled if present in project root (gitignored)
#define CredFile "..\client_secret.json"
#if FileExists(CredFile)
Source: "{#CredFile}"; DestDir: "{app}"; Flags: ignoreversion
#endif

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Icons]
; Start Menu shortcuts (only if user kept that task checked)
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExeName}";   Tasks: startmenuicon
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}";          Tasks: startmenuicon

; Desktop shortcut (only if user checked that task)
Name: "{autodesktop}\{#MyAppName}";     Filename: "{app}\{#MyAppExeName}";   Tasks: desktopicon

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
; Startup â€” registry Run key (correct for a non-elevated / asInvoker app).
; Written to HKCU so no admin rights are needed at launch time.
[Registry]
Root: HKCU; \
  Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; \
  ValueName: "{#MyAppName}"; \
  ValueData: """{app}\{#MyAppExeName}"""; \
  Flags: uninsdeletevalue; \
  Tasks: startup

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
[Run]
; Offer to launch KaptureVault immediately after install finishes.
; shellexec lets Windows handle the normal (non-elevated) launch correctly.
Filename: "{app}\{#MyAppExeName}"; \
  Description: "Launch {#MyAppName} now"; \
  Flags: nowait postinstall skipifsilent shellexec

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
; The startup registry value is removed automatically on uninstall because of
; the uninsdeletevalue flag on the [Registry] entry above â€” no [UninstallRun]
; schtasks call needed.
;
; User data lives in %LOCALAPPDATA%\KaptureVault\ â€” NOT removed automatically.
; The [Code] section below offers the user a choice during uninstallation.
[UninstallDelete]
Type: filesandordirs; Name: "{app}"

; â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    MB_YESNO or $100   { $100 = MB_DEFBUTTON2 â€” No is the safe default }
  );

  if Response = IDYES then
    DelTree(DataDir, True, True, True);
end;






