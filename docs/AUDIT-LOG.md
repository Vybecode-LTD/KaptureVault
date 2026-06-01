---
document: AUDIT-LOG
version: 1.8.0
app-version: 1.0.6
last-updated: 2026-06-01
last-audit: 2026-06-01
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/BUGS.md, docs/ROADMAP.md, docs/TESTING.md, docs/HANDOFF.md]
---

# AUDIT-LOG.md — KaptureVault

## 2026-06-01 (PM) — v1.0.6 released, P1 backlog (T-16/T-09/T-08) completed, docs → v1.8.0

**Trigger:** Same-day continuation after the repo relocation (entry below). User: cut v1.0.6 (ship F-01), then run the P1 backlog T-16 → T-09 → T-08.

**v1.0.6 released (single-creator pipeline held):** promoted CHANGELOG `[Unreleased]` (F-01 Export Vault Database) → `[1.0.6] — 2026-06-01`; ran `Invoke-Release.ps1 -BumpType minor` from the new `C:\DEV` location (bumped csproj+iss to 1.0.6, published, built the installer, committed `aee32b5`, tagged `v1.0.6`, pushed). `auto-release.yml` (`github-actions[bot]`) VirusTotal-scanned + created the **GitHub Release v1.0.6** with `KaptureVaultSetup-1.0.6-x64.exe` — verified live via `gh`.

**P1 backlog COMPLETE (test-first, each its own commit; on `main`, unreleased — ships v1.0.7):**
- **T-16** (`ff78e6d`) — `Avalonia.Headless.XUnit` harness (`TestAppBuilder` over the real `App`); `MainWindowViewModelFilterTests` (filter/selection survive a background Refresh) + `MainWindowSmokeTests` (headless MainWindow constructs/binds). Closes KV-045.
- **T-09** (`53f0ad4`) — `SyncEntries` diff-updates `Entries` in place (reuse-by-Id, reorder, trim) replacing `Clear()`+rebuild (KV-013); debounced `RequestRefresh` (KV-032); query/decrypt off the UI thread via `RefreshAsync` + `Task.Run` (KV-033); `CaptureEntry.IsPinned/Tags` made observable; `MainWindowViewModelEntriesDiffTests`.
- **T-08** (`bf3658c`) — centralized idempotent teardown: `ShutdownCoordinator` + `OnShutdownRequested` wired to `desktop.ShutdownRequested`; stops capture, bounded sync-on-close once (restarts pass false via `App.ShutdownForRestart`), disposes tray + ServiceProvider → HotkeyService/CloudSyncManager (KV-011/KV-024); encryption-cancel + the 3 SettingsWindow restart paths now route through it; `ShutdownCoordinatorTests`.

**Verification (proof, not assertion):** at each step `dotnet test` (49 → 56 → 58 → 63 → 71, all passing), `dotnet build -c Release` 0/0, `dotnet format --verify` clean. Backlog pushed (`aee32b5..bf3658c`); `git ls-remote` HEAD = `bf3658c` = origin/main.

**Documentation reconciliation → shared `version` 1.8.0 / `app-version` 1.0.6:** CLAUDE, HANDOFF, ROADMAP, BUGS, TESTING, AUDIT-LOG bumped; CHANGELOG `[Unreleased]` staged for v1.0.7 (UI-responsiveness + clean-shutdown). Marked fixed: KV-011, KV-013, KV-024, KV-032, KV-033, KV-045 (+ KV-010 disposal residual). **All P1 audit issues are now resolved.** ROADMAP↔code, BUGS↔code, TESTING↔suite (8 suites / 71 tests) reconciled.

**Auditor:** Claude (Opus 4.8). **Next:** F-02 Phase 2 (client Online Vault) and/or P2 backlog; cut **v1.0.7** to ship the P1 batch whenever desired.

---

## 2026-06-01 — Repo relocated off OneDrive to C:\DEV\Utilities\KaptureVault

**Trigger:** User — "move the repo folder to `C:\DEV\Utilities` and make it the permanent location, change all known links, then work from there." Then: cut **v1.0.6** (F-01) and run the P1 backlog **T-16 → T-09 → T-08**.

**Move (verified — proof, not assertion):** robocopy of the OneDrive working tree → `C:\DEV\Utilities\KaptureVault`, preserving `.git` and the two gitignored-but-required secrets (`client_secret.json`, `kaptureweb_clientsecret.json`), excluding regenerable `bin/obj/publish`. Verified at the destination *before* deleting the source: `git fsck --connectivity-only` clean (only harmless dangling objects), `git rev-parse HEAD` = **`dc615ce`** = `origin/main`, `git status` clean, and restore + build + **`dotnet test` 49/49 passing**. The OneDrive source was then removed (`Test-Path` → False) and the `C:\DEV` copy reconfirmed intact. The destination `Utilities` is the off-OneDrive twin of the parent repo (already held `Basefra.me` + `Kapture`), and `C:\DEV\` already carries the constitution + six directive files, so KaptureVault's `../../` directive references resolve correctly at the new path.

**Why:** retires the OneDrive tooling hazard (stale / fabricated / empty tool output) documented since 2026-05-31; the client now sits alongside `kapturevault-backend` under `C:\DEV`.

**Reference fixes (the "known links"):** repo-root path updated in `CLAUDE.md`; `HANDOFF.md` "Start here" + the CRITICAL hazard section reframed as RESOLVED with the new `cd` path and corrected expected SHA (`dc615ce`); the "repo hygiene: move off OneDrive" item marked ✅ DONE in HANDOFF + ROADMAP; the CLAUDE Lessons OneDrive-hazard bullet marked RESOLVED; a Session Log entry added; the 4 project memory files updated and copied to the new project key so future `C:\DEV\Utilities` sessions inherit them. The `%LOCALAPPDATA%\KaptureVault` runtime data path is unchanged (independent of repo location). `.claude/plan.md` "OneDrive provider" mentions were left intact — they refer to the cloud-sync *feature*, not the repo.

**Auditor:** Claude (Opus 4.8). **Next:** v1.0.6 release, then T-16 → T-09 → T-08.

---

## 2026-05-31 (PM-2) — F-01 shipped to main + F-02 Phase 1 backend built

**Trigger:** Continuation of the same-day session, *after* the v1.0.5 reconciliation (the PM entry below). Two deliverables landed and a final reconciliation pass took the managed doc set to **`version` 1.7.0** (app-version stays **1.0.5** — F-01 is unreleased).

**Ground truth re-verified first (proof, not assertion — OneDrive tooling hazard):** client `git ls-remote` HEAD **`ddc3ce4` = `origin/main`**; working tree carries only the in-flight doc edits (CLAUDE/HANDOFF/ROADMAP/TESTING already at 1.7.0, this AUDIT-LOG + BUGS being brought up now); `git ls-files` confirms `DatabaseServiceBackupTests.cs`, `ServiceRegistrationTests.cs`, `tests.yml`, `auto-release.yml`, and `docs/F-02-online-vault-design.md` all present (a parallel `Glob` returned empty — a known OneDrive reporting artifact, *not* missing files; git is authoritative). Backend repo present at `C:\dev\kapturevault-backend` with commits `4758a50` + `8795110`. Client test count **49** confirmed by attribute + `[InlineData]` expansion (Capture 4 / Encryption 6 / Search 3 / Replace 1 / Crud 4 / **Backup 2** / ServiceRegistration 13 / Converter 16 = 49); backend **19** `it/test` cases across 4 spec files.

**F-01 (Export Vault Database) IMPLEMENTED — on `main`, UNRELEASED (ships v1.0.6):** an **Export DB** toolbar button → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy` (WAL-safe `VACUUM INTO`) run **off the UI thread**; encrypted vaults export as-is (restoring needs the password; labelled in the tooltip). Regression: `DatabaseServiceBackupTests` (2 — standalone copy with every row opened as an independent file connection + empty-vault still a valid DB). Commit **`ddc3ce4`** (= HEAD = `origin/main`). Client tests **47 → 49**. Staged under CHANGELOG **`[Unreleased]`** → promotes to **v1.0.6**; not yet released.

**F-02 Phase 1 — BACKEND BUILT in a NEW separate private repo `kapturevault-backend`:** `https://github.com/Vybecode-LTD/kapturevault-backend`, on disk `C:\dev\kapturevault-backend` — **deliberately off OneDrive** (the tooling-hazard mitigation). A Cloudflare **Worker** (TypeScript) providing: Google-token verify → first-party **session JWT**; **Stripe** billing + webhook → **D1** subscription state machine; **presigned R2 URLs scoped to `users/{uid}/`**; an **entitlement gate**; and the **D1 schema**. **19 vitest tests + `tsc --noEmit` clean + GitHub Actions CI green** — commits **`4758a50`** (foundation) + **`8795110`** (router/store/billing/webhook/presign). No client code yet; it cannot run live until the human cloud prereqs are filled (Cloudflare R2/Workers/D1, Stripe keys + $49/yr price id, Google OIDC client) — see HANDOFF.

**Decision — KV-007 / T-12 RETIRES via the backend broker (lands in F-02 Phase 2):** with `kapturevault-backend` now brokering Google's desktop token exchange, the client will hold only the public client ID + PKCE and **no `client_secret`**. T-12 is therefore no longer a standalone P1 — the client-side cutover (stop bundling `client_secret.json`, remove `FallbackClientId`) is **F-02 Phase 2** client work. The v1.0.5 installer still bundles `client_secret.json` meanwhile (a Google-"non-confidential", PKCE-protected desktop credential); **do not widen distribution on that assumption** until Phase 2 ships.

**Documentation reconciliation (this pass) — doc set → `version` 1.7.0:** all managed docs now carry shared **`version` 1.7.0** + **`app-version` 1.0.5** (CLAUDE/HANDOFF/ROADMAP/TESTING were bumped earlier this session; AUDIT-LOG + BUGS brought up in this pass — BUGS had lagged at 1.6.0). Reconciled to ground truth across CLAUDE.md, CHANGELOG.md, ROADMAP.md, BUGS.md, TESTING.md, HANDOFF.md: F-01 uniformly **implemented-but-unreleased (ships v1.0.6)**; client test count **49** everywhere; F-02 Phase 1 backend **DONE** in the separate `kapturevault-backend` repo (19 tests + tsc + CI); **KV-007/T-12 retired-via-F-02-backend** (Phase 2), not a standalone open task. Cross-document checks: frontmatter versions uniform (CHANGELOG has none by design); ROADMAP↔code, BUGS↔code, TESTING↔suite (8 suites / 49 tests), CLAUDE↔reality, HANDOFF↔state all consistent; cross-links resolve (`tests.yml`, `auto-release.yml`, `docs/F-02-online-vault-design.md` all verified on disk via `git ls-files`). No CRITICAL/HIGH reconciliation failures; nothing left needing manual review beyond the carried-over human cloud prereqs for F-02 live testing.

**Auditor:** Claude (Opus 4.8), single session. **Next audit due:** next session start, or at the next release (v1.0.6).

---

## 2026-05-31 (PM) — v1.0.5 release, format-debt closure + CI hardening, KV-007 decision, doc reconciliation

**Trigger:** User — "read & give a rundown." That established a verified baseline, then drove the rest of the session: cut **v1.0.5**, close the open `dotnet format` debt and the **KV-007/T-12** question, and re-reconcile every managed doc. This is a *later same-day event* than the P1-batch-2 entry below (which staged v1.0.5 but did not cut it).

**Baseline re-verified first (proof, not assertion):** `git ls-remote` (HEAD `d92952d` = `origin/main`), `git status` (tree clean), `dotnet build` Debug+Release (0 warnings / 0 errors), `dotnet test` (**47 passed / 0 failed / 0 skipped**), `dotnet list package --vulnerable` (none). Only after that did any change proceed.

**v1.0.5 cut (single-creator pipeline held):** `scripts/Invoke-Release.ps1` built/packaged/bumped/committed-CHANGELOG/**pushed**; `.github/workflows/auto-release.yml` (the **single** release creator, `github-actions[bot]`) then VirusTotal-scanned the installer and created the **GitHub Release**. Tag **`v1.0.5` = commit `ffc3c9d`**. `CHANGELOG.md` has the `[1.0.5] — 2026-05-31` section. **v1.0.5 = P1 hardening batch 2:** T-07/KV-012 (SQLite INSERT moved off the WH_KEYBOARD_LL hook thread → bounded `Channel<CaptureEntry>` + single writer task; `Stop()` drains), T-11/KV-006 (PBKDF2 → 600k + persisted KDF params; legacy vaults default to 100k and still open), T-10/KV-010 (`HotkeyService` + `MainWindowViewModel` resolved from DI via `ServiceRegistration.AddKaptureServices()`).

**Corrected doc/tooling drift (DEBUG_PROTOCOL "proof, not assertion" — applied to our own docs & tooling):** prior docs carried two false claims, both OneDrive fabrication-hazard artifacts caught by re-running authoritative commands:
- **"~11–12 commits unpushed"** — FALSE. `git ls-remote` showed HEAD already on `origin/main`; nothing was unpushed. The earlier "push didn't happen" was a stale/fabricated harness result, not reality.
- **"format-clean"** — FALSE. `dotnet format --verify-no-changes` failed with ~130 pre-existing whitespace violations. The repo was *not* clean.
Both were corrected at the point of discovery rather than carried forward — the same "verify with verbatim command output, never trust a prior summary" discipline we apply to code, turned on our own documentation and tooling.

**Format-debt closure + CI hardening (completes the format/vuln half of T-16/KV-045):** the ~130 whitespace failures were fixed across two commits — app **`51dc9fd`** + test project **`12b7122`** — and the whole solution now verifies clean. The gates are now **CI-enforced**: `.github/workflows/tests.yml` (commit **`d92952d`**) runs, on every push/PR to `main` (windows-latest, .NET 9), `dotnet build` → `dotnet format --verify-no-changes` → `dotnet list package --vulnerable --include-transitive` → `dotnet test` (TRX + Cobertura). **VERIFIED GREEN on GitHub** — Actions run **26725669973**, all steps ✓, 2m6s. Remaining T-16 work: `Avalonia.Headless.XUnit` UI smoke + a `MainWindowViewModel` filter-selection regression test.

**KV-007 / T-12 decision (user) — DEFER secret-less OAuth to F-02 Phase 1 (backend broker):** investigated `GoogleDriveProvider` (it *hard-requires* `_clientSecret` — `AuthenticateAsync` refuses without it, and it's sent in the token exchange) and confirmed via Google's docs that the desktop token endpoint still expects `client_secret`. Conclusion: a purely client-side secret-less fix isn't safely confirmable, so the correct fix is the **F-02 backend** brokering the OAuth code/refresh exchange — the client then holds only the public client ID + PKCE and **no secret**. The v1.0.5 installer still bundles `client_secret.json` meanwhile (a Google-"non-confidential", PKCE-protected desktop credential); **distribution should not widen on that assumption.** T-12 is no longer a standalone pre-distribution P1 — it is folded into F-02 Phase 1.

**Documentation reconciliation (this pass):** all managed docs bumped to shared **`version` 1.6.0** and **`app-version` 1.0.5** (was 1.5.0 / 1.0.4; CLAUDE.md had lagged at 1.4.0 and was brought to 1.6.0). Reconciled to ground truth across CLAUDE.md, CHANGELOG.md, ROADMAP.md, BUGS.md, TESTING.md, HANDOFF.md: v1.0.5 marked **shipped** (not staged); test count **47** everywhere (no stale "30"); KV-012/KV-006 ✅ FIXED-in-v1.0.5 and KV-010 🟡 partial (DI done, disposal still needs T-08); KV-007/T-12 uniformly **deferred-to-F-02**; T-16 shown as format/vuln-CI-done with headless + VM-filter regression remaining; CHANGELOG has the `[1.0.5]` entry + tag link. Cross-document checks: frontmatter versions uniform (CHANGELOG has none by design); ROADMAP↔code, BUGS↔code, TESTING↔suite (7 suites / 47 tests), CLAUDE↔reality, HANDOFF↔state all consistent; cross-links resolve (`tests.yml` and `docs/F-02-online-vault-design.md` both verified present on disk). No CRITICAL/HIGH reconciliation failures; no drift left needing manual review.

**Auditor:** Claude (Opus 4.8), single session. **Next audit due:** next session start, or at the next release.

---

## 2026-05-31 — P1 remediation batch 2 (T-07/T-11/T-10), CI, F-02 design + reconciliation

**Trigger:** User — "knock out P1 tech debt first, then F-01, then design F-02 in full." Carried across a context-limit boundary; this entry reconciles the whole 2026-05-31 effort.

**Code delivered (all test-first, RED→GREEN; each its own commit; tests 30 → 47):**
- **T-07 / KV-012** (`e5977dd`) — SQLite INSERT moved **off the WH_KEYBOARD_LL hook thread**. `CaptureService.Flush()` enqueues to a bounded `Channel<CaptureEntry>` (non-blocking `TryWrite`, `AllowSynchronousContinuations=false`); a single writer task (`ProcessWriteQueueAsync`, started in `Start()`) performs Open()+INSERT+AES off-thread; `Stop()` completes+drains (≤5 s) so no final-entry loss. Serializes writes through one writer (was up to 3 threads). Tests: `Flush_DoesNotBlockTheHookThreadOnTheDatabaseWrite`, `Stop_DrainsBufferedEntriesAndDoesNotLoseData`. *(Initial thread-identity test was rejected as flaky — `Stop().Wait()` can inline the writer on the draining thread — and replaced with a non-blocking-behaviour assertion.)*
- **T-11 / KV-006** (`5748f9f`) — PBKDF2 raised to **600k** (OWASP 2023) for new vaults; `encryption.json` persists KDF params (`Iterations`, `Kdf`). `Unlock` derives with the stored count, defaulting pre-T-11 files (no count) to 100k → **existing vaults still open** (no lockout). Tests: `Configure_StoresStrongKdfParams_AndDerivesWithThem`, `Unlock_LegacyVaultWithoutStoredIterations_StillUnlocksAndDecrypts`. Deferred: legacy re-keying (needs KV-021/T-20 transactional bulk path) + Argon2id.
- **T-10 / KV-010** (`0351500`) — composition root extracted to `ServiceRegistration.AddKaptureServices()` (new `ServiceRegistration.cs`); `HotkeyService` + `MainWindowViewModel` resolved from DI instead of `new`'d in `App`. Explicit factory for the VM (mirrors the original ctor; it has a design-time ctor too) → behaviour-identical. New `ServiceRegistrationTests`. Residual KV-015 (View `App.Services` locator) left for T-22.
- **T-16 partial / KV-045** (`24cd3f2`) — `.github/workflows/tests.yml`: CI `dotnet test` on push/PR to main (windows-latest, .NET 9, TRX+Cobertura; ignores docs/releases/installer). Headless smoke + VM filter-regression tests still pending.

**Design delivered:** **F-02** full paid-Online-Vault design (`docs/F-02-online-vault-design.md`, `b57b22d`) — R2 + Workers + D1 + Stripe; per-user namespace; no client secrets (Worker brokers presigned URLs); data model, Worker API, 4 phases, costs, ops/legal, risks. No code. Hard prereq: T-12.

**Resequencing decision:** T-08 (shutdown teardown) + T-09 (Entries diff-update) deferred to pair **after** T-16's headless harness — they're lifecycle/UI with no clean unit test + high regression risk, so they must be verifiable not hoped. T-11 was pulled early as a clean, self-contained win.

**Release decision (user-pinned):** next release is **v1.0.5** with the T-07/T-11/T-10 hardening batch (mirrors v1.0.4's "P1 batch 1"); F-01 becomes a later feature release. CHANGELOG `[Unreleased]` still to be promoted — see HANDOFF "Known loose ends".

**⚠️ Tooling-hazard incident (process, not code):** the repo is on a **OneDrive path** and the tool harness returned **stale / delayed / fabricated / empty** results repeatedly. The first 05-31 pass *reported* commits, a CI file, the F-02 doc, and a `git push` that had **not actually happened** — caught at this session's start by re-running `git log`/`git status`/`Test-Path` (DEBUG_PROTOCOL "verify with proof, not assertion" — applied to our own tooling). Mitigations that held: verify every consequential action with a second authoritative command (esp. git); `Write` (full-file) is safe blind/repeatable; `Edit` is safe-on-failure; one PowerShell per turn (a non-zero exit cancels parallel siblings); build+test after every change. Underlying git/build/test were sound — only result *reporting* was unreliable. **Recommendation logged: move the repo off OneDrive.**

**Docs:** all managed docs bumped to `version` 1.5.0 (app-version stays 1.0.4 until v1.0.5 is cut). BUGS KV-012/KV-006 → FIXED (`60a89ca`); KV-010 / test-count / CHANGELOG reconciliation noted as remaining in HANDOFF. `docs/F-02-online-vault-design.md` added as a non-managed design reference (in `see-also`).

**Auditor:** Claude (Opus 4.8), single-session. **Next audit due:** at the v1.0.5 cut, or next session start.

---

## 2026-05-30 (PM-4) — AlfaFF incident (external) + feature roadmap added

**Trigger:** User reported a "fail-closed network-capture filter" in KaptureVault taking the machine offline; then two new feature requests.

**AlfaFF incident — investigated, root-caused, NOT KaptureVault:**
- Per DEBUG_PROTOCOL, verified the hypothesis against code before changing anything. Searched the entire `Utilities` tree (KaptureVault + the original full Kapture + scaffolding) for `AlfaFF`/WFP/`Fwpm`/`DeviceIoControl`/`.sys`/`fltmc`/driver/packet/TLS-intercept → **zero matches**. KaptureVault's only capture surface is `KeyboardHookService`/`ClipboardMonitorService`/`ScreenshotService` (user-mode); manifest is `asInvoker` (cannot load a boot-start kernel driver); installer ships no driver/SDK. **No code path exists** for KaptureVault to program AlfaFF. Refused to fabricate a fix.
- Read-only machine investigation identified the real owner: **"Monitoring Software" by PCM** (paycomputermonitoring.com, v3.00.0018, installed 2026-05-14), a commercial surveillance product that bundles `AlfaFF.sys`/`.dll` + `instaff.exe` + `PCMFilterService`/`PCMActivityService` in a disguised path (`…\Common Files\Microsoft Shared\IC\bin\`). User resolved it (reboot; AlfaFF already `Start=Disabled`). **No KaptureVault change.**

**Feature roadmap added** (see `ROADMAP.md → 🚀 FEATURE ROADMAP`): **F-01** export vault DB to local disk (free tier, small); **F-02** paid "Online Vault" epic — accounts + Cloudflare R2 + file hosting + share links, $49/yr. Settled decisions: per-user namespace (not bucket-per-user); one feature-gated app (not two versions); no storage/Stripe secrets in the client (backend brokers presigned URLs — makes T-12 a prerequisite). Stack: R2 + Workers + D1 + Stripe + existing Google sign-in. Not started.

**Docs:** bumped to `version` 1.4.0; HANDOFF re-prioritized to lead with F-01/F-02.

---

## 2026-05-30 (PM-3) — release pipeline split + v1.0.4

**Trigger:** User — analysis of the two release "workflows," then "remove `gh release create`," then "add changelog-to-notes and release v1.0.4."

- **Single release creator:** removed `gh release create` (+ the `-SkipGitHub` flag) from `Invoke-Release.ps1`. The local script now only builds/packages/bumps/commits-CHANGELOG/pushes; `.github/workflows/auto-release.yml` is the sole creator. This fixed the race where the local `gh release create` pre-empted the workflow (its VirusTotal step was effectively dead).
- **Richer notes:** the workflow now slices the version's `## [X.Y.Z]` section out of `CHANGELOG.md` (awk `index()`), appends the download/platform/VirusTotal footer, and marks the release `--latest`.
- **Released v1.0.4** (P1 batch 1). **Verified end-to-end:** workflow run succeeded; release created by `github-actions[bot]` with the sliced changelog notes **and** a real VirusTotal badge (the >32 MB upload-URL path worked). First successful run of the workflow as sole creator.
- Docs bumped to `version` 1.3.0 / app 1.0.4; status flips (unreleased → shipped) across CLAUDE/BUGS/ROADMAP/HANDOFF.

---

## 2026-05-30 (PM-2) — P1 hardening + full doc reconciliation

**Trigger:** User — "let's do P1," then "get documentation all aligned and reconciled … prepare for a handoff … note the standing testing, debugging and documentation directives as well as the new release directives."

**P1 code (all test-first / verified):** ✅ KV-008 (gate on all DB methods), KV-009 (name-based column reads), KV-014/023/018 (annotation editor bitmap/RTB disposal + SaveAs guard), KV-013 partial (cached row brushes + 1000-row entry cap). Tests **10 → 30**, all green; app builds 0/0. 6 commits on `main`, unreleased (→ v1.0.4).

**Documentation reconciliation (this pass):**
- Captured the **standing directives** in `CLAUDE.md` (new "STANDING DIRECTIVES" section: Testing, Debugging/anti-loop, Documentation, Release), pointing at the authoritative parent files `../../DEBUG_PROTOCOL.md`, `../../TESTING_PROCEDURES.md`, `../../DOCUMENTATION_MANAGER.md`. Added a **Documentation Map** and a **Lessons** section to `CLAUDE.md`.
- **Version synchronization:** adopted the standard frontmatter field `version` across all managed docs and bumped to the shared **1.2.0** (was split `doc-version`/`app-version`; `app-version` 1.0.3 retained). Added `see-also` cross-links to every doc.
- Reconciled content vs. code: ROADMAP P1 statuses (T-13/14/15 ✅, T-09 🟡, T-16 🟡, T-17 folded into T-15), human/one-time to-dos added; BUGS progress header; TESTING inventory (30 tests) + required-checks directive; HANDOFF rewritten to current state; CHANGELOG Unreleased section.
- **Cross-document checks:** ROADMAP↔code ✅, BUGS↔code ✅ (fixed issues marked + test refs), TESTING↔suite ✅ (6 suites / 30 tests match disk), CHANGELOG↔versions ✅ (1.0.0–1.0.3 entries present; 1.2.0 doc version is the doc-set version, distinct from app version), cross-refs resolve. No CRITICAL/HIGH reconciliation failures found.

**Reconciliation result:** docs aligned at `version` 1.2.0 / app 1.0.3; HANDOFF (canary) accurate. Ready for a fresh session.

---

## 2026-05-30 (PM) — P0 remediation + v1.0.3 release

**Trigger:** User request — "knock them out one by one" (P0 fixes), then "release it + update docs."

**Code fixes (all test-first, RED→GREEN):**
- **KV-005 / KV-034** — self-exclusion derived from `Process.GetCurrentProcess().ProcessName` (was hardcoded `"Kapture"`); clipboard dedupe updated on the self path. `CaptureServiceTests`.
- **KV-002** — `Decrypt` throws `DecryptionException` on tamper/corruption/wrong-key; `ReadEntries` shows a per-row placeholder, `DecryptAllEntries` skips bad rows. `EncryptionServiceTests`.
- **KV-004** — `Search` filters decrypted candidates in memory when encryption is active. `DatabaseServiceSearchTests`.
- **KV-003** — `ReplaceDatabaseFromAsync` retains the `.pre_sync_backup` recovery point (mitigation; full merge deferred). `DatabaseServiceReplaceTests`.

**Test harness stood up (KV-045):** `KaptureVault.Tests` (xUnit + NSubstitute + FluentAssertions) on `KaptureVault.slnx`; persistence seams added (base-dir for `EncryptionService`, connection-string for `DatabaseService`). **10 tests passing.**

**Security (KV-001) — resolved:**
- All Google OAuth secrets **rotated** (desktop client recreated → new ID `…15r8pqq8…`; web secret rotated). New creds written to gitignored files + `%LOCALAPPDATA%`; `FallbackClientId` updated; stale duplicate removed.
- `Utilities` repo history **purged** with `git filter-repo` (secret file removed, all `GOCSPX-…` values scrubbed), force-pushed, local realigned + pruned, release tag restored on the cleaned commit. Verified clean via fresh remote clone. Repo is PRIVATE.
- **Residual:** new secret still bundled in installer → KV-007/T-12 (secret-less OAuth) before wide release.

**Release:** **v1.0.3** cut via `scripts/Invoke-Release.ps1` (CHANGELOG + version bump + installer + tag + GitHub release). Installer in `releases/latest/`.

**Status:** All P0 items closed (KV-003 mitigated). Next: P1.

---

## 2026-05-30 (AM) — Full codebase audit (v1.0.2)

**Trigger:** User request — "full codebase audit, update all docs, document all issues, prepare for handoff."

**Scope:** Entire KaptureVault source — 39 `.cs` files / 11 `.axaml` (~6,142 LOC), installer, git history, and the existing docs. Read-only; no code changed.

**Methodology:** 5 specialized read-only agents run in parallel, each on a distinct dimension, then cross-validated and synthesized:
1. **Architecture / MVVM / lifecycle / threading** (architecture-mvvm)
2. **Data / SQLite / encryption / Drive sync / secret exposure** (data-persistence)
3. **Performance / memory / bitmaps / virtualization** (performance-profiling)
4. **Testing strategy & gap analysis** (testing-qa)
5. **Correctness bug hunt + documentation drift** (general-purpose)

**Findings:** 45 issues catalogued in `BUGS.md` (KV-001…KV-045): 🔴 4 Critical, 🟠 13 High, 🟡 16 Medium, ⚪ 10 Low, 📄 2 Doc/Process. Prioritized remediation in `ROADMAP.md` (T-01…T-33). Test strategy in `TESTING.md`.

**Headline findings:**
- 🔴 **KV-001** Live Google OAuth secrets in parent-repo git history + on disk, **unrevoked** — only human action (Cloud Console) can close it.
- 🔴 **KV-002** AES-GCM decrypt silently returns ciphertext on auth failure (integrity defeated).
- 🔴 **KV-003** Drive sync whole-DB last-write-wins → silent multi-device data loss.
- 🔴 **KV-004** Content search returns nothing when encryption is on (LIKE on ciphertext).
- 🟠 **KV-005** Self-exclusion broken (`SelfProcessName="Kapture"` ≠ runtime `KaptureVault`) — the app captures its own keystrokes/clipboard. **Trivial fix.**

**Verified sound (no action):** the v1.0.2 app/tag filter fix (diff-update + `_suppressFilterRefresh`); `KeyboardHookService` modifier/AltGr/dead-key handling; `HotkeyService` message-only window + STA pump; `CloudTokenStore` DPAPI usage and `drive.file` minimal scope; timer reentrancy guards across all three capture services; `ScreenshotService` DIB offset math and correct self-process name.

### Documentation reconciliation
- **`CLAUDE.md` (local)** — was pervasively stale (described the pre-fork 8-tab "Kapture" v1.0.27 with `requireAdministrator`, `SystemTweaks/`, mutex `…B7E3F4A2`, `%LOCALAPPDATA%\Kapture`). **Rewritten** this session to vault-only reality (v1.0.2, `asInvoker`, mutex `…C9D2E5F6`, `%LOCALAPPDATA%\KaptureVault`). Logged as KV-044 (FIXED).
- **Parent `Development/CLAUDE.md`** — confirmed to be a generic unfilled `REPLACE_WITH_*` constitution template; not KaptureVault-specific, left as-is.
- **Created this session:** `docs/BUGS.md`, `docs/ROADMAP.md`, `docs/TESTING.md`, `docs/AUDIT-LOG.md`, `docs/HANDOFF.md`. Project memory files updated (overview/state/patterns).

### Doc set versioning
This is the first managed documentation set for KaptureVault — doc-version **1.0.0**, app-version **1.0.2**. All `docs/*.md` carry YAML frontmatter per the project constitution.

**Auditor:** Claude (Opus 4.8) orchestrating 5 sub-agents. **Next audit due:** after P0/P1 remediation, or before the next minor release.
