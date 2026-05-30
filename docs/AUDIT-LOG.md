---
document: AUDIT-LOG
doc-version: 1.1.0
app-version: 1.0.3
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: codebase-audit
---

# AUDIT-LOG.md — KaptureVault

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
