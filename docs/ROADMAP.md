---
document: ROADMAP
version: 1.4.0
app-version: 1.0.4
last-updated: 2026-05-30
last-audit: 2026-05-30
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
> **P1 — in progress (first batch shipped in v1.0.4):**
> ✅ T-13 (KV-008 gate), T-14 (KV-009 named columns), T-15 (KV-014/023/018 editor leaks) — all test-first. 🟡 T-09 partial (KV-013: brush caching + 1000-row cap done; Entries diff-update remains). 🟡 T-16 in progress (test suite 10 → **30**). Release pipeline now single-creator (`auto-release.yml`).
> **Remaining P1, recommended order:** **T-07** (DB writes off the hook thread — top risk), **T-08** (centralized shutdown/teardown), **T-09 remainder + KV-032/033** (Entries diff-update / debounce / off-UI decrypt), **T-12** (secret-less OAuth — closes residual KV-007), **T-11** (PBKDF2/Argon2id), **T-10** (DI for HotkeyService + ViewModels), then continue **T-16**.

---

# 🚀 FEATURE ROADMAP (product — CURRENT FOCUS)

Two new product directions (added 2026-05-30). **F-01 is the immediate next task**; F-02 is a larger, phased initiative. Full feasibility/architecture discussion is recorded in `AUDIT-LOG.md` (2026-05-30 PM-4).

## F-01 · Export vault DB to local disk  *(free tier · ~hours · START HERE)*

**Goal:** let users save a copy of their vault to a file they choose — not only sync to Google Drive.
- Settings → **"Export Vault Database…"** button → `IStorageProvider.SaveFilePickerAsync` (`.db`) → `DatabaseService.CreateBackupCopy(path)` — **already exists** (`VACUUM INTO`, WAL-safe).
- If encryption is on, the export is the encrypted SQLite (valid backup; restoring needs the password) — label it so.
- **Test-first:** in-memory DB → insert rows → `CreateBackupCopy(temp)` → open the copy → assert rows present. Small, self-contained, ships in the free tier.

## F-02 · Paid "Online Vault" — accounts + R2 storage + file hosting  *(epic · multi-week · new backend repo)*

**Goal:** a paid tier (**$49/yr**) where registered users get cloud storage for their vault **and** can upload files (**< 250 MB**), get **share links**, and see bucket items in the vault.

**Three load-bearing decisions (settled in discussion):**
1. **Per-user *namespace* in ONE shared bucket** (`users/{uid}/…`) — not a bucket-per-user (buckets are account-capped).
2. **One feature-gated app**, not two versions — free = offline + DB export; paid features unlock on login with an active subscription. One codebase.
3. **🔒 No storage/Stripe secrets in the desktop client, ever** — a backend brokers short-lived **presigned URLs**. (Same lesson as the KV-001 OAuth leak, higher stakes; makes **T-12** a hard prerequisite and leans on the VERSION_CONTROL secret discipline.)

**Recommended stack:** Cloudflare **R2** (no egress fees — ideal for share links) + **Workers** (backend API) + **D1** (user/file/share metadata) + **Stripe** (subscription); reuse the existing **Google sign-in** for identity. An `R2StorageProvider : ICloudStorageProvider` slots next to `GoogleDriveProvider` for DB sync.

**Phases:**
| # | Phase | Where |
|---|-------|-------|
| 1 | Backend foundation — Worker API + R2 + D1 + Stripe + auth (verify subscription → issue presigned URLs scoped to `users/{uid}/`) | **new backend repo** |
| 2 | Client online vault — `R2StorageProvider` (DB-sync alt to Drive) + login UI + subscription gate | KaptureVault |
| 3 | Client file hosting — upload (presigned PUT, 250 MB cap enforced client + server) + file list + share links + files-in-vault | KaptureVault |
| 4 | Ops — quotas, billing portal, deletion, abuse/DMCA handling | both |

**Reality check:** this turns KaptureVault into a hosted product — a new backend repo, recurring infra cost (R2 cheap + no egress; Workers/D1 ~free at small scale; Stripe ~2.9% + 30¢), and a real operational/legal surface (ToS/privacy updates, share-link abuse/DMCA, data deletion, account management). The economics work; the commitment is the ops surface. **Not yet started** — the fresh session decides whether to design F-02 in full or scaffold Phase 1 after shipping F-01.

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
| T-07 | ⬜ next | Move SQLite INSERT off the keyboard-hook thread (bounded `Channel` + writer task) | KV-012 | M |
| T-08 | ⬜ | Centralize shutdown/teardown (`ShutdownRequested`/`OnExit`): stop all services, dispose tray + ServiceProvider, run SyncOnClose once | KV-011, KV-010, KV-024 | M |
| T-09 | 🟡 partial | Make the entry `ListBox` virtualize. **Done:** brush caching + 1000-row cap. **Left:** diff-update `Entries`, debounce, off-UI decrypt | KV-013, KV-032, KV-033 | M |
| T-10 | ⬜ | Register `HotkeyService` + ViewModels in DI; stop service-locator use in Views | KV-010, KV-015(partial) | M |
| T-11 | ⬜ | Raise PBKDF2 to ≥600k now; plan Argon2id migration with KDF params in `encryption.json` | KV-006 | S→M |
| T-12 | ⬜ | Make desktop OAuth client secret-less (native + loopback PKCE); stop bundling `client_secret.json`; remove `FallbackClientId` | KV-007 | M |
| T-13 | ✅ done | Apply DB concurrency gate consistently (all public methods) | KV-008 | S |
| T-14 | ✅ done | Read columns by name (case-insensitive map) in `ReadEntries` | KV-009 | S |
| T-15 | ✅ done | Dispose annotation-editor base `Bitmap` (`OnClosed`) + `using` the `RenderTargetBitmap` + SaveAs guard | KV-014, KV-023, KV-018 | XS |
| T-16 | 🟡 in progress | Test suite (10 → 30 tests). **Left:** Avalonia headless smoke tests, VM filter regression, CI test job, `dotnet format`/vuln-scan in loop | KV-045 | M |

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

- **v1.0.4** — P1 hardening batch 1 (KV-008, KV-009, KV-014/023/018, KV-013 partial); release pipeline now single-creator via `auto-release.yml` (VirusTotal + sliced-changelog notes).
- **v1.0.3** — P0 remediation (KV-005/002/004/003) + OAuth rotation + history purge + test harness.
- **v1.0.2** — sidebar filter fix; mobile vault viewer (`kapture.tools/vault`).
- **v1.0.1** — About dialog; screenshot save-as-image + annotation editor; BMP installer icon; release automation + CHANGELOG.

## Carried over from earlier (pre-audit, still relevant)

- Align data paths (legacy split was a pre-fork concern; verify all paths now under `%LOCALAPPDATA%\KaptureVault`)
- Quick Paste hotkey: `AppSettings` stores a string but `HotkeyService` hardcodes VK constants — needs a parser to honor user config

## Human / one-time (not code — needs the maintainer)

- **Google Cloud Console:** confirm the OLD web secret is deleted (desktop client already recreated); reconfigure the desktop client as **secret-less native + loopback PKCE** (pairs with T-12); finish the OAuth consent screen for `kapture.tools` (authorized domain + Privacy/TOS URLs, exit Testing mode).
- **GitHub Pages + DNS:** point `kapture.tools` at Pages — A `@` → `185.199.108–111.153`, CNAME `www` → `vybecode-ltd.github.io`; enable Enforce HTTPS. Verify `kapture.tools` in Google Search Console.
- **Mobile viewer:** paste the web client ID `232322018793-70gd1j2j…` into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **Repo hygiene:** consider moving the repos off the OneDrive path (OneDrive + `.git` is risky).

---

## Effort key
XS ≈ <30 min · S ≈ <½ day · M ≈ ½-2 days · L ≈ multi-day
