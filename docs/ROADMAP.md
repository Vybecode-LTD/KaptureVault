---
document: ROADMAP
version: 1.9.0
app-version: 1.0.7
last-updated: 2026-06-01
last-audit: 2026-06-01
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/BUGS.md, docs/TESTING.md, docs/HANDOFF.md, docs/AUDIT-LOG.md]
---

# ROADMAP.md — KaptureVault

> Two tracks: a **Feature Roadmap** (new product work — the current focus) and the
> **audit-remediation backlog** (P0 done; P1–P3 tech debt). Issue IDs reference `BUGS.md`;
> feature IDs are `F-NN`. Ordering = risk × user-impact × effort.

---

> **P0 — ✅ complete, shipped in v1.0.3.** T-01 (secrets rotated), T-02 (history purged + verified), T-03/04/05 fixed test-first; T-06 🟡 mitigated.
>
> **P1 — in progress (batch 1 shipped in v1.0.4; batch 2 shipped in v1.0.5):**
> ✅ T-13 (KV-008 gate), T-14 (KV-009 named columns), T-15 (KV-014/023/018 editor leaks), **T-07** (KV-012 — DB writes off the hook thread, bounded `Channel` + writer task), **T-11** (KV-006 — PBKDF2 600k + persisted KDF params), **T-10** (KV-010 — HotkeyService + MainWindowViewModel in DI via `ServiceRegistration`) — all test-first. 🟡 T-09 partial (KV-013: brush caching + 1000-row cap done; Entries diff-update remains). 🟡 T-16 partial (test suite now **49** — was 47 at the v1.0.5 cut, +2 from F-01's `DatabaseServiceBackupTests`; CI `dotnet test` + `dotnet format --verify` + `--vulnerable` scan added — headless + VM-filter regression tests pending). Release pipeline single-creator (`auto-release.yml`).
> **Shipped in v1.0.5 (2026-05-31):** the **T-07 + T-11 + T-10** batch (DB-writes-off-hook-thread, PBKDF2 600k + KDF params, DI via `ServiceRegistration`), plus the `dotnet format`/`--vulnerable` CI gates from T-16.
> **P1 — ✅ COMPLETE (final batch on `main` 2026-06-01, unreleased).** **T-16** (Avalonia.Headless.XUnit harness + VM filter-selection/diff regressions; test suite **71**), **T-09** (Entries diff-update via `SyncEntries` + debounced `RequestRefresh` + off-UI-thread query/decrypt; `CaptureEntry` observable), and **T-08** (centralized idempotent teardown via `ShutdownRequested` + `ShutdownCoordinator`; ServiceProvider disposed on every exit path) all landed test-first. **T-12 (secret-less OAuth, residual KV-007) is RETIRED** — F-02 Phase 1's backend brokers the OAuth exchange, so the client-side cutover lands in **F-02 Phase 2**. **Next focus: F-02 Phase 2** (client Online Vault) and/or the **P2 backlog**; the P1 batch can ship as **v1.0.7** whenever a release is cut. _Resequenced 2026-05-31: T-08/T-09 followed T-16 so the lifecycle/UI refactors were verifiable via the headless harness — which is how they shipped._

---

# 🚀 FEATURE ROADMAP (product — CURRENT FOCUS)

Two new product directions (added 2026-05-30). **F-01 is implemented (ships v1.0.6)**; F-02 is a larger, phased initiative — **Phase 1 (backend foundation) is now DONE**. Full feasibility/architecture discussion is recorded in `AUDIT-LOG.md` (2026-05-30 PM-4).

## F-01 · Export vault DB to local disk  *(free tier · ✅ IMPLEMENTED 2026-05-31 — unreleased, ships v1.0.6)*

> **✅ Done (2026-05-31, on `main`, unreleased — ships in v1.0.6):** `ExportVaultDatabaseCommand` + an **Export DB** toolbar button (`MainWindowViewModel` / `MainWindow.axaml`) → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy` off the UI thread (handles `VACUUM INTO`'s no-pre-existing-file rule; encrypted vaults export as-is, noted in the tooltip). Regression tests: `DatabaseServiceBackupTests` (standalone copy with every row + empty-vault). Tests 47 → **49**. Ships in the next release (**v1.0.6**). The spec below is the original design, now realized.

**Goal:** let users save a copy of their vault to a file they choose — not only sync to Google Drive.
- Settings → **"Export Vault Database…"** button → `IStorageProvider.SaveFilePickerAsync` (`.db`) → `DatabaseService.CreateBackupCopy(path)` — **already exists** (`VACUUM INTO`, WAL-safe).
- If encryption is on, the export is the encrypted SQLite (valid backup; restoring needs the password) — label it so.
- **Test-first:** in-memory DB → insert rows → `CreateBackupCopy(temp)` → open the copy → assert rows present. Small, self-contained, ships in the free tier.

## F-02 · Paid "Online Vault" — accounts + R2 storage + file hosting  *(epic · multi-week · separate private backend repo)*

**Goal:** a paid tier (**$49/yr**) where registered users get cloud storage for their vault **and** can upload files (**< 250 MB**), get **share links**, and see bucket items in the vault.

**Three load-bearing decisions (settled in discussion):**
1. **Per-user *namespace* in ONE shared bucket** (`users/{uid}/…`) — not a bucket-per-user (buckets are account-capped).
2. **One feature-gated app**, not two versions — free = offline + DB export; paid features unlock on login with an active subscription. One codebase.
3. **🔒 No storage/Stripe secrets in the desktop client, ever** — a backend brokers short-lived **presigned URLs** (and now the OAuth token exchange). (Same lesson as the KV-001 OAuth leak, higher stakes; leans on the VERSION_CONTROL secret discipline. **The backend now exists** — see Phase 1 below — so the client-side secret-less auth that was T-12/KV-007 lands in Phase 2.)

**Recommended stack:** Cloudflare **R2** (no egress fees — ideal for share links) + **Workers** (backend API) + **D1** (user/file/share metadata) + **Stripe** (subscription); reuse the existing **Google sign-in** for identity. An `R2StorageProvider : ICloudStorageProvider` slots next to `GoogleDriveProvider` for DB sync.

**Backend repo (NEW, off OneDrive):** `kapturevault-backend` — `https://github.com/Vybecode-LTD/kapturevault-backend`, on disk `C:\dev\kapturevault-backend`. Separate private repo (deliberately not on the OneDrive path; see the OneDrive tooling hazard in `CLAUDE.md` Lessons).

**Phases:**
| # | Phase | Where | Status |
|---|-------|-------|--------|
| 1 | Backend foundation — Worker API + R2 + D1 + Stripe + auth (verify subscription → issue presigned URLs scoped to `users/{uid}/`) | **`kapturevault-backend`** | **✅ DONE (2026-05-31, repo `kapturevault-backend`)** — Cloudflare Worker: Google-auth sessions, Stripe billing + webhook→D1 state machine, presigned R2 URLs scoped to `users/{uid}/`, entitlement gate, D1 schema; **19 vitest tests + tsc + GitHub CI green**. Retires T-12/KV-007 (backend brokers the OAuth exchange). |
| 2 | Client online vault — `R2StorageProvider` (DB-sync alt to Drive) + login UI + subscription gate **+ secret-less client OAuth (ex-T-12/KV-007, now via the backend broker)** | KaptureVault | ⏳ **NEXT** |
| 3 | Client file hosting — upload (presigned PUT, 250 MB cap enforced client + server) + file list + share links + files-in-vault | KaptureVault | ⬜ |
| 4 | Ops — quotas, billing portal, deletion, abuse/DMCA handling | both | ⬜ |

**Reality check:** this turns KaptureVault into a hosted product — a separate backend repo (`kapturevault-backend`), recurring infra cost (R2 cheap + no egress; Workers/D1 ~free at small scale; Stripe ~2.9% + 30¢), and a real operational/legal surface (ToS/privacy updates, share-link abuse/DMCA, data deletion, account management). The economics work; the commitment is the ops surface. **Phase 1 is started AND done** (backend foundation built + 19 tests + CI green, 2026-05-31); **Phase 2 (client `R2StorageProvider` + login + subscription gate, including the secret-less client OAuth folded in from T-12/KV-007) is next**, then Phase 3 (client file hosting), then Phase 4 (ops).

---

# 🔧 AUDIT-REMEDIATION BACKLOG

## P0 — Critical / ✅ COMPLETE (shipped v1.0.3)

| # | Task | Status |
|---|------|--------|
| T-01 | Rotate the 3 Google OAuth secrets | ✅ done |
| T-02 | Purge secret from `Utilities` git history | ✅ done + verified |
| T-03 | Fix self-exclusion (KV-005/034) | ✅ done (tested) |
| T-04 | Stop swallowing decrypt failures (KV-002) | ✅ done (tested) |
| T-05 | Fix content search under encryption (KV-004) | ✅ done (tested) |
| T-06 | Drive data-loss | 🟡 mitigated (backup retained); full merge → P1 |

<details><summary>Original P0 detail (kept for reference)</summary>

### (historical) P0 — Critical / do first (data loss, security, broken core behavior)

| # | Task | Issues | Effort | Notes |
|---|------|--------|--------|-------|
| T-01 | **Revoke + rotate all 3 Google OAuth secrets** in Cloud Console | KV-001 | S (human) | **Cannot be done by Claude.** Most urgent. Old history secret was never revoked. |
| T-02 | **Purge secret from parent `Utilities` git history** (`git filter-repo`/BFG, force-push) + confirm repo visibility | KV-001 | M | Deleting the file later did not remove it from history. |
| T-03 | **Fix self-exclusion** — `SelfProcessName = "KaptureVault"` (or derive from `Process.ProcessName`) | KV-005, KV-034 | XS | One-line, high value. App currently captures its own input. **Start here.** |
| T-04 | **Stop silently swallowing decrypt failures** — throw typed `DecryptionException`, surface to UI | KV-002 | S | Restores AES-GCM integrity guarantee. |
| T-05 | **Fix / guard content search under encryption** — decrypt-then-filter or clear "unavailable while encrypted" notice | KV-004, KV-041 | M | Currently returns nothing silently. |
| T-06 | **Address Drive multi-device data loss** — at minimum document single-device-only + keep pre-sync backup; ideally per-entry merge | KV-003, KV-029 | L | Whole-DB clobber. Decide: document limitation now, real delta-sync later. |

</details>

## P1 — High (reliability, security hardening, perf hot paths)

| # | Status | Task | Issues | Effort |
|---|--------|------|--------|--------|
| T-07 | ✅ done | Move SQLite INSERT off the keyboard-hook thread (bounded `Channel` + writer task) | KV-012 | M |
| T-08 | ✅ done | Centralized idempotent teardown via `ShutdownRequested` + `ShutdownCoordinator`: stops capture, bounded SyncOnClose once (restarts skip it), disposes tray + ServiceProvider (→ HotkeyService/CloudSyncManager) on every exit path. 2026-06-01 | KV-011, KV-024 | M |
| T-09 | ✅ done | Entry list diff-update (`SyncEntries`, reuse-by-Id) replacing `Clear()`+rebuild; debounced `RequestRefresh`; query/decrypt off the UI thread; `CaptureEntry` observable. (Brush caching + 1000-row cap shipped earlier.) 2026-06-01 | KV-013, KV-032, KV-033 | M |
| T-10 | ✅ done | Register `HotkeyService` + `MainWindowViewModel` in DI (`ServiceRegistration.cs`); resolved from container, not `new`ed in `App`. View `App.Services` locator cleanup (KV-015) folded into T-22 | KV-010, KV-015(partial) | M |
| T-11 | ✅ done | PBKDF2 raised to 600k for new vaults; KDF params persisted in `encryption.json` (legacy vaults default to 100k, still open); re-key + Argon2id deferred (needs KV-021/T-20) | KV-006 | S→M |
| ~~T-12~~ | ✅ RETIRED → **F-02** | Secret-less client OAuth. **Retired 2026-05-31:** F-02 Phase 1's backend (repo `kapturevault-backend`, built + CI green) now **brokers the Google token exchange**, so the client holds no secret. The client-side change (stop bundling `client_secret.json`, remove `FallbackClientId`) is now **F-02 Phase 2 client work** — no longer a standalone P1. | KV-007 | M |
| T-13 | ✅ done | Apply DB concurrency gate consistently (all public methods) | KV-008 | S |
| T-14 | ✅ done | Read columns by name (case-insensitive map) in `ReadEntries` | KV-009 | S |
| T-15 | ✅ done | Dispose annotation-editor base `Bitmap` (`OnClosed`) + `using` the `RenderTargetBitmap` + SaveAs guard | KV-014, KV-023, KV-018 | XS |
| T-16 | ✅ done | test suite **71**; CI `dotnet test` + `dotnet format --verify` + `--vulnerable` scan live in tests.yml (green); `Avalonia.Headless.XUnit` harness (`TestAppBuilder`) + headless MainWindow smoke + VM filter/diff regressions added. (Only coverage-% tracking remains — optional.) 2026-06-01 | KV-045 | M |

## P2 — Medium (correctness, hardening, MVVM hygiene)

| # | Task | Issues | Effort |
|---|------|--------|--------|
| ~~T-17~~ | ✅ done (folded into T-15) — SaveAs NaN/missing-image guard | KV-018 | XS |
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

## Shipped (releases) — see `CHANGELOG.md`

- **v1.0.5** — P1 hardening batch 2 (T-07 DB-writes-off-hook-thread, T-11 PBKDF2 600k + KDF params, T-10 DI via ServiceRegistration); plus `dotnet format` debt closed (app+tests) and `dotnet format`/`--vulnerable` gates added to the Tests CI workflow.
- **v1.0.4** — P1 hardening batch 1 (KV-008, KV-009, KV-014/023/018, KV-013 partial); release pipeline now single-creator via `auto-release.yml` (VirusTotal + sliced-changelog notes).
- **v1.0.3** — P0 remediation (KV-005/002/004/003) + OAuth rotation + history purge + test harness.
- **v1.0.2** — sidebar filter fix; mobile vault viewer (`kapture.tools/vault`).
- **v1.0.1** — About dialog; screenshot save-as-image + annotation editor; BMP installer icon; release automation + CHANGELOG.

## Carried over from earlier (pre-audit, still relevant)

- Align data paths (legacy split was a pre-fork concern; verify all paths now under `%LOCALAPPDATA%\KaptureVault`)
- Quick Paste hotkey: `AppSettings` stores a string but `HotkeyService` hardcodes VK constants — needs a parser to honor user config

## Human / one-time (not code — needs the maintainer)

- **Google Cloud Console:** confirm the OLD web secret is deleted (desktop client already recreated); reconfigure the desktop client as **secret-less native + loopback PKCE** (now part of F-02 Phase 2 client work — the `kapturevault-backend` broker handles the token exchange); finish the OAuth consent screen for `kapture.tools` (authorized domain + Privacy/TOS URLs, exit Testing mode).
- **GitHub Pages + DNS:** point `kapture.tools` at Pages — A `@` → `185.199.108–111.153`, CNAME `www` → `vybecode-ltd.github.io`; enable Enforce HTTPS. Verify `kapture.tools` in Google Search Console.
- **Mobile viewer:** paste the web client ID `232322018793-70gd1j2j…` into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **Repo hygiene:** ✅ DONE 2026-06-01 — KaptureVault repo moved off OneDrive to `C:\DEV\Utilities\KaptureVault` (alongside `kapturevault-backend` at `C:\dev`).

---

## Effort key
XS ≈ <30 min · S ≈ <½ day · M ≈ ½-2 days · L ≈ multi-day
