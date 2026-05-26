# Kapture — Feature Status & Development Plan

> Last updated: 2026-05-19

---

## Completed Features (all shipped & integrated)

### Core Vault (v1.0.0 → v1.0.10)
- [x] Keystroke capture with low-level Win32 keyboard hook
- [x] Clipboard change monitoring
- [x] Periodic screenshot capture
- [x] SQLite database with full-text search, tagging, pinning
- [x] Three-column Vault layout: sidebar → entry list → content reader
- [x] Entry type badges (KB/CB/SC), buffer fill bars, language detection
- [x] System tray with recording/paused states
- [x] Single-instance mutex enforcement

### Settings Infrastructure (v1.0.11)
- [x] `AppSettings` model (JSON-serializable)
- [x] `SettingsService` — load/save from `%LOCALAPPDATA%\Kapture\settings.json`
- [x] `SettingsWindow` — modal dialog accessible from nav bar gear icon
- [x] Registered in DI container

### Theme System (v1.0.12)
- [x] 6 themes: Dark (default), Light, Sunset, Dawn, Oceanic, Rose
- [x] `ThemeRegistry` + `ThemeDefinition` + `Colors.cs` — complete theme architecture
- [x] Dynamic switching via `Application.Current.RequestedThemeVariant`
- [x] Theme picker in Settings window
- [x] All custom colors defined as DynamicResource for proper theme switching

### Entry Size Indicator (v1.0.13)
- [x] Visual progress bar in entry list items (charCount / 5000)
- [x] Color gradient: green → yellow → red as buffer fills
- [x] Shown in entry list items

### Encryption at Rest (v1.0.14)
- [x] `EncryptionService` — AES-256-GCM encryption
- [x] Optional password protection in Settings
- [x] `PasswordDialog` for PIN/password entry on startup
- [x] DPAPI-level credential storage

### Auto-Cleanup Rules (v1.0.15)
- [x] `AutoCleanupEnabled`, `RetentionDays`, `ExcludePinned` settings
- [x] `ExpiryDialog` — retention period picker
- [x] Runs on app startup; periodic cleanup
- [x] Pinned entries exempted by default

### Quick Paste Hotkey (v1.0.16)
- [x] Global hotkey: Ctrl+Shift+V via `HotkeyService` (Win32 `RegisterHotKey`)
- [x] `QuickPasteWindow` — floating, borderless, always-on-top popup
- [x] Auto-focused search with real-time filtering
- [x] Enter/click pastes selected entry via clipboard
- [x] Escape/focus-loss dismisses

### Cloud Sync (v1.0.17 → v1.0.20)
- [x] `ICloudStorageProvider` interface
- [x] Google Drive implementation (`GoogleDriveProvider`) — OAuth2 PKCE via localhost redirect
- [x] `CloudSyncManager` — orchestrates upload/download of SQLite DB
- [x] `CloudTokenStore` — DPAPI-secured OAuth token storage
- [x] `OAuthHelper` — OAuth2 PKCE flow helper
- [x] Settings UI: provider connect/disconnect, sync now button
- [ ] OneDrive provider (not yet implemented)
- [ ] Dropbox provider (not yet implemented)
- [ ] Box provider (not yet implemented)

### System Tweaks Module (v1.0.21 → v1.0.24)
- [x] `ITweak` interface — `PreviewAsync → ExecuteAsync → UndoAsync` lifecycle
- [x] Foundation: `TweakRunner`, `UndoLog`, `RestorePointService`, `RegistryBackup`, `ShellNotifier`, `FileCleanupHelper`, `DriverParser`, `ElevationCheck`
- [x] 60+ tweaks across 8 categories: Visual (12), Cache (5), Storage (4), Services (8), Registry (3), Drivers (5), Security (10), Performance (1)
- [x] Safety metadata: Severity, Reversibility, RequiresElevation/Restart/ExplorerRestart/RestorePoint
- [x] Three-column Tweaks view: category sidebar (with undo history) → tweak list (severity badges) → detail/preview pane
- [x] `app.manifest` → `requireAdministrator`

### Services Browser (v1.0.24)
- [x] Full Windows service browser via `ServiceController` + WMI
- [x] Safety classification: Safe / Caution / Unsafe / Critical
- [x] Status dots, safety badges, filter by safety/status
- [x] Detail pane: description, start type, category, start/stop controls
- [x] Critical service warning banner

### Dashboard (v1.0.24)
- [x] Live system monitoring: CPU, RAM, Network (send/recv), Disk drives
- [x] `PerformanceCounter` + WMI for real-time data
- [x] Animated progress bars with inset-shadow tracks
- [x] System info card: CPU name, GPU, OS build, uptime
- [x] `IDisposable` for proper PerformanceCounter cleanup

### Profiles (v1.0.24)
- [x] 3 preset profiles: Gaming, Work, Battery Saver
- [x] Profile cards with decorative icons and color-matched glow
- [x] One-click apply: each profile runs a preset combination of tweaks

### Startup Analyzer (v1.0.25)
- [x] Scans registry Run keys + shell Startup folder
- [x] Boot time stats: last boot time, duration, post-boot delay
- [x] Enabled/disabled counts with toggle controls
- [x] Detail pane: command, location, publisher, type

### Scheduler (v1.0.25)
- [x] Create Windows Task Scheduler tasks for automated tweak execution
- [x] Schedule types: daily, weekly, on boot
- [x] Job list with status dots, run-now, delete controls
- [x] Detail pane: task name, tweak ID, next/last run times

### Privacy Dashboard (v1.0.26)
- [x] Privacy score calculation with color-coded banner
- [x] 30+ privacy toggles: Telemetry, Advertising, Cortana/Copilot, Location, Camera/Mic, App Permissions, Search, Diagnostics, Misc
- [x] Tabbed detail pane: Setting Detail (registry info, secure/revert) + Network Monitor
- [x] Live telemetry connection scanner (`netstat` + MS IP range + process name cross-reference)
- [x] `DispatcherTimer` lifecycle management (auto-stop on nav away / window close)

### Visual Consistency Pass (v1.0.27)
- [x] Unified style classes applied to all views: card, section-header, detail-label, detail-value, mono, danger, empty-state
- [x] Nav bar rewrite: nav-btn/nav-active conditional classes, icon+text layout
- [x] All hard-coded hex colors replaced with DynamicResource theme references
- [x] `DialogShadow` applied to QuickPaste window

### Visual Depth & UI Overhaul (v1.0.27)
- [x] Multi-layer shadow system: CardShadow, HeaderShadow, FooterShadow, NavPillShadow, InsetShadow, AccentGlow
- [x] panel-header / panel-footer / progress-track style classes across all 7 detail views
- [x] Ctrl+K spotlight-style search overlay (replaced always-visible TextBox)
- [x] Segoe MDL2 Assets icon system: SymbolFont resource, icon/icon-lg classes
- [x] Icons throughout: nav bar (8), stats bar (5), action buttons (5), type filters, empty states (7), profile cards (3), Dashboard gauges, delete dialog

---

## Future Development Ideas

### Near-term Polish
- [ ] Visual testing pass — run app and verify all shadow/icon/search changes render correctly
- [ ] Dialog/window chrome consistency — apply shadows and depth to SettingsWindow, ContentViewerWindow
- [ ] Toast notification animation — fade/slide in+out
- [ ] Typography pass — consistent font sizes and weights across all views

### Additional Cloud Providers
- [ ] OneDrive via Microsoft Graph + Azure.Identity
- [ ] Dropbox via Dropbox.Api (OAuth2 PKCE)
- [ ] Box via Box.V2 (OAuth2)

### Feature Ideas
- [ ] Export vault entries (JSON, CSV, plain text)
- [ ] Import from other clipboard managers
- [ ] Tweak profiles — user-defined tweak combinations (beyond the 3 presets)
- [ ] Scheduled privacy scans — periodic re-check of privacy settings
- [ ] Network monitor history — persist telemetry connection logs
- [ ] Tweak comparison — before/after benchmarks for performance tweaks

---

## Version Bumps

Each build increments version by 0.0.1. Current: **v1.0.27**.

## Build Order (historical)

Features 1-2 (settings + theme) → 3-5 (smaller features) → 6 (hotkey) → 7 (cloud sync) → System Tweaks module → Services/Dashboard/Profiles → Startup/Scheduler → Privacy → Visual polish passes.
