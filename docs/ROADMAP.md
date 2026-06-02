---
document: ROADMAP
version: 1.18.0
app-version: 1.1.0
last-updated: 2026-06-02
last-audit: 2026-06-02
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
> **P1 — ✅ COMPLETE (final batch on `main` 2026-06-01, unreleased).** **T-16** (Avalonia.Headless.XUnit harness + VM filter-selection/diff regressions; test suite **71**), **T-09** (Entries diff-update via `SyncEntries` + debounced `RequestRefresh` + off-UI-thread query/decrypt; `CaptureEntry` observable), and **T-08** (centralized idempotent teardown via `ShutdownRequested` + `ShutdownCoordinator`; ServiceProvider disposed on every exit path) all landed test-first. **T-12 (secret-less OAuth, residual KV-007) is RETIRED** — F-02 Phase 1's backend brokers the OAuth exchange, so the client-side cutover lands in **F-02 Phase 2**. **F-02 engine BUILT + provisioned LIVE; Phases 0–1 (polish + desktop UX) done; Phase 2 (backend free-vault + quota/size-cap + refresh-token fix + CORS + `/me` tier + client storage display) done** (2026-06-01). **Phase 3 ✅ COMPLETE (2026-06-02, slices A–H)** — screenshots now sync to the Online Vault, end-to-end encrypted + quota-aware (F `a00ee25` client pipeline, G `5cc03e6` restore, H docs); client **162** / backend **59**. **Next: a live end-to-end smoke → cut v1.0.8**, then the P2 backlog (incl. T-35). `/account` deferred to Phase 4/5. Phase 2 is **deployed live + smoke-verified** (2026-06-01, Worker `17ba084b`; R2-bucket CORS applied; secrets rotated). _Resequenced 2026-05-31: T-08/T-09 followed T-16 so the lifecycle/UI refactors were verifiable via the headless harness — which is how they shipped._

---

# 🚀 FEATURE ROADMAP (product — CURRENT FOCUS)

**F-01 (DB export) shipped in v1.0.6.** F-02 is the major initiative — its **engine is built and provisioned LIVE**, Phases 0–1 (polish + desktop UX) are done, **Phase 2 (free vault + quota + refresh-token fix + CORS + `/me` tier + client storage display) is done, DEPLOYED LIVE + smoke-verified** (2026-06-01, Worker `17ba084b`; R2 CORS applied; secrets rotated; `/account` deferred to Phase 4/5), **Phase 3 (vault-sync v2) ✅ COMPLETE (2026-06-02)**, **Phase 4 (web vault) ✅ BUILT**, and **"P5" (UX redesign) ✅ BUILT + AUDITED (2026-06-02)** — P5 decoupled Google Drive backup from the Online Vault (auto-syncs when signed in), added a main-window **Login → Log out/Web Vault/Upload/Sync** toolbar (spinning Sync icon; tier-adaptive Upload upgrade popup), and a **true web-vault handoff** (the browser auto-logs-in from a one-time code; backend `2bd0bee` + desktop `00c379d` + website `0907f51`). Client **182** / backend **65**; both independent audits PASS / "SAFE TO DEPLOY". **All ship together as v1.1.0** — **next is the human go-live** (backend D1 schema + `wrangler deploy` for the handoff → add the `kapture.tools` Google JS origin → push all three repos → end-to-end smoke → `Invoke-Release.ps1 -BumpType major`). The agreed product model + build phases are below; full design in `docs/F-02-online-vault-design.md` (§ Revision 2) + `docs/F-02-PHASE-3-DESIGN.md` (§ 11).

> **P5 follow-up (LOW, from the P5c security audit):** the `handoff_codes` table is GC'd only opportunistically on exchange and `/auth/handoff/create` has no rate limit — bounded (single-use, 120 s TTL) + auth-gated, so deferred. *Optional hardening:* a Cloudflare WAF/rate-limit rule on `/auth/handoff/*`, a scheduled `DELETE WHERE expires_at <= now`, or a per-uid cap. Do only if abuse is observed.

## F-01 · Export vault DB to local disk  *(free tier · ✅ IMPLEMENTED 2026-05-31 — unreleased, ships v1.0.6)*

> **✅ Done (2026-05-31, on `main`, unreleased — ships in v1.0.6):** `ExportVaultDatabaseCommand` + an **Export DB** toolbar button (`MainWindowViewModel` / `MainWindow.axaml`) → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy` off the UI thread (handles `VACUUM INTO`'s no-pre-existing-file rule; encrypted vaults export as-is, noted in the tooltip). Regression tests: `DatabaseServiceBackupTests` (standalone copy with every row + empty-vault). Tests 47 → **49**. Ships in the next release (**v1.0.6**). The spec below is the original design, now realized.

**Goal:** let users save a copy of their vault to a file they choose — not only sync to Google Drive.
- Settings → **"Export Vault Database…"** button → `IStorageProvider.SaveFilePickerAsync` (`.db`) → `DatabaseService.CreateBackupCopy(path)` — **already exists** (`VACUUM INTO`, WAL-safe).
- If encryption is on, the export is the encrypted SQLite (valid backup; restoring needs the password) — label it so.
- **Test-first:** in-memory DB → insert rows → `CreateBackupCopy(temp)` → open the copy → assert rows present. Small, self-contained, ships in the free tier.

## F-02 · "Online Vault" — accounts, free cloud sync, paid file hosting  *(epic · multi-week · separate backend repo)*

> **Agreed product model — Revision 2 (2026-06-01).** Supersedes the earlier "paid-only vault" framing. Full design + critique constraints in `docs/F-02-online-vault-design.md` (§ Revision 2).

**Tiers:**
| Capability | Free (offline) | Free (registered) | Paid — $49/yr |
|---|:---:|:---:|:---:|
| Desktop app · local vault · DB export · Google Drive sync | ✓ | ✓ | ✓ |
| Account — **Google OR email/password** | — | ✓ | ✓ |
| **Online vault sync** (encrypted capture DB + re-encoded screenshots) + **web vault** | — | ✓ (≤ **250 MB**) | ✓ (~**10 GB**) |
| **File hosting** (arbitrary files) · private/public · **shareable links** | — | — | ✓ |

- The **paid differentiator is file hosting + share links** — vault sync is **free** for any registered account. Screenshots sync **re-encoded** (BMP→PNG/JPEG), counted against the quota. Free cap **250 MB**, paid **~10 GB** (config, tunable).
- **Settled:** per-user namespace in ONE R2 bucket (`users/{uid}/`); one feature-gated app; **no storage/Stripe/Google secret in the client, ever** — the Worker brokers presigned URLs **and** the OAuth code exchange.
- **Stack:** Cloudflare **R2 + Workers + D1** + **Stripe**; identity = Google **and** email/password. Backend repo `kapturevault-backend` (`C:\dev\kapturevault-backend`, private). `R2StorageProvider : ICloudStorageProvider` slots beside `GoogleDriveProvider`.

**Build phases (replaces the old 4-phase table):**
| # | Phase | Status |
|---|-------|--------|
| Engine | Backend foundation (auth/billing/presign/D1) + client API client, account/session layer, `R2StorageProvider`, DI wiring, account UI | **✅ BUILT + provisioned LIVE** — backend `8480022` (Worker at `kapturevault-backend.kapture.workers.dev`, `/health` ok); client `6ad70e5`..`9bd7369`. Backend **26** vitest; client suite **120**. |
| 0 | Polish — UTF-8 "Connected" page, email-not-uid, 402→message | **✅ DONE** (`e0c49f2`) |
| 1 | Desktop UX — Online Vault panel (sign-in → Open Vault → Upgrade); relocate Export-DB + Run-on-startup → **Settings → General**; Settings overflow fix; kapture.tools (no www) | **✅ DONE** (`d4e1ff8`,`7c7a7f8`,`8b8e964`,`55f2279`,`97f4ca8`) |
| 2 | **Backend free-vault + foundations** — dropped the `/vault/*` gate (free sync); per-user **quota + server-pinned object-size cap** (HEAD-on-commit at `PUT /vault/meta`, reject + delete over-quota, `storage_used` maintained; 250 MB free / 10 GB paid, tunable); **refresh ≠ session token** (distinct audiences); Worker **CORS**; `/me` → `{tier,features,quota,used}`. **Client:** Settings panel shows quota/used. Backend vitest 26→51, client 123. Commits `f657b87`..`e61a3ad` (backend) + `09f2fee` (client). | ✅ **DONE + DEPLOYED LIVE** 2026-06-01 — Worker version `17ba084b`; R2-bucket CORS applied (`r2-cors.json`); secrets rotated. **`/account` → Phase 4/5** |
| 3 | Client vault-sync v2 — multi-object sync (`vault.db` + re-encoded screenshot images), quota-aware; carry salt/KDF in `vault.db.meta` for web unlock | ✅ **COMPLETE 2026-06-02 (A–H)** — A KDF meta · B encryption interlock · C binary crypto · **backend** D object API · E multi-object quota · **F** (`a00ee25`) client screenshot pipeline · **G** (`5cc03e6`) restore · **H** docs/UX. Client **162** / backend **59**. **Unpushed; v1.0.8 pending a live smoke.** See the slice tracker ↓ |
| 4 | Web vault (`kapture.tools/vault`) — read the Online Vault (R2) + decrypt + **show screenshots**. Google sign-in → `/auth/session`; `vault/get-url` + `vault/meta` (KDF from meta — fixes the hardcoded 100k); WebCrypto decrypt; `vault/objects` + `object/get-url` → binary decrypt → image. Built in `Kapture.Tools-Website/vault/index.html` (`b5e2fc7`). Email/password login → Phase 5. T-34 did **not** block this (built in the website repo directly). | 🟢 **BUILT 2026-06-02** — unpushed; needs the Google JS-origin for `kapture.tools` + a live smoke, then ships v1.1.0. Desktop "Use Online Vault for sync" control (`86cfc30`) + KV-046 ShutdownMode fix (`cbfbf5e`) landed alongside. |
| 5 | Email/password auth — `/auth/register|verify|login|reset` + transactional email + rate-limiting + the **account-password vs vault-password interlock**; closes the residual KV-007 for the sign-in client | ⬜ (after v1.2.0) |
| 6 | File hosting (paid) — `/files/*` + shares + 250 MB-per-file cap + desktop **Upload files** UI + public/private + share links | 🟢 **BUILT + AUDITED 2026-06-02 (6A–6D); ships v1.2.0, LOCAL/unpushed.** Backend: 6A `c990834` (file API + paid gate + unified quota), 6B `171c3cd` (shares + public `GET /s/{token}`), 6D-1 `c9c6257` (per-file `encrypted` + virtual `folder`; share-encrypted→409). Client: 6C `34ecbeb` (FileHostingService + api), 6D-2 `006bcc9` (encrypt/download), 6D-3 `7956410` (FilesWindow + folders + TextPromptDialog). **Per-file 🔒 encrypt OR 🔗 public link** (mutually exclusive) + virtual folders. Backend **85** vitest / client **207**; **audit "SAFE TO DEPLOY".** Go-live = `migrations/0001` + `wrangler deploy` → push client+backend → smoke (subscribed) → `Invoke-Release.ps1 -BumpType major` (**v1.2.0**) → extend ToS/Privacy. **LOW follow-ups:** orphan put-url row/object GC + put-url rate-limit; add `DELETE` to Worker CORS when the web vault gains file delete; confirm the lapsed-subscriber-public-link policy. |

**Phase 3 slice tracker** (full design + acceptance in `docs/F-02-PHASE-3-DESIGN.md` §§ 5, 11–12):
- [x] **A** — web-unlock meta: KDF params (salt/iterations/kdf) carried in `vault.db.meta` v2 (`3b5c131`)
- [x] **B** — encryption interlock: a vault password is required to use the Online Vault — R2 upload backstop (`R2StorageProvider` refuses when `!IsActive`) + `OnlineAccountViewModel` gate + `VaultPasswordRequired` panel warning; replaced the stale `IsPaid` sync-gate (`c716d20`)
- [x] **C** — binary `EncryptBytes`/`DecryptBytes` on `EncryptionService` (AES-GCM raw bytes; tamper/wrong-key/not-active throw) (`912821a`)
- [x] **D** — backend `/vault/object/{put,get,delete}-url` + `GET /vault/objects` (keys validated to `screenshots/<name>`) (`0193551`)
- [x] **E** — backend multi-object quota: `PUT /vault/meta` sums all vault objects (R2 list) + rejects over-quota without deleting (`6e4570c`)
- [x] **F — client screenshot sync pipeline** (`a00ee25`): client object API (`GetObjectPutUrlAsync`/`GetObjectGetUrlAsync`/`DeleteObjectAsync`/`ListObjectsAsync` + `VaultObject`/`VaultObjectList` + `IsPayloadTooLarge`); `SkiaScreenshotImageCodec` (BMP→PNG); `ScreenshotSyncService.SyncUpAsync` (enumerate→re-encode→`EncryptBytes`→upload-only-new oldest-first within quota; orphan cleanup; meta-recommit + 413 trim/retry backstop); wired into `CloudSyncManager`. **Deviation:** remote object list is the source of truth for "already uploaded" — no local `online_sync_state.json` (robust across devices, no drift; list needed anyway for orphan/quota).
- [x] **G — restore** (`5cc03e6`): `ScreenshotSyncService.RestoreAsync` (`GET /vault/objects` → download each missing → `DecryptBytes` → write the PNG into the local screenshots dir by filename); runs on download-wins. **Deviation:** resolve-by-filename at *display* (`CaptureEntry.ScreenshotPath` fallback, all four screenshot read sites repointed) instead of mutating `Content` in the DB — avoids multi-device LWW path ping-pong.
- [x] **H — UX + docs** : panel status delivered via `CloudSyncManager` folding the result into `LastSyncStatus` (Settings `SyncStatusText`: "· N screenshot(s)", "· N not synced — over quota", "· N restored"); managed docs reconciled to v1.15.0. **v1.0.8 deferred to the maintainer** pending a fresh live end-to-end smoke. **Known test gap:** `CloudSyncManager` push/restore dispatch wiring stays unit-untested (static `DbPath`); verified correct by independent audit.

**Hard constraints (from the design critique — honor in the relevant phase):** refresh token ≠ session token (fix before passwords); web-viewer KDF 100k→600k; account-password ≠ vault-encryption-password (silent data-loss trap → hard interlock); PBKDF2-on-Worker is attacker-triggerable → rate-limit (needs a new CF KV/DO binding); never trust client-reported size (pin in the presigned signature / HEAD from R2); CORS on the Worker **and** the R2 bucket.

**Open product decisions:** confirm the paid quota (~10 GB); the **kapture.tools repo consolidation (T-34)** — gates Phase 4.

**Live prereqs (human — ✅ DONE 2026-06-01):** Cloudflare R2+D1+Workers, Stripe (live price `price_1TdVtY…` + keys + webhook), Google OIDC sign-in client, all secrets set, D1 schema applied, Worker deployed. Runbook: `docs/F-02-PROVISIONING.md`.

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
| T-35 | Route `GoogleDriveProvider` through the F-02 backend broker (stop bundling `client_secret.json`, drop `FallbackClientId`); reuse the Phase 2 secret-less model — closes the residual KV-007 for Drive sync | KV-007 | M |

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

## Infra / repo consolidation

| # | Task | Notes | Effort |
|---|------|-------|--------|
| T-34 | **Investigate consolidating the kapture.tools website into the main repo (if feasible).** The marketing site lives in a **separate** repo `Kapture.Tools-Website` (`C:\DEV\Kapture.Tools-Website`), deployed via an external host (no GitHub Pages); the app repo's `docs/` is a redundant legacy landing page that still carries a stale `kapture.tools` CNAME. **Approach:** decide monorepo-vs-separate, then either `git subtree` the site into `KaptureVault/site/` (preserves history) or copy it in, repoint the host (likely Cloudflare Pages) at the main repo/subdir, and retire the `docs/` page + duplicate CNAME. **Caveats:** only one repo can serve the `kapture.tools` custom domain; don't break the live site; the host build config is external. May legitimately stay separate if that's cleaner — this is an investigate-and-decide task. | — (website repo) | M |

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
