---
document: BUGS
version: 1.6.0
app-version: 1.0.5
last-updated: 2026-05-31
last-audit: 2026-05-31
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/ROADMAP.md, docs/TESTING.md, docs/HANDOFF.md, docs/AUDIT-LOG.md]
---

# BUGS.md — KaptureVault Issue Register

> Source: full multi-agent codebase audit on **2026-05-30** (architecture, data/security, performance, testing, correctness). 45 issues. See `AUDIT-LOG.md` for methodology and `ROADMAP.md` for the prioritized fix order.
>
> **Remediation progress (2026-05-31):**
> - **P0 (shipped v1.0.3):** ✅ KV-001 (secrets rotated + history purged, verified), KV-005/034, KV-002, KV-004. 🟡 KV-003 mitigated.
> - **P1 (shipped in v1.0.4):** ✅ KV-008, KV-009, KV-014, KV-023, KV-018. 🟡 KV-013 partial (brush caching + 1000-row cap; Entries diff-update remains).
> - **P1 — SHIPPED in v1.0.5 (2026-05-31):** ✅ KV-012, ✅ KV-006, 🟡 KV-010 (DI done; disposal still needs T-08).
> - **Tests:** 10 → **47** passing. CI (`tests.yml`) now runs `dotnet test` + `dotnet format --verify-no-changes` + `dotnet list package --vulnerable`, verified green (run 26725669973). 🟡 KV-045: headless (Avalonia.Headless.XUnit) smoke tests + a MainWindowViewModel filter-selection regression test still pending.
> - **Next:** KV-013 remainder + KV-032/033 (T-09), KV-011/024 (shutdown teardown, T-08), KV-007 (secret-less OAuth, T-12); KV-015 View-locator cleanup folded into T-22.

**Severity counts:** 🔴 Critical 4 · 🟠 High 13 · 🟡 Medium 16 · ⚪ Low 10 · 📄 Doc/Process 2

Status legend: `OPEN` · `IN PROGRESS` · `FIXED` · `WONTFIX`

---

## 🔴 Critical

### KV-001 · Live Google OAuth secrets exposed (git history + on disk)
- **Area:** Security · **Status:** ✅ RESOLVED (2026-05-30)
- The desktop OAuth secret was committed in the parent `Utilities` repo history (`Kapture/client_secret_…json`) plus more live secrets on disk/in installers.
- **Resolved:** (1) all three secrets **rotated** in Google Cloud Console (desktop client deleted+recreated → new ID `…15r8pqq8…`; web secret rotated). (2) New creds written to the gitignored `client_secret.json` / `kaptureweb_clientsecret.json` + `%LOCALAPPDATA%` runtime copy; stale duplicate deleted; `FallbackClientId` updated to the new (public) client ID. (3) `git filter-repo` purged the secret file + scrubbed all old `GOCSPX-…` values from `Utilities` history; force-pushed; local repo realigned + pruned; release tag restored on the cleaned commit. **Verified clean** via fresh remote clone (no secret file, no full secrets). Parent repo is PRIVATE.
- **Still open (separate task):** **KV-007 / T-12** — the *new* secret is still bundled in the installer. Reconfigure the desktop client as native/loopback-PKCE with **no secret** before wide distribution. User should also confirm the old web secret is deleted in the console.

### KV-002 · Decryption silently returns ciphertext on auth-tag failure
- **Area:** Crypto · **Status:** ✅ FIXED (2026-05-30) · `Services/EncryptionService.cs`, `Services/DatabaseService.cs`
- `catch { return ciphertext; }` swallowed `AuthenticationTagMismatchException`, discarding AES-GCM's integrity guarantee.
- **Fix applied:** `Decrypt` now throws a typed `DecryptionException` on malformed/truncated/tampered/wrong-key content (still returns non-`ENC:` input and locked-vault as-is). Callers handle it gracefully: `ReadEntries` substitutes a per-row "[Unable to decrypt …]" placeholder (no silent ciphertext, no list crash); `DecryptAllEntries` skips un-decryptable rows instead of aborting the disable. Also added a **base-directory seam** to `EncryptionService` so tests never touch the real `encryption.json` (progress on KV-045). Tests: `EncryptionServiceTests` (round-trip, tamper→throw, wrong-key→throw, non-encrypted passthrough). RED→GREEN verified.

### KV-003 · Drive sync = last-write-wins whole-DB overwrite → multi-device data loss
- **Area:** Data/Sync · **Status:** 🟡 MITIGATED (2026-05-30); full fix deferred · `Services/DatabaseService.cs`, `Services/CloudSync/CloudSyncManager.cs`
- Conflict resolution compares file mtimes (±5s) then uploads or **wholesale-replaces** `vault.db`. No merge → independent captures on two devices → last sync clobbers the other's vault.
- **Mitigation applied:** `ReplaceDatabaseFromAsync` now **retains** the `<db>.pre_sync_backup` (it was deleted on success), so a clobbering sync-down always leaves a recoverable copy of the pre-sync local state. Test: `DatabaseServiceReplaceTests.ReplaceDatabaseFromAsync_RetainsPreSyncBackupWithLocalData` (RED→GREEN).
- **Still open (larger task, ROADMAP T-06):** real per-entry delta merge (watermark/`synced` flag) instead of whole-file replace; download md5 verification (KV-029); a prominent **single-device-only** warning in the sync UI. Until then, multi-device use can still lose data between syncs — recoverable only from the retained backup.

### KV-004 · Content search returns nothing when encryption is active
- **Area:** Search/Data · **Status:** ✅ FIXED (2026-05-30) · `Services/DatabaseService.cs`
- `Search()` ran `content LIKE @q` against ciphertext when encryption was on → zero rows for any real content query.
- **Fix applied:** when `_encryption.IsActive`, `Search` now fetches candidates (`GetAll`/`GetByApp`, which decrypt per-row) and filters in memory via `MatchesQuery` (content + plaintext metadata, case-insensitive). Unencrypted vaults keep the efficient SQL `LIKE` path. Added the in-memory-SQLite **connection-string seam** to `DatabaseService` (progress on KV-045) and `ThrowIfReplacing()` to `Search` (partial KV-008). Tests: `DatabaseServiceSearchTests` (encrypted content found, ciphertext-at-rest sanity, no-match empty). RED→GREEN verified.

---

## 🟠 High

### KV-005 · Self-exclusion broken — app captures its own keystrokes & clipboard
- **Area:** Correctness · **Status:** ✅ FIXED (2026-05-30) · `Services/CaptureService.cs`, `Services/ClipboardMonitorService.cs`
- Both defined `SelfProcessName = "Kapture"` but the renamed process is **`KaptureVault`** → self-exclusion never matched; the app logged its own input.
- **Fix applied:** `SelfProcessName` is now `static readonly = Process.GetCurrentProcess().ProcessName` in both services (rename-safe; resolves to "KaptureVault" in the published app). Regression test: `CaptureServiceTests.Flush_WhenActiveWindowIsKaptureVaultItself_DoesNotCapture` (drives the service with the test runner's own process name as the active window). RED→GREEN verified.

### KV-006 · PBKDF2 100k iterations below 2026 guidance
- **Area:** Crypto · **Status:** ✅ **FIXED — shipped in v1.0.5** (2026-05-31, T-11, commit 5748f9f) · `Services/EncryptionService.cs`
- 100k PBKDF2-HMAC-SHA256 is ~6× under current OWASP (600k+); Argon2id is the modern recommendation. `vault.db` + `encryption.json` sit together in LocalAppData → a leaked pair is GPU-brute-forceable.
- **Fix (shipped in v1.0.5):** New vaults derive at **600k**; `encryption.json` persists the KDF params (`Iterations`, `Kdf`). `Unlock` derives with the stored count, defaulting pre-T-11 files (no count) to 100k, so existing vaults still open. Argon2id + re-keying legacy vaults deferred (needs the transactional bulk path, KV-021/T-20).

### KV-007 · OAuth client secret bundled in installer + hardcoded fallback
- **Area:** Security · **Status:** OPEN — **deferred to F-02 (decision 2026-05-31)** · `Services/CloudSync/GoogleDriveProvider.cs:15,82-87,113`, `installer/KaptureVaultSetup.iss:101-105`
- App uses PKCE but still *hard-requires* `_clientSecret` (`AuthenticateAsync` refuses without it; it's sent in the token exchange at :113), ships `client_secret.json` into Program Files, and has a hardcoded `FallbackClientId` (that constant is a client **ID** — public, not a secret). A secret shipped to every user is not secret.
- **2026-05-31 DECISION (user):** fix **deferred to F-02 Phase 1 (backend broker)**, not a quick client-side change. Rationale: Google's desktop token endpoint still expects `client_secret` (PKCE alone secret-less was not confirmable without risking sync for all users), so the correct fix is the F-02 backend that brokers the OAuth code/refresh exchange — the client then holds only the public client ID + PKCE and never a secret. Re-confirmed the bundling persists (the v1.0.5 installer compresses `client_secret.json`). Until F-02 ships, the bundled value is a Google-"non-confidential" desktop credential (PKCE-protected); **do not widen distribution on that assumption.**
- **Fix (via F-02 Phase 1):** backend brokers token exchange; client keeps only the public client ID + PKCE; stop bundling `client_secret.json`; remove the `FallbackClientId` constant.

### KV-008 · `ThrowIfReplacing()` gate applied inconsistently → sync-swap races
- **Area:** Data · **Status:** ✅ FIXED (2026-05-30, P1) · `Services/DatabaseService.cs`
- The gate guarded only some public methods; ~11 (incl. `GetByApp/UpdatePin/GetStats/Encrypt/DecryptAllEntries`) were ungated.
- **Fix applied:** `ThrowIfReplacing()` is now called at the top of **every** public read/write method, so during a sync replace all ops fail fast with a clear "retry shortly" instead of racing a half-copied file.
- **Residual (minor, deferred):** the check-then-act window still exists (the `_dbGate` semaphore is held only by the replace path, not by readers). A true reader/writer lock would close it fully; low priority given sync is single-device-recommended (KV-003).

### KV-009 · Fragile positional/ordinal column mapping in `ReadEntries`
- **Area:** Data · **Status:** ✅ FIXED (2026-05-30, P1) · `Services/DatabaseService.cs`
- Hard-coded ordinals against `SELECT *` — a reordered schema or a DB synced from a different app version would shift ordinals and silently corrupt the decode.
- **Fix applied:** `ReadEntries` builds a case-insensitive name→ordinal map from the reader once and reads every field by name (optional post-migration columns tolerated). Test: `DatabaseServiceCrudTests` (all-field round-trip + null-expiry + pin/tags).

### KV-010 · `HotkeyService` created outside DI, never disposed
- **Area:** Lifecycle · **Status:** 🟡 PARTIAL (2026-05-31, T-10, v1.0.5) · `App.axaml.cs:140-143`, `Services/HotkeyService.cs`
- `new HotkeyService()` owns a message-only HWND + background STA thread; only `Stop()` on the Quit path, never `Dispose()`, never touched on restart/cancel shutdowns → orphaned global hotkey registration.
- **Fix:** Register as a DI singleton; ensure teardown on every shutdown path (see KV-011). HotkeyService + MainWindowViewModel now resolved from DI via ServiceRegistration (shipped v1.0.5). Residual: HotkeyService disposal + provider teardown on all shutdown paths still pending T-08.

### KV-011 · Service teardown only on tray-Quit path
- **Area:** Lifecycle · **Status:** OPEN · `App.axaml.cs:266-288`, `Views/SettingsWindow.axaml.cs:237,250,278`, `App.axaml.cs:84`
- `_capture/_clipboardMonitor/_screenshotService/_hotkeyService.Stop()`, tray disposal, and `SyncOnClose` live **only** in the Quit handler. The three `SettingsWindow` restart routes and the encryption-cancel `Shutdown()` bypass all of it → hooks/timers keep running, SyncOnClose silently skipped.
- **Fix:** Centralize teardown in `ShutdownRequested`/`OnExit`; dispose the provider there (KV-024); run sync-on-close once regardless of trigger.

### KV-012 · Synchronous WAL+AES SQLite INSERT on the keyboard-hook thread
- **Area:** Performance · **Status:** ✅ **FIXED — shipped in v1.0.5** (2026-05-31, T-07, commit e5977dd) · `Services/CaptureService.cs`
- When the buffer hit `MaxBufferSize` mid-typing, `Flush()` ran `Open()` + INSERT + AES **inside the WH_KEYBOARD_LL callback**, degrading system-wide input latency and risking hook eviction (`LowLevelHooksTimeout`).
- **Fix (shipped in v1.0.5):** `Flush()` now hands the entry to a bounded `Channel<CaptureEntry>` (non-blocking `TryWrite`, `AllowSynchronousContinuations=false`); a single writer task (`ProcessWriteQueueAsync`, started in `Start()`) performs Open()+INSERT+AES off the hook thread. `Stop()` completes + drains the channel (<=5s) so the final buffered entry isn't lost; inserts are now serialized through one writer.
- **Tests:** `CaptureServiceTests.Flush_DoesNotBlockTheHookThreadOnTheDatabaseWrite` (RED->GREEN) + `Stop_DrainsBufferedEntriesAndDoesNotLoseData`.

### KV-013 · Entry `ListBox` effectively non-virtualized
- **Area:** Performance · **Status:** 🟡 PARTIAL (2026-05-30, P1) · `MainWindowViewModel.cs`, `Services/DatabaseService.cs`, `ViewModels/Converters.cs`
- (a) no LIMIT → whole table loaded; (b) `Entries.Clear()`+rebuild tears down realized containers each flush; (c) per-row converters `new SolidColorBrush` per call.
- **Done:** (a) `GetAll`/`GetByApp` take an optional `limit`; the entry list caps at `MaxDisplayedEntries = 1000` (search still scans the full vault) — `DatabaseServiceCrudTests.GetAll_WithLimit_…`. (c) converters return cached `ImmutableSolidColorBrush` — `ConverterTests`.
- **Remaining:** (b) diff-update `Entries` instead of `Clear()`+rebuild (apply the sidebar pattern, preserving order + selection) — pairs with **KV-032** (debounce `Refresh`) and **KV-033** (move whole-table decrypt off the UI thread). Deferred to the next P1 batch (threading/UI-thread work).

### KV-014 · Annotation editor base `Bitmap` never disposed
- **Area:** Performance/Memory · **Status:** ✅ FIXED (2026-05-30, P1) · `Views/Dialogs/ScreenshotEditorWindow.axaml.cs`
- **Fix applied:** base bitmap stored in `_baseBitmap` field and disposed in a new `OnClosed` override.

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
- **Area:** Correctness · **Status:** ✅ FIXED (2026-05-30, P1) · `Views/Dialogs/ScreenshotEditorWindow.axaml.cs`
- **Fix applied:** `LoadImage` shows a "file not found" status; `SaveAs_Click` guards on `_baseBitmap == null || double.IsNaN(Width/Height)` and the whole export is wrapped in try/catch (surfaces failures instead of crashing the `async void` handler).

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
- **Area:** Performance · **Status:** ✅ FIXED (2026-05-30, P1) · `ScreenshotEditorWindow.axaml.cs`
- **Fix applied:** `using var rtb = …` inside the try/catch — released on every path.

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
✅ FIXED (2026-05-30) — `ClipboardMonitorService.PollClipboard` now records `_lastClipboardText` on the self-originated path before returning, so the same string later copied from a real app isn't wrongly suppressed. Fixed alongside KV-005.

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
