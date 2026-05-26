# CLAUDE.md — Kapture

> **Living context document for Claude Code sessions.** Read this first when starting any new session. Update the "Session log" section at the bottom whenever a meaningful task ships.

---

## Project Identity

**Kapture** is a feature-rich Windows desktop power tool (v1.0.27) that combines **keystroke/clipboard/screenshot capture** with a full suite of **system optimization, privacy hardening, and maintenance utilities**. Think of it as a personal productivity vault fused with a Windows system-tuning dashboard.

**Author:** Big Haas's son  
**Environment:** Claude Code Max, Windows 11, Visual Studio / Rider  
**Repo root:** `C:\Users\vybec\OneDrive\Documents\Development\Utilities\Kapture\csharp`

---

## Stack & Dependencies

| Layer | Technology |
|-------|-----------|
| **Runtime** | .NET 9 (`net9.0-windows`), C# 13, implicit usings |
| **UI Framework** | Avalonia 11.3.12 (FluentTheme base) |
| **MVVM** | CommunityToolkit.Mvvm 8.2.1 (`[ObservableProperty]`, `[RelayCommand]`, source generators) |
| **DI** | Microsoft.Extensions.DependencyInjection 9.0.0 |
| **Database** | Microsoft.Data.Sqlite 9.0.0 (local SQLite, stored in `%LOCALAPPDATA%\Kapture`) |
| **Logging** | Serilog 4.2.0 → File sink |
| **Editor** | AvaloniaEdit 11.4.1 + TextMate grammars |
| **System APIs** | System.Management 9.0, System.ServiceProcess.ServiceController 9.0, System.Diagnostics.PerformanceCounter 10.0 |
| **Cloud Sync** | Google Drive API (OAuth2 PKCE via localhost redirect) |
| **Publish** | Single-file self-contained `win-x64` |
| **Single-instance** | `Global\Kapture_SingleInstance_B7E3F4A2` mutex |
| **Elevation** | `app.manifest` → `requireAdministrator` (needed for system tweaks, services, registry writes) |

---

## Application Sections (8 tabs)

### 1. Vault (default section)
Three-column layout: **Sidebar** (app list + tag list) → **Entry list** (with type badges, buffer fill bars, language detection) → **Content reader** (preview, tags, actions). Captures keystrokes, clipboard content, and screenshots. Entries are stored in SQLite with full-text search, tagging, pinning, and auto-expiry.

### 2. Tweaks
Three-column layout: **Category sidebar** (with undo history) → **Tweak list** (severity badges: Safe/Moderate/Advanced/Aggressive) → **Detail/preview pane** (preview results, targets, warnings, execute/undo buttons). 60+ system tweaks across 8 categories: Visual, Cache, Storage, Services, Registry, Drivers, Security, Performance.

### 3. Services
Two-column layout: **Service list** (with status dots, safety ratings, search/filter) → **Detail pane** (description, service details, safety warnings, start/stop controls). Full Windows service browser with safety classification (Safe/Caution/Unsafe/Critical).

### 4. Dashboard
Full-page scrollable layout with live-updating cards: **System info** (CPU name, GPU, OS, uptime) → **CPU gauge** (animated progress bar) → **RAM gauge** (used/free/total) → **Network** (send/recv rates) → **Disk drives** (per-drive usage bars). Uses `PerformanceCounter` and WMI for real-time data. Disposed on window close.

### 5. Profiles
Three profile cards (Gaming, Work, Battery Saver) with detailed tweak lists and one-click apply buttons. Each profile applies a preset combination of registry tweaks, service changes, and power plan adjustments.

### 6. Startup
Two-column layout: **Startup item list** (boot time stats, enabled/disabled counts) → **Detail pane** (command, location, publisher, type, toggle controls). Scans both registry Run keys and shell Startup folder.

### 7. Scheduler
Two-column layout: **Job creator + job list** → **Job detail pane** (next run, last run, run-now, delete). Creates Windows Task Scheduler tasks that run specific tweaks on a schedule (daily, weekly, on boot).

### 8. Privacy
Two-column layout: **Privacy score banner + category filter + toggle list** → **Tabbed detail pane** with two sub-tabs:
- **Setting Detail:** Toggle description, registry info (hive/key/value/recommendation level), secure/revert buttons.
- **Network Monitor:** Live telemetry connection scanner using `netstat -n -o`. Cross-references Microsoft IP ranges with known telemetry process names (svchost, diagtrack, utcsvc, etc.). Start/stop monitoring with `DispatcherTimer`. Auto-stops when navigating away from Privacy tab or closing window.

30+ privacy toggles across: Telemetry, Advertising, Cortana & Copilot, Location, Camera & Mic, App Permissions, Search, Diagnostics, Misc.

---

## UI / Theme Architecture

### Design System
- **Base:** Avalonia FluentTheme (Dark default, 6 theme options: Dark, Light, Sunset, Dawn, Oceanic, Rose)
- **Custom theme:** `Themes/AppTheme.axaml` — all reusable style classes
- **Resources:** `App.axaml` — color palette, shadows, icon font
- **Primary bg:** `#0D1117` → **Accent:** `#F0A500` (amber/gold)
- **Icons:** Segoe MDL2 Assets (`{StaticResource SymbolFont}`) — used throughout nav, stats, actions, empty states

### Reusable Style Classes
| Class | Element | Purpose |
|-------|---------|---------|
| `card` | Border | Elevated surface with shadow, rounded corners, border |
| `card-hover` | Border | Card with hover lift animation + shadow transition |
| `card-accent` | Border | Card with colored left accent bar |
| `panel-header` | Border | Section header with downward shadow for depth |
| `panel-footer` | Border | Action bar with upward shadow for depth |
| `progress-track` | Border | Inset-shadowed track for progress bars |
| `search-overlay` | Border | Floating search popup (dialog shadow, accent border) |
| `nav-btn` | Button | Transparent nav button with opacity/scale transitions |
| `nav-active` | Button | Active state for nav (accent color, bold, full opacity) |
| `accent` | Button | Amber accent button (accent glow via BoxShadow on wrapping Borders only) |
| `danger` | Button | Red outline/text delete button |
| `icon-btn` | Button | Transparent toolbar icon button |
| `section-header` | TextBlock | Accent-colored 13px semibold section label |
| `detail-label` | TextBlock | 12px secondary-colored key label |
| `detail-value` | TextBlock | 12px semibold primary-colored value |
| `mono` | TextBlock | Consolas/Courier monospace 11px |
| `empty-state` | TextBlock | Centered, italic, 14px, 70% opacity placeholder |
| `icon` | TextBlock | Segoe MDL2 Assets, 14px, vertically centered |
| `icon-lg` | TextBlock | Segoe MDL2 Assets, 42px, 25% opacity decorative |
| `pill` | Border | Small rounded badge |
| `separator` | Border | 1px vertical divider |
| `kbd-hint` | TextBlock | Keyboard shortcut hint text |

### Shadow Hierarchy (Dark theme)
| Resource | Value | Used for |
|----------|-------|----------|
| `CardShadow` | `0 2 8 0 #22000000, 0 0 1 0 #12000000` | Cards |
| `CardShadowHover` | `0 8 24 0 #38000000, 0 2 6 0 #18000000` | Hovered cards |
| `HeaderShadow` | `0 2 8 0 #18000000` | Panel headers casting shadow on content |
| `FooterShadow` | `0 -2 8 0 #14000000` | Action bars floating above content |
| `NavPillShadow` | `inset 0 1 4 0 #20000000` | Recessed nav pill container |
| `InsetShadow` | `inset 0 1 3 0 #25000000` | Progress bar tracks |
| `DialogShadow` | `0 16 48 0 #60000000, 0 4 12 0 #30000000` | Dialogs, search overlay |
| `AccentGlow` | `0 0 16 0 #35F0A500` | Accent-colored Border elements |

### Key Avalonia Patterns
- **Conditional classes:** `Classes="nav-btn" Classes.nav-active="{Binding IsXxxActive}"` — NOT converter-based
- **Color vs Brush:** `Color` resources (e.g., `StatusRunning`) for `<SolidColorBrush Color="{Binding ...}"/>` bindings; `Brush` resources (e.g., `SuccessBrush`) for direct property assignment
- **DynamicResource** for theme-switched values, **StaticResource** for fixed values (SymbolFont, semantic brushes)
- **Panel** container for layering visible/hidden content (tab switching in Privacy view)
- **`FuncValueConverter<TIn, TOut>`** for inline converters defined as static fields on ViewModels
- **BoxShadow is Border-only** — cannot be set on Button, ListBoxItem, or other controls (will get AVLN2000 error)

### Search System
- **Spotlight-style popup**: Ctrl+K opens a floating centered search overlay (520px wide, dialog shadow)
- Search button in nav bar with magnifying glass icon + "Ctrl+K" badge
- `IsSearchOpen` property on MainWindowViewModel; `ToggleSearchCommand` / `CloseSearchCommand`
- Escape closes and clears; auto-focuses TextBox on open
- SearchText is forwarded to active section's ViewModel via `OnMainVmPropertyChanged` in code-behind

---

## System Tweaks Module

### Architecture
`ITweak` interface with `PreviewAsync → ExecuteAsync → UndoAsync` lifecycle. Foundation services handle cross-cutting concerns:
- `TweakRunner` — orchestrates preview/execute/undo with error handling
- `UndoLog` — JSONL undo records with before-state snapshots
- `RestorePointService` — System Restore points for HKLM/service/driver tweaks
- `RegistryBackup` — registry key backup before mutation
- `ShellNotifier` — Explorer restart after shell-affecting tweaks
- `FileCleanupHelper` — safe file/directory deletion with access handling
- `DriverParser` — parses `driverquery` and PnP device data
- `ElevationCheck` — verifies admin context

### Tweak Categories (60+ tweaks)
| Category | Count | Examples |
|----------|-------|---------|
| Visual | 12 | Dark mode, classic context menu, file extensions, taskbar alignment, disable Cortana/tips/ads |
| Cache | 5 | App cache, DNS cache, shell icon cache, shader cache, browser cache |
| Storage | 4 | Temp files, crash dumps, recycle bin, hibernation toggle |
| Services | 8 | DiagTrack, Fax, MapsBroker, RetailDemo, Xbox, Background apps, GameDVR, SysMain |
| Registry | 3 | Clear recent docs, typed paths, Run MRU |
| Drivers | 5 | Old driver cleanup, driver inventory, problem scan, OEM tool detection, driver backup |
| Security | 10 | Disable SMB1, RDP, Remote Assistance, AutoRun, LLMNR, NetBIOS, WDigest, Guest account; enable audit logging; security posture scan |
| Performance | 1 | High performance power plan |

### Safety Flags
Every tweak declares: `Severity` (Safe/Moderate/Advanced/Aggressive), `Reversibility` (FullyReversible/PartiallyReversible/Irreversible), `RequiresElevation`, `RequiresRestart`, `RequiresExplorerRestart`, `RequiresRestorePoint`.

---

## File Layout

```
csharp/
├── App.axaml / App.axaml.cs          Application resources, DI setup, theme dictionaries
├── Program.cs                         Entry point, single-instance mutex
├── Kapture.csproj                     Project file (v1.0.27)
├── app.manifest                       requireAdministrator, longPathAware, PerMonitorV2
├── CLAUDE.md                          THIS FILE
│
├── Assets/
│   ├── app.ico                        Application icon
│   ├── tray-recording.png             System tray icon (recording state)
│   └── tray-paused.png                System tray icon (paused state)
│
├── Models/
│   ├── AppSettings.cs                 Settings model (JSON-serializable)
│   └── CaptureEntry.cs                Vault entry model (keyboard/clipboard/screenshot)
│
├── Services/
│   ├── ActiveWindowService.cs         Win32 foreground window tracking
│   ├── CaptureService.cs              Keystroke capture → buffer → flush to DB
│   ├── ClipboardMonitorService.cs     Clipboard change monitoring
│   ├── ScreenshotService.cs           Periodic screenshot capture
│   ├── DatabaseService.cs             SQLite CRUD, search, stats, tags, expiry
│   ├── SettingsService.cs             JSON settings load/save (~/.kapture/settings.json)
│   ├── KeyboardHookService.cs         Low-level keyboard hook (Win32)
│   ├── HotkeyService.cs              Global hotkey registration (Quick Paste)
│   ├── LanguageDetector.cs            Programming language detection
│   ├── EncryptionService.cs           AES-256-GCM encryption at rest
│   ├── StartupService.cs             Registry-based startup registration
│   ├── I*.cs                          Interfaces for all services
│   └── CloudSync/
│       ├── ICloudStorageProvider.cs    Provider interface
│       ├── GoogleDriveProvider.cs      Google Drive implementation
│       ├── CloudSyncManager.cs         Sync orchestration
│       ├── CloudTokenStore.cs          DPAPI-secured OAuth token storage
│       └── OAuthHelper.cs             OAuth2 PKCE helper
│
├── SystemTweaks/
│   ├── TweakServicesRegistration.cs   DI registration for all tweaks
│   ├── Core/
│   │   ├── ITweak.cs                  Tweak interface + metadata
│   │   ├── TweakRunner.cs             Preview/Execute/Undo orchestrator
│   │   ├── UndoRecord.cs              Undo entry model
│   │   ├── UndoLog.cs                 JSONL undo persistence
│   │   ├── RestorePointService.cs     System Restore point creation
│   │   ├── RegistryBackup.cs          Registry key backup
│   │   ├── ShellNotifier.cs           Explorer restart helper
│   │   ├── FileCleanupHelper.cs       Safe file deletion
│   │   ├── DriverParser.cs            Driver query parsing
│   │   └── ElevationCheck.cs          Admin context verification
│   └── Tweaks/
│       ├── Visual/                    12 visual/UX tweaks
│       ├── Cache/                      5 cache-clearing tweaks
│       ├── Storage/                    4 storage-purging tweaks
│       ├── Services/                   8 service-management tweaks
│       ├── Registry/                   3 registry-cleanup tweaks
│       ├── Drivers/                    5 driver-management tweaks
│       ├── Security/                  10 security-hardening tweaks
│       └── RegistryClean/              1 network optimization tweak
│
├── Themes/
│   ├── AppTheme.axaml                 All reusable style classes
│   ├── Colors.cs                      Theme color definitions (6 themes)
│   ├── ThemeDefinition.cs             Theme model
│   └── ThemeRegistry.cs               Theme lookup/application
│
├── ViewModels/
│   ├── MainWindowViewModel.cs         Vault + navigation + search + stats
│   ├── TweaksViewModel.cs             Tweaks section
│   ├── ServicesViewModel.cs           Services section
│   ├── DashboardViewModel.cs          Dashboard section (IDisposable for timers)
│   ├── ProfilesViewModel.cs           Profiles section
│   ├── StartupViewModel.cs            Startup section
│   ├── SchedulerViewModel.cs          Scheduler section
│   ├── PrivacyViewModel.cs            Privacy section (tab switching, telemetry monitor)
│   ├── SettingsViewModel.cs           Settings dialog
│   ├── ExpiryDialogViewModel.cs       Expiry picker dialog
│   ├── Converters.cs                  Shared value converters
│   └── ViewModelBase.cs               Base class (ObservableObject)
│
├── Views/
│   ├── MainWindow.axaml/.cs           Main window (stats bar, nav, search overlay, vault, section switching)
│   ├── TweaksView.axaml/.cs           Tweaks section
│   ├── ServicesView.axaml             Services section
│   ├── DashboardView.axaml            Dashboard section
│   ├── ProfilesView.axaml             Profiles section
│   ├── StartupView.axaml              Startup section
│   ├── SchedulerView.axaml            Scheduler section
│   ├── PrivacyView.axaml              Privacy section
│   ├── SettingsWindow.axaml/.cs       Settings dialog
│   ├── QuickPasteWindow.axaml/.cs     Quick Paste popup (Ctrl+Shift+V)
│   ├── ContentViewerWindow.axaml/.cs  Full-content viewer dialog
│   └── Dialogs/
│       ├── DeleteConfirmDialog.axaml/.cs
│       ├── ExpiryDialog.axaml/.cs
│       └── PasswordDialog.axaml/.cs
│
├── Helpers/
│   └── TextEditorBindingHelper.cs     AvaloniaEdit binding helper
│
└── ViewLocator.cs                     Avalonia view resolution
```

---

## Coding Conventions

- **C# 13** features: file-scoped namespaces, primary constructors where appropriate, collection expressions `[]`
- Filenames match class names (one public type per file)
- `internal` by default, `public` only when crossing assembly boundaries
- Records for DTOs/models, classes for services/ViewModels
- `nameof(X)` over string literals for parameter names
- `ArgumentNullException.ThrowIfNull(x)` for guards
- **No `async void`** except event handlers
- `async Task` everywhere, `CancellationToken` accepted by anything that does file/network/DB work
- **MVVM strictly**: Views know about ViewModels; ViewModels never know about Views
- **DI for all services**: Constructor injection, no statics for cross-cutting concerns

---

## Build & Run

```powershell
# From the csharp directory
dotnet build                              # Debug build
dotnet build --no-restore                 # Skip NuGet restore (faster)
dotnet run                                # Run in debug

# If Kapture.exe is locked (running in background):
dotnet build --no-restore -o bin/Debug/net9.0-windows/test-build

# Single-file publish
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Important:** The app runs as admin (`requireAdministrator`). If the exe is locked by a running instance, builds will fail with MSB3027/MSB3021 — kill the process or build to an alternate output path.

---

## Known Issues & Warnings

- `BoxShadow` only works on `Border` elements — setting it on Button/ListBoxItem/etc. causes AVLN2000 compile error
- Privacy ViewModel's `PrivacyScoreColor` and toggle `StatusColor`/`RecommendationColor` are hard-coded hex strings returned as properties bound to `<SolidColorBrush Color="{Binding ...}"/>` — these can't easily use DynamicResource since they're string properties on the ViewModel
- Dashboard's `DashboardViewModel` implements `IDisposable` for PerformanceCounter cleanup — must be disposed on window close
- Privacy's `DispatcherTimer` must be stopped when leaving the Privacy tab or closing the window (handled in MainWindow.axaml.cs)

---

## Session Log

- **2026-05-10:** Initial scaffolding for System Tweaks module — ITweak, UndoRecord, UndoLog, RestorePointService, ShellNotifier, RegistryBackup, ElevationCheck, TweakRunner. Three sample tweaks. app.manifest updated.
- **2026-05-11 → 2026-05-18:** Built out 60+ tweaks across 8 categories. Added Services browser, Dashboard, Profiles, Startup Analyzer, Scheduler, Privacy Dashboard sections. Implemented all ViewModels and Views.
- **2026-05-19 (session 1):** Visual consistency pass — applied card/section-header/detail-label/detail-value/mono/danger/empty-state style classes across all views. Rewrote nav bar with nav-btn/nav-active conditional classes. Replaced all hard-coded hex colors with theme resources. Added DialogShadow to QuickPaste. Privacy monitor full redesign: tabbed Detail/Monitor sub-sections, timer leak fixes, improved telemetry detection (process name + MS IP range cross-reference).
- **2026-05-19 (session 2):** Visual depth & UI overhaul. (1) Enhanced shadow system — multi-layer CardShadow, new HeaderShadow/FooterShadow/NavPillShadow/InsetShadow/AccentGlow resources, panel-header/panel-footer/progress-track style classes applied to all 7 detail views. (2) Search redesign — replaced always-visible TextBox with Ctrl+K spotlight-style floating search overlay (search-overlay class, auto-focus, Escape-to-close). (3) Segoe MDL2 Assets icons — SymbolFont resource, icon/icon-lg style classes, icons added to nav bar (8 sections), stats bar (5 metrics), action buttons (Copy/Save/Pin/Expiry/Delete), type filters, pin indicator, settings button, toast, all 7 empty states (42px decorative icons), profile cards (controller/briefcase/battery with color glow), Dashboard (CPU/RAM/Network/Disk icons), delete confirm dialog.
- **2026-05-21 → 2026-05-23 (sessions 1-3):** Production-readiness remediation pass driven by `claude-audit/` documents. (1) **P0 Safety**: Rewrote ProfilesViewModel to delegate to TweakRunner via declarative ProfileTweakBase (GamingProfileTweak, WorkProfileTweak, BatterySaverProfileTweak). PrivacyViewModel now creates PrivacyToggleAdapter instances routed through TweakRunner. All system mutations have undo records and restore points. (2) **P0 Data**: DPAPI token encryption (CloudTokenStore), SemaphoreSlim DB gate for safe cloud sync replacement, safe-copy upload. (3) **P1 Reliability**: Interlocked reentrancy guards on all 4 timer-driven services (CaptureService, ClipboardMonitor, ScreenshotService, PrivacyViewModel). Fixed clipboard/screenshot self-exclusion sequence consumption. LRU bitmap cache replacing per-call allocation. Settings-driven CaptureService (MaxBufferChars, IdleFlushSeconds). SyncOnClose wiring. Scheduler filtered by CanRunUnattended from DI. Headless --run-tweak CLI mode. (4) **P2 Polish**: ILogger injection in CaptureService/SettingsService catch blocks. Removed unused _selectedResult field. Fixed CS0169 warning. (5) **Build**: 0 warnings, 0 errors. Bumped ProtectedData 10.0.8, pinned Tmds.DBus.Protocol 0.93.0. No vulnerable packages. Installer elevated to admin. (6) **Repo hygiene**: .gitignore, git rm --cached for bin/obj/publish/secrets. 6-agent multi-point audit: 19/19 PASS. See HANDOFF.md, TODO.md, ROADMAP.md for full details.
