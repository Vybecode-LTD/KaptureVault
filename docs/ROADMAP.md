---
document: ROADMAP
doc-version: 1.0.0
app-version: 1.0.2
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: codebase-audit
---

# ROADMAP.md — KaptureVault

> Prioritized remediation plan from the 2026-05-30 audit. Issue IDs reference `BUGS.md`.
> Ordering = risk × user-impact × effort. **Do P0 before shipping any further releases.**

---

## P0 — Critical / do first (data loss, security, broken core behavior)

| # | Task | Issues | Effort | Notes |
|---|------|--------|--------|-------|
| T-01 | **Revoke + rotate all 3 Google OAuth secrets** in Cloud Console | KV-001 | S (human) | **Cannot be done by Claude.** Most urgent. Old history secret was never revoked. |
| T-02 | **Purge secret from parent `Utilities` git history** (`git filter-repo`/BFG, force-push) + confirm repo visibility | KV-001 | M | Deleting the file later did not remove it from history. |
| T-03 | **Fix self-exclusion** — `SelfProcessName = "KaptureVault"` (or derive from `Process.ProcessName`) | KV-005, KV-034 | XS | One-line, high value. App currently captures its own input. **Start here.** |
| T-04 | **Stop silently swallowing decrypt failures** — throw typed `DecryptionException`, surface to UI | KV-002 | S | Restores AES-GCM integrity guarantee. |
| T-05 | **Fix / guard content search under encryption** — decrypt-then-filter or clear "unavailable while encrypted" notice | KV-004, KV-041 | M | Currently returns nothing silently. |
| T-06 | **Address Drive multi-device data loss** — at minimum document single-device-only + keep pre-sync backup; ideally per-entry merge | KV-003, KV-029 | L | Whole-DB clobber. Decide: document limitation now, real delta-sync later. |

## P1 — High (reliability, security hardening, perf hot paths)

| # | Task | Issues | Effort |
|---|------|--------|--------|
| T-07 | Move SQLite INSERT off the keyboard-hook thread (bounded `Channel` + writer task) | KV-012 | M |
| T-08 | Centralize shutdown/teardown (`ShutdownRequested`/`OnExit`): stop all services, dispose tray + ServiceProvider, run SyncOnClose once | KV-011, KV-010, KV-024 | M |
| T-09 | Make the entry `ListBox` virtualize: add `LIMIT`/paging, diff-update `Entries`, cache converter brushes | KV-013, KV-032, KV-033 | M |
| T-10 | Register `HotkeyService` + ViewModels in DI; stop service-locator use in Views | KV-010, KV-015(partial) | M |
| T-11 | Raise PBKDF2 to ≥600k now; plan Argon2id migration with KDF params in `encryption.json` | KV-006 | S→M |
| T-12 | Make desktop OAuth client secret-less (native + loopback PKCE); stop bundling `client_secret.json`; remove `FallbackClientId` | KV-007 | M |
| T-13 | Apply DB concurrency gate consistently (all public methods) | KV-008 | S |
| T-14 | `SELECT` explicit named columns + `GetOrdinal` in `ReadEntries` | KV-009 | S |
| T-15 | Dispose annotation-editor base `Bitmap` (`OnClosed`) + `using` the `RenderTargetBitmap` | KV-014, KV-023 | XS |
| T-16 | **Stand up the test project + first-PR suite** (see `TESTING.md`); regression test for the filter fix | KV-045 | M |

## P2 — Medium (correctness, hardening, MVVM hygiene)

| # | Task | Issues | Effort |
|---|------|--------|--------|
| T-17 | Guard ScreenshotEditor SaveAs against missing source image (NaN→PixelSize crash) | KV-018 | XS |
| T-18 | Replace `SHA256(key)` verification with a known-plaintext verifier / HKDF value | KV-019 | S |
| T-19 | Zero the master key on disable/lock/shutdown | KV-020 | XS |
| T-20 | Wrap bulk encrypt/decrypt in a transaction | KV-021 | S |
| T-21 | `wal_checkpoint(TRUNCATE)` before DB copy/replace | KV-022 | S |
| T-22 | Extract `SettingsViewModel`/`QuickPasteViewModel`/`ContentViewerViewModel`; bind instead of code-behind | KV-015, KV-027, KV-037 | L |
| T-23 | Inject clipboard/file-dialog/toast abstractions into `MainWindowViewModel` | KV-026 | M |
| T-24 | Harden `CloudSyncManager` timer (try/catch, log) + `WithRetryAsync` (transport retry, terminate) | KV-028, KV-030, KV-031 | M |
| T-25 | Debounce/coalesce `Refresh()` | KV-032 | S |
| T-26 | Add a test CI workflow (`dotnet test` on `windows-latest`, coverage) | KV-045 | S |

## P3 — Low / polish

| # | Task | Issues | Effort |
|---|------|--------|--------|
| T-27 | Fix UAC-cancel revert when no settings file; log failures | KV-035 | XS |
| T-28 | Reorder mutex release in `RestartElevated` (start process first) | KV-036, KV-025 | S |
| T-29 | Incremental pen geometry for active stroke | KV-038 | S |
| T-30 | `DecodeToWidth` for list thumbnails (biggest memory lever) | KV-039 | S |
| T-31 | Pool/stream screenshot buffers | KV-040 | S |
| T-32 | Verify capture can't insert while locked-but-configured (+ test) | KV-042 | S |
| T-33 | Remove dead `ViewLocator` reflection (or back with keyed DI) | KV-043 | XS |

---

## Shipped this session (v1.0.2 — 2026-05-30)

- ✅ App/tag sidebar filter selection fix (diff-update, see CHANGELOG)
- ✅ Mobile vault viewer web app (`kapture.tools/vault`)
- ✅ Screenshot save-as-image + annotation editor (v1.0.1)
- ✅ About dialog, BMP installer icon, release automation (v1.0.1)
- ✅ Release automation script (`scripts/Invoke-Release.ps1`) + CHANGELOG

## Carried over from earlier (pre-audit, still relevant)

- Align data paths (legacy split was a pre-fork concern; verify all paths now under `%LOCALAPPDATA%\KaptureVault`)
- Quick Paste hotkey: `AppSettings` stores a string but `HotkeyService` hardcodes VK constants — needs a parser to honor user config

---

## Effort key
XS ≈ <30 min · S ≈ <½ day · M ≈ ½-2 days · L ≈ multi-day
