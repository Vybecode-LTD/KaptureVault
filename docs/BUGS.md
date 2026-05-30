---
document: BUGS
doc-version: 1.0.0
app-version: 1.0.2
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: codebase-audit
---

# BUGS.md — KaptureVault Issue Register

> Source: full multi-agent codebase audit on **2026-05-30** (architecture, data/security, performance, testing, correctness). 45 issues. **None fixed yet — all Open.** See `AUDIT-LOG.md` for methodology and `ROADMAP.md` for the prioritized fix order.

**Severity counts:** 🔴 Critical 4 · 🟠 High 13 · 🟡 Medium 16 · ⚪ Low 10 · 📄 Doc/Process 2

Status legend: `OPEN` · `IN PROGRESS` · `FIXED` · `WONTFIX`

---

## 🔴 Critical

### KV-001 · Live Google OAuth secrets exposed (git history + on disk), unrevoked
- **Area:** Security · **Status:** OPEN · **Owner:** requires human (Google Cloud Console)
- The desktop OAuth client secret was committed in the **parent** `Utilities` repo history (commits `97cb28c`/`db86dd3`/`e821089`, path `Kapture/client_secret_232322018793-…json`, secret `GOCSPX-…69t19o`) and is still reachable there. The parent repo has a real push remote (`github.com/Vybecode-LTD/Utilities`). Three more **live** secrets sit on disk and are bundled into installers: desktop `…iB5` (×2 projects) and **web** `…L9a-1` (kapture.tools). The KaptureVault fork itself is clean (gitignored, verified), but it shares the same client identity.
- **Fix:** (1) **Revoke + rotate all three secrets in Google Cloud Console** — most urgent, human-only. (2) `git filter-repo`/BFG the parent repo to purge the secret blob, force-push. (3) Confirm repo visibility (public = assume full compromise). (4) Reconfigure desktop client as native/PKCE-no-secret (see KV-007).

### KV-002 · Decryption silently returns ciphertext on auth-tag failure
- **Area:** Crypto · **Status:** OPEN · `Services/EncryptionService.cs:117-120`
- `catch { return ciphertext; }` swallows `AuthenticationTagMismatchException`. AES-GCM's integrity guarantee is discarded: a tampered/corrupted row, or a vault synced under a *different* key, surfaces in the UI as the literal `ENC:…` string instead of failing. Risk of double-encryption on re-save.
- **Fix:** Let auth failures propagate as a typed `DecryptionException`; distinguish "not our prefix → return as-is" (legit) from "our prefix but auth failed → error". Surface "wrong password / corrupted vault" to the user.

### KV-003 · Drive sync = last-write-wins whole-DB overwrite → multi-device data loss
- **Area:** Data/Sync · **Status:** OPEN · `Services/CloudSync/CloudSyncManager.cs:114-141`
- Conflict resolution compares file mtimes (±5s) then uploads or **wholesale-replaces** `vault.db`. No merge. Device A and Device B capturing independently → last sync clobbers the other's entire vault. The `.pre_sync_backup` only guards replace mechanics, not the logical merge.
- **Fix:** Per-entry delta sync (watermark/`synced` flag + `last_update_time`) merging rows by `id`/content-hash, or document loudly that sync is **single-device-only**. At minimum union DBs instead of clobbering and keep the pre-sync backup.

### KV-004 · Content search returns nothing when encryption is active
- **Area:** Search/Data · **Status:** OPEN · `Services/DatabaseService.cs:175`, `ViewModels/MainWindowViewModel.cs:158`
- `Search()` runs `content LIKE @q` against ciphertext when encryption is on → **zero rows** for any real content query (app/window/tags still match, masking it intermittently). No decrypt-then-filter fallback.
- **Fix:** When `IsActive`, fetch candidates (app/date/tags) and filter on decrypted `Content` in memory; or a keyed blind index. At minimum show a "search needs vault unlocked / limited while encrypted" notice.

---

## 🟠 High

### KV-005 · Self-exclusion broken — app captures its own keystrokes & clipboard
- **Area:** Correctness · **Status:** OPEN · `Services/CaptureService.cs:13`, `Services/ClipboardMonitorService.cs:13`
- Both define `SelfProcessName = "Kapture"` but the renamed process is **`KaptureVault`** (`ScreenshotService.cs:13` already uses the correct name). Self-exclusion at `CaptureService.cs:166,221` and `ClipboardMonitorService.cs:78` never matches → keystrokes typed into KaptureVault's own UI (tag box, search) get captured; app-set clipboard (Copy, Quick Paste) gets re-captured.
- **Fix:** Set both constants to `"KaptureVault"` — or derive from `Process.GetCurrentProcess().ProcessName` so a future rename can't reintroduce drift. **Trivial, highest-value functional fix — do first.**

### KV-006 · PBKDF2 100k iterations below 2026 guidance
- **Area:** Crypto · **Status:** OPEN · `Services/EncryptionService.cs:14,123-127`
- 100k PBKDF2-HMAC-SHA256 is ~6× under current OWASP (600k+); Argon2id is the modern recommendation. `vault.db` + `encryption.json` sit together in LocalAppData → a leaked pair is GPU-brute-forceable.
- **Fix:** Raise to ≥600k as a stopgap; migrate to Argon2id (e.g. `Konscious.Security.Cryptography`). Store KDF params in `encryption.json` so vaults upgrade per-user.

### KV-007 · OAuth client secret bundled in installer + hardcoded fallback
- **Area:** Security · **Status:** OPEN · `Services/CloudSync/GoogleDriveProvider.cs:14,81-86`, `installer/KaptureVaultSetup.iss:101-105`
- App uses PKCE but still *hard-requires* `_clientSecret`, ships `client_secret.json` into Program Files unprotected, and has a hardcoded `FallbackClientId`. A secret shipped to every user is not secret.
- **Fix:** Register a native/Desktop OAuth client with **no** secret + loopback PKCE; drop the hard gate, stop bundling the file, remove the fallback constant.

### KV-008 · `ThrowIfReplacing()` gate applied inconsistently → sync-swap races
- **Area:** Data · **Status:** OPEN · `Services/DatabaseService.cs:94` (+ ~11 ungated public methods)
- The gate guards `Insert/GetAll/Delete/PruneExpired/CreateBackupCopy` but is **absent** on `GetByApp/Search/UpdatePin/UpdateExpiry/PruneOlderThan/GetDistinctApps/GetStats/UpdateTags/GetDistinctTags/EncryptAllEntries/DecryptAllEntries`. `_dbGate` is only taken by the replace path. A sync download mid-`UpdatePin`/`Search` can throw or read a half-copied file.
- **Fix:** Route every public op through a shared `_dbGate.WaitAsync` vs. the exclusive replace path, or call `ThrowIfReplacing()` at the top of all public methods. Partial application is worse than none.

### KV-009 · Fragile positional/ordinal column mapping in `ReadEntries`
- **Area:** Data · **Status:** OPEN · `Services/DatabaseService.cs:402-418` (+ `SELECT *`)
- Hard-coded ordinals against `SELECT *`. A reordered `CREATE TABLE`, a mid-table column, or a DB **synced down from a different app version** shifts ordinals → silent decode corruption / `DateTime.Parse` throws.
- **Fix:** `SELECT` explicit named columns and use `reader.GetOrdinal("…")`.

### KV-010 · `HotkeyService` created outside DI, never disposed
- **Area:** Lifecycle · **Status:** OPEN · `App.axaml.cs:140-143`, `Services/HotkeyService.cs`
- `new HotkeyService()` owns a message-only HWND + background STA thread; only `Stop()` on the Quit path, never `Dispose()`, never touched on restart/cancel shutdowns → orphaned global hotkey registration.
- **Fix:** Register as a DI singleton; ensure teardown on every shutdown path (see KV-011).

### KV-011 · Service teardown only on tray-Quit path
- **Area:** Lifecycle · **Status:** OPEN · `App.axaml.cs:266-288`, `Views/SettingsWindow.axaml.cs:237,250,278`, `App.axaml.cs:84`
- `_capture/_clipboardMonitor/_screenshotService/_hotkeyService.Stop()`, tray disposal, and `SyncOnClose` live **only** in the Quit handler. The three `SettingsWindow` restart routes and the encryption-cancel `Shutdown()` bypass all of it → hooks/timers keep running, SyncOnClose silently skipped.
- **Fix:** Centralize teardown in `ShutdownRequested`/`OnExit`; dispose the provider there (KV-024); run sync-on-close once regardless of trigger.

### KV-012 · Synchronous WAL+AES SQLite INSERT on the keyboard-hook thread
- **Area:** Performance · **Status:** OPEN · `Services/CaptureService.cs:236` (← `OnChar`→`Flush` at `:112`), `KeyboardHookService.cs:46-54`
- When the buffer hits `MaxBufferSize` mid-typing, `Flush()` runs `Open()` (new connection + `PRAGMA journal_mode=WAL`) + INSERT + AES **inside the WH_KEYBOARD_LL callback**. Blocking that callback degrades system-wide input latency and risks hook eviction (`LowLevelHooksTimeout`).
- **Fix:** Hand flushed text to a bounded `Channel<CaptureEntry>`; run inserts on a dedicated writer task. Never do DB/crypto on the hook thread.

### KV-013 · Entry `ListBox` effectively non-virtualized
- **Area:** Performance · **Status:** OPEN · `Views/MainWindow.axaml:219-267`, `MainWindowViewModel.cs:176-178`, `DatabaseService.cs:156`
- (a) `GetAll()` has **no LIMIT** (the "2000" in older notes is not enforced) → entire table loaded. (b) `Entries.Clear()`+per-row `Add` on every flush tears down all realized containers. (c) Each row runs 4 converters, 3 of which `new SolidColorBrush(...)` per call.
- **Fix:** Add `LIMIT`/paging; apply the sidebar's diff-update pattern to `Entries`; return cached static brushes from converters.

### KV-014 · Annotation editor base `Bitmap` never disposed
- **Area:** Performance/Memory · **Status:** OPEN · `Views/Dialogs/ScreenshotEditorWindow.axaml.cs:68`
- `new Bitmap(_entry.Content)` is assigned to a transient `Image.Source`; the window has no `OnClosed`. Each open leaks ~33 MB (4K). Editor is non-modal (`.Show()`) so they accumulate.
- **Fix:** Store the bitmap in a field, dispose in `OnClosed` (mirror `ContentViewerWindow`).

### KV-015 · `SettingsWindow` code-behind holds business logic (MVVM inversion)
- **Area:** Architecture/MVVM · **Status:** OPEN · `Views/SettingsWindow.axaml.cs:64-185,203-279`
- Encryption config, bulk encrypt/decrypt, OAuth, sync execution, and the entire elevation/restart orchestration live in the View. `SettingsViewModel` only mirrors fields.
- **Fix:** Move logic into `SettingsViewModel` commands behind service interfaces; the window keeps only dialog plumbing and binds state.

### KV-016 · `ObservableCollection` mutated via background-thread event re-entry
- **Area:** Threading · **Status:** OPEN · `MainWindowViewModel.cs:89-92` vs `CaptureService.cs:237`/`ClipboardMonitorService.cs:101`/`ScreenshotService.cs:114`
- Currently safe because the VM handler re-posts to the UI thread, but `OnEntryFlushed` fires on hook/timer threads and the "subscribers must self-marshal" contract is undocumented & unenforced — one naive subscriber away from a cross-thread crash.
- **Fix:** Marshal once at the source (raise the event on the dispatcher) or document/enforce the contract.

### KV-017 · `MainWindow` PropertyChanged subscription never removed
- **Area:** Performance/Lifecycle · **Status:** OPEN · `Views/MainWindow.axaml.cs:18-23`
- `mainVm.PropertyChanged += …` with no `-=`. Not a live leak today (both singletons; window hides not destroys) but `Loaded` can double-fire and it's a latent leak the moment the window becomes recreatable.
- **Fix:** Unsubscribe in `OnDetachedFromVisualTree`, or guard against double-subscription.

---

## 🟡 Medium

### KV-018 · ScreenshotEditor SaveAs crashes (NaN→PixelSize) when source image missing
- **Area:** Correctness · **Status:** OPEN · `Views/Dialogs/ScreenshotEditorWindow.axaml.cs:64-85,483-487`
- `LoadImage()` early-returns if the file is missing, leaving `Canvas.Width/Height = NaN`; `SaveAs_Click` does `(int)NaN` → `int.MinValue` → `RenderTargetBitmap` throws on an `async void` handler. Missing files are realistic (deleted screenshots, synced-in entries without local images).
- **Fix:** Guard SaveAs (`double.IsNaN(Width)` / `Children.Count==0`); surface "screenshot file not found" from `LoadImage`.

### KV-019 · `KeyHash = SHA256(derived key)` is an offline password oracle
- **Area:** Crypto · **Status:** OPEN · `Services/EncryptionService.cs:33-38`
- Storing a fast unsalted hash of the actual AES key lets an attacker verify password guesses with one SHA-256 each (no AES needed), and leaks a deterministic function of the key.
- **Fix:** Verify via decrypting a stored known-plaintext verifier (GCM tag = check), or an HKDF-separated value — never `SHA256(key)`.

### KV-020 · Master key held as plain `byte[]`, never zeroed
- **Area:** Crypto · **Status:** OPEN · `Services/EncryptionService.cs:23,63`
- Key lives in a managed array for the session, exposed to dumps/swap; `Disable()` just nulls the reference.
- **Fix:** `CryptographicOperations.ZeroMemory` on disable/lock/shutdown; consider pinned/`ProtectedMemory`.

### KV-021 · No transaction around bulk re-encryption
- **Area:** Data · **Status:** OPEN · `Services/DatabaseService.cs:297-322,324-349`
- Per-row updates with no surrounding transaction → a crash mid-enable leaves a mixed plaintext/ciphertext table while the UI reports "encrypted."
- **Fix:** Wrap each bulk op in one `SqliteTransaction`; batch rather than materializing all rows first.

### KV-022 · WAL checkpoint not managed before DB copy/replace
- **Area:** Data · **Status:** OPEN · `DatabaseService.cs:391`, `CloudSyncManager.cs:159`
- WAL set per-connection; replace/backup copy the main file only. If a connection holds the WAL open, recent writes aren't in the copied file. (`VACUUM INTO` upload path is fine.)
- **Fix:** `PRAGMA wal_checkpoint(TRUNCATE)` before any copy/replace; ensure no other open connection.

### KV-023 · `RenderTargetBitmap` leaks on the exception path in SaveAs
- **Area:** Performance · **Status:** OPEN · `ScreenshotEditorWindow.axaml.cs:486-509`
- `rtb.Dispose()` isn't in a `finally`; any throw in render/encode/IO leaks ~33 MB.
- **Fix:** `using var rtb = …`.

### KV-024 · `ServiceProvider` never disposed
- **Area:** Lifecycle · **Status:** OPEN · `App.axaml.cs:18,53`
- Singleton `IDisposable` services (Capture/Clipboard/Screenshot/CloudSyncManager) never get `Dispose()`d.
- **Fix:** `(_serviceProvider as IDisposable)?.Dispose()` on the centralized shutdown (KV-011).

### KV-025 · Single-instance mutex released before relaunch confirmed
- **Area:** Lifecycle · **Status:** OPEN · `Program.cs:51-56`, `SettingsWindow.axaml.cs:225,264`
- `PrepareForRestart()` releases the mutex before `Process.Start`; a throw afterward (`catch { Close(); }`) leaves a protection-less instance running.
- **Fix:** Start the new process first, release only on success; the cancel path should `Shutdown()` not `Close()`. (See KV-036.)

### KV-026 · `MainWindowViewModel` couples to Avalonia UI types
- **Area:** Architecture · **Status:** OPEN · `MainWindowViewModel.cs:3-7,474,483-494`
- Reaches `Application.Current.ApplicationLifetime` for `TopLevel`/`IClipboard`, opens `StorageProvider`, uses `DispatcherTimer` — complicates headless testing.
- **Fix:** Inject `IClipboardService`/`IFileDialogService`/`IToastService`.

### KV-027 · `QuickPasteWindow` & `ContentViewerWindow` code-behind own data/query logic
- **Area:** Architecture/MVVM · **Status:** OPEN · `QuickPasteWindow.axaml.cs:58,69,108-126`, `ContentViewerWindow.axaml.cs:72-148`
- QuickPaste holds an `IDatabaseService` + its own collection + queries; ContentViewer imperatively populates ~8 named controls.
- **Fix:** Add `QuickPasteViewModel` / `ContentViewerViewModel`; bind instead of `FindControl`. (TextMate grammar wiring legitimately stays view-side.)

### KV-028 · `CloudSyncManager` async-void timer; status updated off UI thread
- **Area:** Threading · **Status:** OPEN · `CloudSyncManager.cs:51,170-174`
- `_syncTimer.Elapsed += async (_,_) => await SyncAsync()` can throw unobserved; `UpdateStatus` writes shared state from the timer thread (no UI subscriber today, but one away from a cross-thread bug).
- **Fix:** Guard the handler body in try/catch; marshal status if ever UI-bound.

### KV-029 · Drive download integrity not verified
- **Area:** Data · **Status:** OPEN · `GoogleDriveProvider.cs:228-245`
- Upload verifies md5; download streams to disk with no check. A truncated-but-valid SQLite file would pass `integrity_check` and replace the live vault.
- **Fix:** Verify the remote `md5Checksum` against downloaded bytes before replace.

### KV-030 · `WithRetryAsync` inconsistent retry / potential non-termination
- **Area:** Data · **Status:** OPEN · `GoogleDriveProvider.cs:377-395`
- Non-`DriveApiException` transport errors (`HttpRequestException`, `IOException`, timeouts) aren't retried at all; `EnsureTokenOrThrowAsync` inside the catch can bypass backoff.
- **Fix:** Treat transport/timeout as retryable; guarantee a terminal path.

### KV-031 · Sync errors swallowed to a status string
- **Area:** Data/Observability · **Status:** OPEN · `CloudSyncManager.cs:143-147`
- Every failure → `UpdateStatus("Sync error: …")`, no logging, no severity class. Auth-revoked looks like a blip.
- **Fix:** Log via Serilog, classify severity, surface auth-revoked distinctly.

### KV-032 · `Refresh()` rebuilds 4 collections per capture event, no debounce
- **Area:** Performance · **Status:** OPEN · `MainWindowViewModel.cs:99-105`
- `RefreshStats+RefreshAppList+RefreshTagList+RefreshEntries` on every flush; clipboard/screenshot pollers fire every 500 ms.
- **Fix:** Debounce behind a 250-500 ms `DispatcherTimer` that coalesces bursts.

### KV-033 · Per-call connection + WAL pragma; whole-table decrypt on UI thread
- **Area:** Performance · **Status:** OPEN · `DatabaseService.cs:386-394`, `MainWindowViewModel.cs:152-186`
- Each `Refresh` opens 4 connections (re-running the WAL pragma) and `GetAll` decrypts every row synchronously on the UI thread → multi-hundred-ms stalls on encrypted vaults, every capture event.
- **Fix:** Reuse a connection/pooling; move the read to a worker, marshal back the list; combine with KV-032 debounce.

---

## ⚪ Low

### KV-034 · Clipboard dedupe not updated on self-exclusion early return
`ClipboardMonitorService.cs:78-86` — `_lastClipboardText` stale after self-copy (latent; goes live once KV-005 is fixed). **Fix:** update `_lastClipboardText` before the self return.

### KV-035 · UAC-cancel leaves `CaptureAdminApps=true` if no settings file
`Program.cs:91-101,124-136` — `TrySaveSettings` no-ops when the file is missing; a failed revert write → UAC re-prompt loop every launch. **Fix:** create the file with the corrected value; log revert-write failures.

### KV-036 · `RestartElevated` releases mutex before UAC result known
`SettingsWindow.axaml.cs:222-257` — brief protection-less window; cancel path should not have released. **Fix:** start elevated process first, release on success only; don't release on 1223 cancel.

### KV-037 · `Enable/DisableEncryption_Click` lack try/catch in `async void`
`SettingsWindow.axaml.cs:64,80` — an exception in `EncryptAllEntries`/`DecryptAllEntries` crashes the process. **Fix:** wrap + report to status (folds into KV-015).

### KV-038 · Pen `StreamGeometry` rebuilt from full point list per pointer-move
`ScreenshotEditorWindow.axaml.cs:288,340-350` — O(n²) CPU/GC for long strokes (not a leak — correct redraw). **Fix:** incremental `LineTo` or `Polyline.Points` for the active stroke; full rebuild only on commit.

### KV-039 · Thumbnail LRU cache holds full-resolution bitmaps
`ViewModels/Converters.cs:147-206` — `new Bitmap(path)` at full res × 40 ≈ up to ~1.3 GB. **Fix:** `Bitmap.DecodeToWidth(stream, 320)` for the list path. *Biggest single memory lever.*

### KV-040 · Large `byte[]` allocations on the screenshot timer thread
`ScreenshotService.cs:100,162` — marshaled DIB + BMP buffer (~33 MB each) per capture. **Fix:** stream/pool buffers; acceptable priority.

### KV-041 · No FTS; `LIKE '%term%'` leading-wildcard full scans
`DatabaseService.cs:173-185` — O(n) content search (compounds with KV-004). **Fix:** FTS5 for unencrypted use; in-memory decrypt-filter for encrypted.

### KV-042 · `Encrypt` no-op when locked-but-configured
`EncryptionService.cs:72-73` + `DatabaseService.cs:140` — an insert while the vault is *locked* stores plaintext. **Fix:** verify the capture pipeline cannot insert while locked-but-configured; add a targeted test.

### KV-043 · `ViewLocator` is dead reflection code
`ViewLocator.cs:22-27` — `[RequiresUnreferencedCode]`, never hits its happy path (no `*View`/`*ViewModel` pairs), blocks trimming/AOT. **Fix:** remove, or back with a keyed DI lookup if VM-first nav is wanted.

---

## 📄 Doc / Process

### KV-044 · Local `CLAUDE.md` pervasively stale
**Status:** FIXED 2026-05-30 (this session). Described the pre-fork 8-tab Kapture (v1.0.27, `requireAdministrator`, mutex `…B7E3F4A2`, `SystemTweaks/`, `%LOCALAPPDATA%\Kapture`). Rewritten to vault-only reality (v1.0.2, `asInvoker`, mutex `…C9D2E5F6`, `%LOCALAPPDATA%\KaptureVault`).

### KV-045 · 0% automated test coverage
**Status:** OPEN — no test project, no test packages, no test CI job. See `TESTING.md` for the strategy and first-PR plan. Regression test for the KV-013-area filter fix is the priority.
