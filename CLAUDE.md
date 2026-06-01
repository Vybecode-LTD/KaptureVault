---
document: CLAUDE
version: 1.9.0
app-version: 1.0.7
last-updated: 2026-05-31
last-audit: 2026-05-31
managed-by: manual-reconciliation
---

# CLAUDE.md — KaptureVault

> **Living context document for Claude Code sessions. Read this first.**
> Start-of-session: read this file, then `docs/HANDOFF.md`, then run `dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj` to confirm a green baseline.

## Documentation Map (all docs are cross-linked)

| Doc | Purpose |
|---|---|
| `CLAUDE.md` (this) | Project constitution, architecture, standing directives, session log |
| `docs/HANDOFF.md` | **Canary** — current state + next steps; read at every session start |
| `docs/ROADMAP.md` | All to-do items (P1/P2/P3), prioritized, with status |
| `docs/BUGS.md` | Issue register KV-001…KV-045 with fix status + test refs |
| `docs/TESTING.md` | Test suite inventory, coverage, the testing directive |
| `docs/AUDIT-LOG.md` | Audit + reconciliation history |
| `CHANGELOG.md` | Versioned release history (+ Unreleased section) |

All managed docs share **one `version`** (currently **1.9.0**) and carry YAML frontmatter. App version (`1.0.7` (shipped 2026-06-01)) is tracked separately via `app-version`. *(There is also a non-managed design reference, `docs/F-02-online-vault-design.md`, with no shared version.)*

> **Note:** the paid **Online Vault backend (F-02)** lives in a separate **private** repo `kapturevault-backend` (`C:\dev\kapturevault-backend`, off OneDrive — github.com/Vybecode-LTD/kapturevault-backend), not in this client repo.

---

## Project Identity

**KaptureVault** is the **vault-only fork** of the original "Kapture" power tool. It captures **keystrokes, clipboard content, and screenshots** into a local SQLite vault with search, tagging, pinning, auto-expiry, optional AES-256-GCM encryption, optional Google Drive sync, a global Quick-Paste hotkey, and a screenshot annotation editor.

It does **NOT** contain the original app's system-tweak suite — there are no Tweaks/Services/Dashboard/Profiles/Startup/Scheduler/Privacy sections, no `SystemTweaks/`, and no `ITweak` infrastructure. It runs as a standard user (`asInvoker`), not admin.

- **Version:** 1.0.7 (`KaptureVault.csproj` `<Version>`; see `CHANGELOG.md`)
- **Repo root (= project root):** `C:\DEV\Utilities\KaptureVault` *(moved off OneDrive 2026-06-01; was `…\OneDrive\Documents\Development\Utilities\KaptureVault`)*
- **Remote:** `github.com/Vybecode-LTD/KaptureVault` (public) · **Site:** `kapture.tools`
- **Backend repo (F-02 paid Online Vault):** `github.com/Vybecode-LTD/kapturevault-backend` (private) at `C:\dev\kapturevault-backend` (off OneDrive)
- **Environment:** Windows 11, Claude Code

---

## Stack & Dependencies

| Layer | Technology |
|-------|-----------|
| **Runtime** | .NET 9 (`net9.0-windows`), C# 13, implicit usings, nullable enabled |
| **UI** | Avalonia 11.3.12 (FluentTheme, compiled bindings by default) |
| **MVVM** | CommunityToolkit.Mvvm 8.2.1 (`[ObservableProperty]`, `[RelayCommand]`) |
| **DI** | Microsoft.Extensions.DependencyInjection 9.0 |
| **Database** | Microsoft.Data.Sqlite 9.0 (`%LOCALAPPDATA%\KaptureVault\vault.db`, WAL) |
| **Logging** | Serilog 4.2 → File sink |
| **Editor** | AvaloniaEdit 11.4.1 + TextMateSharp.Grammars (content viewer syntax highlight) |
| **Imaging** | SkiaSharp 2.88.9 (screenshot encode + annotation export) |
| **Crypto** | AES-256-GCM + PBKDF2-SHA256; `System.Security.Cryptography.ProtectedData` 10.0.8 (DPAPI tokens) |
| **Cloud Sync** | Google Drive API, OAuth2 **PKCE** (loopback), scope `drive.file` |
| **Publish** | single-file self-contained `win-x64` |
| **Manifest** | `app.manifest` → identity `KaptureVault.Desktop`, **`asInvoker`**, longPathAware, PerMonitorV2 |
| **Single-instance** | mutex `Global\KaptureVault_SingleInstance_C9D2E5F6` (`Program.cs`) |

## Data locations (all under `%LOCALAPPDATA%\KaptureVault\`)
`vault.db` · `settings.json` · `encryption.json` (salt + key-hash) · `screenshots/*.bmp` · DPAPI-protected OAuth tokens · `client_secret.json` (gitignored, also bundled by installer)

---

## Architecture

**Single Vault window**, three columns: **Sidebar** (app list + tag list) → **Entry list** (type badges, buffer-fill bars, language detection) → **Content reader** (preview, tags, actions). Plus `SettingsWindow`, `QuickPasteWindow` (Ctrl+Shift+V), `ContentViewerWindow`, and dialogs: `DeleteConfirmDialog`, `ExpiryDialog`, `PasswordDialog`, `AboutDialog`, `ScreenshotEditorWindow` (annotation editor).

**Capture pipeline:** `KeyboardHookService` (WH_KEYBOARD_LL) → `CaptureService` (buffer + idle/window-change flush) → `DatabaseService.Insert`. `ClipboardMonitorService` and `ScreenshotService` run on `System.Timers.Timer` pollers (500 ms / interval) with `Interlocked` reentrancy guards. `ActiveWindowService` tracks the foreground app. All three capture services raise `OnEntryFlushed`; `MainWindowViewModel` re-posts to the UI thread via `Dispatcher.UIThread.Post(Refresh)`.

**Services** (`Services/`, all interface-backed, singleton in DI): ActiveWindow, Capture, ClipboardMonitor, Screenshot, Database, Settings, KeyboardHook, Hotkey, LanguageDetector, Encryption, Startup. **CloudSync/**: `ICloudStorageProvider` → `GoogleDriveProvider`, `CloudSyncManager`, `CloudTokenStore` (DPAPI), `OAuthHelper` (PKCE).

**Encryption:** AES-256-GCM, key via PBKDF2-SHA256 (16-byte salt). Stored format `ENC:base64(nonce[12] + tag[16] + cipher)`. Vault unlock gated at startup in `App.OnFrameworkInitializationCompleted`.

**Capture Admin Apps** (opt-in setting): relaunches elevated via `runas` so the low-level hook can see input from admin-level apps. Startup elevation check is in `Program.cs` (before the mutex); the restart/UAC-cancel logic is in `SettingsWindow.axaml.cs`.

---

## File Layout (vault-only — no SystemTweaks)

```
KaptureVault/
├── App.axaml(.cs)            App resources, DI registration, theme dictionaries, lifecycle
├── Program.cs                Entry point, elevation check, single-instance mutex
├── KaptureVault.csproj       v1.0.5, net9.0-windows, single-file publish
├── app.manifest              KaptureVault.Desktop, asInvoker
├── CLAUDE.md / CHANGELOG.md
├── Assets/                   app.ico (BMP frames), AppIcon.png, tray-*.png, installer-*.bmp
├── Models/                   AppSettings.cs, CaptureEntry.cs
├── Services/                 capture/db/settings/encryption/hooks/hotkey/language/startup
│   └── CloudSync/            GoogleDriveProvider, CloudSyncManager, CloudTokenStore, OAuthHelper
├── ViewModels/               MainWindowViewModel, SettingsViewModel, ExpiryDialogViewModel, Converters, ViewModelBase
├── Views/                    MainWindow, SettingsWindow, QuickPasteWindow, ContentViewerWindow
│   └── Dialogs/              DeleteConfirm, Expiry, Password, About, ScreenshotEditorWindow
├── Themes/                   AppTheme.axaml, Colors.cs, ThemeDefinition.cs, ThemeRegistry.cs (6 themes)
├── Helpers/                  TextEditorBindingHelper.cs
├── ViewLocator.cs            (currently dead reflection — see KV-043)
├── docs/                     HANDOFF/BUGS/ROADMAP/TESTING/AUDIT-LOG + site (index/privacy/tos/vault)
├── installer/                KaptureVaultSetup.iss (Inno Setup 6)
├── scripts/                  Invoke-Release.ps1
└── releases/latest/          current installer (committed; consumed by the website script)
```

**Themes:** Dark (default), Light, Sunset, Dawn, Oceanic, Rose.

---

## Coding Conventions

- C# 13, file-scoped namespaces, collection expressions `[]`, `nameof`, `ArgumentNullException.ThrowIfNull`
- One public type per file; records for DTOs, classes for services/VMs
- **MVVM strictly** — Views know VMs, never the reverse. (Several windows currently violate this — KV-015/027.)
- DI via constructor injection; avoid `App.Services.GetRequiredService` as a service locator (currently overused in Views — KV-015)
- No `async void` except event handlers; wrap their bodies in try/catch
- Per project constitution: ruff/dotnet-format clean, test-first for bug fixes (write the failing test first)

## Avalonia gotchas

- **`BoxShadow` is Border-only** (AVLN2000 on Button/ListBoxItem).
- **`ListBox.Clear()` + two-way bound `SelectedItem`** posts a *deferred* `SelectedItem = null` back to the property → wipes filters. **Diff-update collections; never `Clear()` a list whose selection is bound.** (Applied to AppList/TagList; `Entries` still needs it — KV-013.)
- **Compiled bindings on** by default — set `x:DataType`; pure code-behind windows (e.g. ScreenshotEditor) omit it.
- `Avalonia.Controls.Shapes.Path` has `StrokeJoin`, not `StrokeLineJoin`; ambiguous with `System.IO.Path` (fully-qualify).
- Pen strokes: reassigning `Path.Data` (new geometry) forces redraw; mutating `Polyline.Points` in place does **not**.
- Dispose `Bitmap`/`RenderTargetBitmap` deterministically in `OnClosed` (several leaks open — KV-014/023/039).

---

## STANDING DIRECTIVES — binding, apply every session

> Full authoritative versions live at the parent level and are `@include`d by the parent constitution:
> `../../DEBUG_PROTOCOL.md`, `../../TESTING_PROCEDURES.md`, `../../DOCUMENTATION_MANAGER.md`, and `../../SOFTWARE_RELEASE.md` (release pipeline — KaptureVault **is** a desktop download app, so `ONLY_IF_DESKTOP_DOWNLOAD_APP` is true here). The summaries below are the working contract; read the source files when a situation needs the detail.

### 🧪 Testing directive
- **Tests are part of the implementation, not follow-up.** Every bug fix gets a regression test that **fails before the fix and passes after** (proven RED→GREEN). If a test is genuinely impossible, document the exact reason.
- New source file → create its test; new public method/endpoint → test it.
- **Test stack:** `KaptureVault.Tests` (xUnit + NSubstitute + FluentAssertions + coverlet) on `KaptureVault.slnx`. Persistence seams exist: base-dir (`EncryptionService`), connection-string (`DatabaseService`). Never touch the real `%LOCALAPPDATA%\KaptureVault` vault in tests — use temp dirs / shared in-memory SQLite.
- **Before declaring work done, run the relevant checks and report results (evidence ledger):**
  `dotnet build` · `dotnet build -c Release` · `dotnet test --collect:"XPlat Code Coverage"` · `dotnet format --verify-no-changes` · `dotnet list package --vulnerable --include-transitive` · `dotnet publish -c Release -r win-x64` (for deliverables).
- **Known test gaps (P1/T-16):** no `Avalonia.Headless.XUnit` UI smoke tests yet; no CI test job; `dotnet format` / vulnerable-scan not yet wired into the loop. Add these.

### 🐞 Debugging directive (anti-loop circuit breaker)
- **2-strike rule:** if the same bug survives **two** fix attempts (user says "still broken" twice), STOP blind fixing and enter **DIAGNOSTIC MODE** — freeze production-code edits, restate assumptions, read the whole error, **reproduce**, form 3 competing hypotheses, explain why each prior fix failed, instrument + gather evidence, then propose ONE evidence-backed fix and **verify with proof (verbatim command output), not assertion.**
- **`BREAKLOOP`** (or "enter debug protocol") forces DIAGNOSTIC MODE immediately.
- Standing rules: run the code rather than guess; fix root causes, not symptoms; read whole stack traces; never fabricate results; don't mirror the user's guessed cause if evidence disagrees; stop and ask when genuinely ambiguous.
- When the user has to manually break a loop, append a one-line guard to **Lessons** below.

### 📚 Documentation directive
- **Update docs at the point of change, not later.** Fix a bug → update `BUGS.md` in the same step; complete a task → mark `ROADMAP.md`; add a dep → update `TESTING.md` + this file's Stack.
- All managed docs share **one `version`**; bump together (MINOR for session work that adds content). Keep frontmatter accurate.
- **Every session ends with a handoff** (`perform handoff`): reconcile all docs vs. code, update `HANDOFF.md` (the canary) + `CLAUDE.md`, log the audit in `AUDIT-LOG.md`. If the user forgets, remind them.
- Reconciliation (`perform audit`) at session boundaries / before deploy: ROADMAP↔code, BUGS↔code, TESTING↔suite, CLAUDE↔reality, HANDOFF↔state, CHANGELOG↔versions, cross-refs resolve.

### 🚀 Release directive ("release it")
Three stages, no race conditions, no manual steps — the local script makes the artifact, the cloud workflow creates the release, the website reads from GitHub.

When the user says **"release it"**:
1. Add a new top entry to `CHANGELOG.md` for the new version (user-facing summary; promote the `[Unreleased]` items). **Each bullet MUST lead with a `**bold sentence**`** (the problem/what), then plain text (the fix) — the kapture.tools changelog parser uses the bold lead as a column; a plain bullet renders with an empty middle column. Match the 1.0.2/1.0.3 entry style.
2. Pre-flight: kill any running `KaptureVault.exe` (locks build output); confirm `dotnet test` is green.
3. Run `powershell -ExecutionPolicy Bypass -File scripts\Invoke-Release.ps1 -BumpType minor` (**minor = +0.0.1**, **major = +0.1.0**). The script bumps the version in `.csproj` **and** `installer/.iss`, publishes, builds the Inno Setup installer, copies it to `releases/latest/`, and commits (incl. CHANGELOG) + tags `vX.Y.Z` + **pushes**. It does **NOT** create the GitHub release.

Then it's automatic:
- **`.github/workflows/auto-release.yml`** (the **single** release creator) triggers on the pushed `releases/latest/*.exe` → VirusTotal-scans the installer → creates the GitHub Release (with the VT link in the notes), ~30 s later.
- The **kapture.tools** website is a passive consumer: `download.js` reads the latest release (button URL, version, size, VT badge) and `changelog.js` reads `CHANGELOG.md` — both live from GitHub, 5-min cache. Nothing is pushed to the site.

⚠️ **Never re-add `gh release create` to the script** — it would race the workflow, pre-empt the VirusTotal scan, and produce minimal notes. Stable download URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.

---

## Build / Run / Release

```powershell
dotnet build -c Debug                  # clean = 0 warnings / 0 errors
.\bin\Debug\net9.0-windows\win-x64\KaptureVault.exe

# Full release: bump → publish → Inno Setup → releases/latest → commit+tag+push → gh release
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Release.ps1 -BumpType minor   # or major
```

**Kill any running `KaptureVault.exe` before building** — it hides to tray and locks the output (MSB3027). May be running *elevated* (Capture Admin Apps) → needs an elevated kill. Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`.

When the user says **"release it"**: add a new top entry to `CHANGELOG.md`, then run `Invoke-Release.ps1` (minor = +0.0.1, major = +0.1.0).

---

## Health & Known Issues

A full audit (2026-05-30) catalogued **45 issues** in `docs/BUGS.md`. **All P0 (Critical) items are fixed and shipped in v1.0.3:**
- ✅ KV-005 self-exclusion (no longer captures own input); KV-002 decrypt integrity (throws on tamper); KV-004 search works under encryption; KV-003 mitigated (pre-sync backup retained).
- ✅ KV-001 — all OAuth secrets rotated; committed secret purged from `Utilities` git history (verified clean).
- ✅ Test suite live (`KaptureVault.Tests`, 10 tests) — was KV-045.

**P1 shipped in v1.0.4:** ✅ KV-008, KV-009, KV-014/023/018, KV-013 (partial).
**P1 shipped in v1.0.5 (released 2026-05-31):** ✅ KV-012 (DB writes off the keyboard-hook thread → bounded `Channel` + writer task; T-07), ✅ KV-006 (PBKDF2 600k + persisted KDF params, legacy vaults still open; T-11), ✅ KV-010 (HotkeyService + MainWindowViewModel resolved from DI; T-10 — disposal-on-all-shutdown-paths half completed by T-08, 2026-06-01).
**Dev-infra debt CLOSED + CI-GATED:** the pre-existing `dotnet format` whitespace debt is fixed (app `51dc9fd` + test project `12b7122`, both clean) and `.github/workflows/tests.yml` now runs `dotnet format --verify-no-changes` + `dotnet list package --vulnerable` on push/PR to `main` (verified green on GitHub, Actions run 26725669973). This completes the **format/vuln half of T-16/KV-045**. Tests **30 → 47**; no vulnerable packages.
**F-01 (Export Vault Database) — ✅ RELEASED in v1.0.6 (2026-06-01):** an "Export DB" toolbar button → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy` off the UI thread; regression covered by `DatabaseServiceBackupTests`. Released via `Invoke-Release.ps1` → `auto-release.yml` (GitHub Release v1.0.6, commit `aee32b5`).
**F-02 Phase 1 BACKEND BUILT (separate repo):** a new **private** repo `kapturevault-backend` (`C:\dev\kapturevault-backend`, off OneDrive — github.com/Vybecode-LTD/kapturevault-backend) holds a Cloudflare Worker (TypeScript): Google-token verify + first-party session JWT, Stripe billing + webhook→D1 state machine, presigned R2 URLs scoped to `users/{uid}/`, entitlement gate, D1 schema. **19 vitest tests + tsc clean + GitHub CI green** (commits 4758a50 foundation + 8795110 router).
**KV-007/T-12 — secret-less model DELIVERED for the Online Vault (F-02 Phase 2, 2026-06-01):** `LoopbackGoogleSignIn` → backend `POST /auth/google` broker (the Worker holds `GOOGLE_CLIENT_SECRET`; the client holds only the public client id + PKCE). **Residual:** `GoogleDriveProvider` still bundles `client_secret.json` for Drive sync → ROADMAP **T-35** routes Drive through the broker too to close KV-007 fully.
**P1 — ✅ COMPLETE (final batch on `main` 2026-06-01, unreleased — ships v1.0.7):** **T-16** (`Avalonia.Headless.XUnit` harness + headless MainWindow smoke + VM filter/diff regressions; suite **71**), **T-09** (Entries diff-update via `SyncEntries` + debounced `RequestRefresh` + off-UI-thread query/decrypt; `CaptureEntry` observable — KV-013/032/033), **T-08** (centralized idempotent teardown via `ShutdownRequested` + `ShutdownCoordinator`; ServiceProvider disposed on every exit — KV-011/024). **T-12/KV-007 folds into F-02 Phase 2 (backend broker).** **Next:** **F-02 Phase 2** (client `R2StorageProvider` + Google login + entitlement gate) and/or the **P2 backlog**; cut **v1.0.7** to ship the P1 batch whenever desired. See `docs/ROADMAP.md` + `docs/HANDOFF.md` to pick up.
**F-02 Phase 2 — ✅ CLIENT BUILT (2026-06-01, local/unpushed):** secret-less Google sign-in via the backend `/auth/google` broker, `KaptureOnlineApiClient`, `OnlineAccountService` (DPAPI session + auto-refresh + cached entitlement), `R2StorageProvider : ICloudStorageProvider` (encrypted vault ↔ R2 over presigned URLs + `vault.db.meta`), DI into `CloudSyncManager`, `OnlineAccountViewModel` + the Settings "Online Vault" panel. Backend gained `POST /auth/google` + `PUT /vault/meta` (vitest 19→26). Client suite **71 → 118**, Debug+Release 0/0, format clean. **Inactive until cloud accounts provisioned + `OnlineVaultConfig`/Worker secrets filled** (panel shows "not available in this build yet"). Next: go live, then F-02 Phase 3 (file hosting) / the P2 backlog.

---

## Lessons (self-maintaining — append a guard each time a loop is broken)

- **Avalonia `ListBox.Clear()` + two-way bound `SelectedItem`** posts a *deferred* `SelectedItem = null` back to the property → silently wipes the bound filter. **Diff-update collections; never `Clear()` a list whose selection is bound.** (Root cause of two filter-loses-selection bugs; fixed for AppList/TagList. `Entries` still uses Clear()+rebuild — KV-013.)
- **Never do DB/crypto work on the WH_KEYBOARD_LL hook thread** — it degrades system input latency and risks hook eviction. (KV-012 **FIXED** 2026-05-31, T-07: `CaptureService.Flush()` `TryWrite`s to a bounded `Channel<CaptureEntry>`; a writer task does the INSERT off-thread; `Stop()` drains it. Pattern to keep: never block the hook callback — hand work to a channel/writer.)
- **Bumping a KDF iteration count silently locks out every existing vault** unless the old count is stored — the derived key changes, so the key-hash check fails. **Persist KDF params** (`Iterations`, `Kdf`) and derive with the stored value; default missing = the old count (100k). (KV-006/T-11.)
- **A drained `Channel` writer can run inline on the thread that calls `Stop().Wait()`** (TPL inlining), so a "did the insert run on a different thread?" test is flaky — assert the *non-blocking behaviour* (a slow/gated write must not block the producer) instead.
- **Docs can silently drift from reality** — this session a "format-clean" claim (code had ~130 whitespace failures) and an "unpushed" claim (already pushed) were both false. Verify doc claims against the build/`git ls-remote`, not prior summaries (DEBUG_PROTOCOL "proof, not assertion" applied to our own docs).
- **⚠️ TOOLING / ONEDRIVE HAZARD (2026-05-31; ✅ RESOLVED 2026-06-01 — repo moved to `C:\DEV\Utilities\KaptureVault`):** with the repo on a OneDrive path, the agent harness returned **stale / delayed / fabricated / empty** tool output — twice reporting commits, files, and a `git push` that never happened. **Verify every consequential action (esp. git) with a second authoritative command** (`git log` / `git status` / `Test-Path`) before relying on it — DEBUG_PROTOCOL's "proof, not assertion," applied to the tooling itself. `Write` (full-file) is safe blind/repeatable; `Edit` is safe-on-failure (errors, never corrupts); run ONE PowerShell per turn (a non-zero exit cancels parallel siblings); **`Edit` needs an in-session `Read` of the file first.** Consider moving the repo off OneDrive. (F-02 backend repo `kapturevault-backend` was deliberately placed at `C:\dev\` off OneDrive for this reason.)
- **Self-exclusion / process identity:** derive from `Process.GetCurrentProcess().ProcessName`, never hardcode the app name (a rename silently broke self-exclusion — KV-005).
- **AES-GCM decrypt must throw on auth failure**, never return ciphertext as plaintext (silent swallow defeated integrity — KV-002).
- **PowerShell 5.1 mangles non-ASCII** in scripts/here-strings — keep `.ps1` ASCII only.
- **Bash tool eats backslashes** in args (`-o publish\win-x64` → `publishwin-x64`) — use forward slashes or PowerShell for paths.
- **Running app locks the build output** (single-instance, hides to tray; may be elevated) — kill `KaptureVault.exe` before build/publish.
- **OneDrive + `.git`** is risky — let OneDrive settle before git history ops; do history rewrites on a mirror clone in a non-OneDrive temp dir.
- **CHANGELOG entries need a `**bold lead**`** — the kapture.tools `changelog.js` parser treats the bold opening of each bullet as the "what/problem" column and the rest as the "fix" column. A plain bullet (no bold) renders with an empty middle column. Always write `- **Problem statement.** How it was fixed.`

## Session Log

- **≤2026-05-26:** Pre-fork "Kapture" full app (8 tabs, 60+ system tweaks). *(History; not in this fork.)*
- **2026-05-27 → 05-29 (v1.0.0 → v1.0.1):** Forked to vault-only. KV branding/icon, Google Drive sync fix, TOS/Privacy pages, interactive installer + uninstaller data removal, error-740 fix (`asInvoker`), Capture Admin Apps, About dialog, screenshot save-as-image + annotation editor, BMP installer icon, `kapture.tools` wiring, release automation.
- **2026-05-30 (v1.0.2):** App/tag sidebar filter selection fix (diff-update); mobile vault viewer web app (`/vault/`); CHANGELOG + v1.0.2 release; **full codebase audit** → created `docs/` set (BUGS/ROADMAP/TESTING/AUDIT-LOG/HANDOFF) and rewrote this file.
- **2026-05-30 (v1.0.3):** **P0 remediation, test-first** — self-exclusion (KV-005/034), decrypt integrity (KV-002), encrypted search (KV-004), Drive pre-sync backup retention (KV-003). Stood up `KaptureVault.Tests` (10 tests) + `KaptureVault.slnx` with persistence seams. **Security:** rotated all Google OAuth secrets, new desktop client ID `…15r8pqq8…`, updated `FallbackClientId`; purged the committed secret from `Utilities` git history (filter-repo) and verified clean. Released v1.0.3.
- **2026-05-30 (P1 → v1.0.4):** Hardening — named-column DB reads (KV-009), annotation editor bitmap/RTB disposal + SaveAs guard (KV-014/023/018), consistent `ThrowIfReplacing()` gate (KV-008), cached row brushes + 1000-row entry cap (KV-013 partial). Tests 10 → **30**. **Release pipeline:** removed `gh release create` from the script — `auto-release.yml` is now the single release creator (VirusTotal scan + GitHub Release with the version's CHANGELOG section sliced into the notes). **Released v1.0.4** (workflow-created, verified). Docs reconciled to `version` 1.3.0.
- **2026-05-30 (incident + feature planning):** Investigated an "AlfaFF network filter offline" report → **verified it is NOT KaptureVault** (zero network/WFP/driver code; `asInvoker`); root cause was third-party PCM surveillance software. No code change. Added the **Feature Roadmap** (F-01 local DB export; F-02 paid Online Vault — R2 + Workers + D1 + Stripe). Re-did the handoff to lead with F-01/F-02; docs bumped to `version` 1.4.0.
- **2026-05-31 (P1 batch 2 + F-02 design):** Test-first, each its own commit (tests **30 → 47**, build 0/0): **T-07/KV-012** — SQLite INSERT off the keyboard-hook thread (bounded `Channel` + writer task; `e5977dd`); **T-11/KV-006** — PBKDF2 → 600k + persisted KDF params, legacy vaults still open (`5748f9f`); **T-10/KV-010** — `HotkeyService` + `MainWindowViewModel` resolved from DI via new `ServiceRegistration.cs` (`0351500`); **T-16 partial/KV-045** — `.github/workflows/tests.yml` CI test job (`24cd3f2`). Wrote the full **F-02 design** (`docs/F-02-online-vault-design.md`, `b57b22d`). Reconciled all managed docs to `version` 1.5.0. **Resequenced:** T-08 (teardown) + T-09 (Entries diff-update) deferred to pair after T-16's headless harness (lifecycle/UI, high regression risk, need verifiability). **Pinned decision: next release is v1.0.5** with the T-07/T-11/T-10 batch (CHANGELOG `[Unreleased]` staged).
- **2026-05-31 (v1.0.5 release + CI hardening):** Released **v1.0.5** (P1 batch 2: T-07 hook-thread DB writes, T-11 PBKDF2 600k, T-10 DI) via Invoke-Release.ps1 → auto-release.yml. Closed pre-existing `dotnet format` whitespace debt (app `51dc9fd` + tests `12b7122`) and **wired `dotnet format --verify` + `dotnet list --vulnerable` gates into `tests.yml` CI** (verified green, run 26725669973) — the format/vuln half of T-16. **KV-007/T-12 decision:** defer secret-less OAuth to F-02 Phase 1 (backend broker). Corrected false doc drift caught this session (the prior "~12 commits unpushed" and "format-clean" claims were both wrong — OneDrive tooling-hazard artifacts). HEAD `d92952d`.
- **2026-05-31 (F-01 + F-02 Phase 1 backend):** Shipped **F-01** to `main` (Export Vault Database button + `DatabaseServiceBackupTests`, `ddc3ce4`; tests 47→49; staged in CHANGELOG `[Unreleased]` for v1.0.6). Built **F-02 Phase 1** in a new private repo **kapturevault-backend** (`C:\dev\kapturevault-backend`, off OneDrive — github.com/Vybecode-LTD/kapturevault-backend): Cloudflare Worker — Google-auth sessions, Stripe billing + webhook→D1, presigned R2 URLs scoped to `users/{uid}/`, entitlement gate; 19 tests + tsc + CI green. **KV-007/T-12 now retires via that backend broker** (Phase 2 client work). Next: F-02 Phase 2 (client `R2StorageProvider` + login + gate), or cut v1.0.6 to ship F-01.
- **2026-06-01 (repo relocated off OneDrive):** Moved the client repo from `…\OneDrive\Documents\Development\Utilities\KaptureVault` to **`C:\DEV\Utilities\KaptureVault`** — robocopy preserving `.git` + the gitignored secrets, excluding regenerable `bin/obj/publish`; verified at the destination *before* deleting the source (`git fsck` clean, HEAD `dc615ce` = origin/main, `dotnet test` **49/49** at the new path), then removed the OneDrive copy. Retires the OneDrive tooling hazard; the client now sits beside `kapturevault-backend` under `C:\DEV`. Updated repo-path references (CLAUDE/HANDOFF/ROADMAP/AUDIT-LOG + project memory). **Future sessions: launch Claude Code from `C:\DEV\Utilities`.** Next: cut v1.0.6 (F-01) → P1 backlog T-16 → T-09 → T-08.
- **2026-06-01 (v1.0.6 release + P1 backlog complete):** Cut **v1.0.6** (ships F-01 Export Vault Database) from the new `C:\DEV` location via `Invoke-Release.ps1` → `auto-release.yml` (GitHub Release verified, release commit `aee32b5`). Then completed the **final P1 batch**, test-first, each its own commit: **T-16** (`ff78e6d` — Avalonia.Headless.XUnit harness `TestAppBuilder` + `MainWindowSmokeTests` + VM filter/diff regressions), **T-09** (`53f0ad4` — Entries diff-update `SyncEntries` + debounced `RequestRefresh` + off-UI-thread decrypt; `CaptureEntry` observable), **T-08** (`bf3658c` — centralized teardown via `ShutdownCoordinator`/`ShutdownRequested` + ServiceProvider disposal). Tests **49 → 71**, Release build 0/0, `dotnet format` clean, pushed (HEAD `bf3658c` = origin/main). **All P1 audit issues resolved.** Reconciled all managed docs to **v1.8.0**. Next: F-02 Phase 2 and/or cut v1.0.7.
- **2026-06-01 (F-02 Phase 2 client built — local/unpushed):** Implemented the client Online Vault, test-first, each slice its own commit (`6ad70e5`..`9bd7369`) + backend `9a969d9`: `KaptureOnlineApiClient` (typed Worker contract), `OnlineAccountService` (DPAPI session via `CloudTokenStore` "online" + auto-refresh on near-expiry/401 + cached entitlement from `/me`), `LoopbackGoogleSignIn` (loopback PKCE → auth code), `R2StorageProvider : ICloudStorageProvider` (encrypted vault ↔ R2 over presigned URLs + `vault.db.meta`), DI wiring (`CloudSyncManager` takes `IEnumerable<ICloudStorageProvider>`), `OnlineAccountViewModel` + the Settings "Online Vault" panel. Backend (`kapturevault-backend`) gained `POST /auth/google` (secret-less PKCE broker holding the new `GOOGLE_CLIENT_SECRET`) + `PUT /vault/meta` (vitest **19 → 26**). Client tests **71 → 118**; Debug+Release 0/0; `dotnet format` clean. **Route B (backend broker) chosen** for secret-less sign-in (identical one-click UX, one extra deploy-time Worker secret). **KV-007:** delivered for the Online Vault; Drive bundling residual = new **T-35**. **Inactive until cloud accounts provisioned + `OnlineVaultConfig`/Worker secrets filled.** Reconciled managed docs to **v1.10.0**. **Commits NOT pushed.** Next: go live, then F-02 Phase 3 / the P2 backlog.
