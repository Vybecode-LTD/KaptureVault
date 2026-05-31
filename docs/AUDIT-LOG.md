---
document: AUDIT-LOG
version: 1.4.0
app-version: 1.0.4
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/BUGS.md, docs/ROADMAP.md, docs/TESTING.md, docs/HANDOFF.md]
---

# AUDIT-LOG.md — KaptureVault

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
