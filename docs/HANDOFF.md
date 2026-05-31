---
document: HANDOFF
version: 1.5.0
app-version: 1.0.4
last-updated: 2026-05-31
last-audit: 2026-05-31
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/ROADMAP.md, docs/BUGS.md, docs/TESTING.md, docs/AUDIT-LOG.md, CHANGELOG.md, docs/F-02-online-vault-design.md]
---

# HANDOFF.md — KaptureVault

> **Canary doc — read first when picking up the project.** Pairs with `CLAUDE.md` (project facts + **standing directives**), `ROADMAP.md` (all to-dos), `BUGS.md` (issue register), `TESTING.md` (test plan), `AUDIT-LOG.md` (history), `F-02-online-vault-design.md` (paid-tier design).

## ▶ Start here (fresh session) — DO THIS FIRST

The 2026-05-31 session hit a **flaky tooling environment** (see ⚠️ below). Before trusting anything, establish ground truth with these exact commands and compare to the expected values:

```powershell
cd C:\Users\vybec\OneDrive\Documents\Development\Utilities\KaptureVault
git log --oneline -10          # expect the 7-commit stack listed under "Commit stack" below
git status --porcelain         # expect CLEAN (or see "Known loose ends")
git rev-list --count origin/main..HEAD   # expect 7 if NOT yet pushed, 0 if pushed
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # expect 47 passing
```

**If those match, the code is healthy** — proceed to "Next moves". If they don't, the tooling may have left something half-done; reconcile against the "Commit stack" list before editing anything.

## TL;DR

KaptureVault = the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, with optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, and a screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. **Released tip: v1.0.4** (on GitHub). Local `main` has **7 unpushed commits** of P1 hardening + design docs (this session). `dotnet test` → **47 passing** (was 30).

## ⚠️ CRITICAL — environment hazard (2026-05-31)

The repo lives on a **OneDrive path** and the tool harness this session repeatedly returned **stale, delayed, fabricated, or empty tool output**:
- Last session (05-31 part 1) it reported commits, a CI workflow file, an F-02 doc, and a `git push` as DONE — **none of which had actually happened.** Verified false at the start of this session via `git log`/`git status`/`Test-Path`.
- This session the read/verify channel (Read, Bash `sed`, some PowerShell) went **dark (empty results)** mid-task, while `Write` and `git commit` via PowerShell kept working.

**Mitigations that worked:** (1) verify every consequential action with a SECOND authoritative command before relying on it — *especially git*; (2) `Write` (full-file overwrite) is safe to use blind and safe to repeat; (3) targeted `Edit` is safe-on-failure (errors, never corrupts); (4) run ONE PowerShell per turn (a non-zero exit cancels parallel siblings); (5) build+test after every change. **The actual filesystem/git/build/test operations were sound — only result *reporting* was unreliable.**
**STRONGLY consider cloning the repo to a non-OneDrive path (e.g. `C:\dev\KaptureVault`) for the next session.** It will make everything faster and removes the most likely root cause.

## Commit stack (local `main`, 2026-05-31 — verify with `git log --oneline -10`)

Newest first; everything above `bec5d2b` is this session's work:

| Commit | What |
|---|---|
| `b9c99d2` | docs(handoff): this canary rewrite _(HEAD)_ |
| `b57b22d` | docs(design): F-02 paid Online Vault full design (`docs/F-02-online-vault-design.md`) |
| `24cd3f2` | ci: Tests workflow (`.github/workflows/tests.yml`) — T-16 partial |
| `0351500` | refactor(di): register HotkeyService + MainWindowViewModel in DI (T-10, KV-010) |
| `60a89ca` | docs: mark KV-012/T-07 + KV-006/T-11 fixed; resequence P1 |
| `5748f9f` | fix(crypto): PBKDF2 → 600k + persisted KDF params (T-11, KV-006) |
| `e5977dd` | fix(capture): SQLite INSERT off the keyboard-hook thread (T-07, KV-012) |
| `c487d66` | chore: remove stray .orig backups; ignore *.orig/*.rej |
| `bec5d2b` | _(origin/main tip — last pushed; session start)_ |

> **8 commits ahead of `origin/main`, unpushed.** SHAs above are from this session's verified `git log` — if yours differ, trust `git log`, not this table. (An earlier 05-31 attempt reported SHAs that were never real.)

## ✅ What this session delivered (all test-first, RED→GREEN where code)

- **T-07 / KV-012** — `CaptureService.Flush()` no longer does SQLite Open()+INSERT+AES on the WH_KEYBOARD_LL hook thread. It enqueues to a bounded `Channel<CaptureEntry>` (non-blocking `TryWrite`, `AllowSynchronousContinuations=false`); a single writer task (`ProcessWriteQueueAsync`, started in `Start()`) does the write off-thread. `Stop()` completes + drains (≤5 s) so the final buffered entry isn't lost. Tests: `Flush_DoesNotBlockTheHookThreadOnTheDatabaseWrite`, `Stop_DrainsBufferedEntriesAndDoesNotLoseData`.
- **T-11 / KV-006** — new vaults derive at **600k** PBKDF2-HMAC-SHA256 (OWASP 2023 floor); `encryption.json` persists KDF params (`Iterations`, `Kdf`). `Unlock` derives with the stored count, **defaulting pre-T-11 files (no count) to 100k so existing vaults still open** (no lockout). Tests: `Configure_StoresStrongKdfParams_AndDerivesWithThem`, `Unlock_LegacyVaultWithoutStoredIterations_StillUnlocksAndDecrypts`. *Deferred:* re-keying legacy vaults (needs the transactional bulk path KV-021/T-20) + Argon2id.
- **T-10 / KV-010** — composition root extracted to `ServiceRegistration.AddKaptureServices()` (new file `ServiceRegistration.cs`); `HotkeyService` + `MainWindowViewModel` now resolved from DI instead of `new`'d in `App`. `MainWindowViewModel` uses an explicit factory mirroring the original ctor (it has a design-time ctor too) → behavior-identical. New `ServiceRegistrationTests`. *Residual:* a few View code-behinds still use `App.Services` as a locator (KV-015) — left for the larger T-22 VM extraction.
- **T-16 (partial) / KV-045** — `.github/workflows/tests.yml`: runs `dotnet test` on every code push/PR to main (windows-latest, .NET 9, TRX + Cobertura coverage; ignores docs/releases/installer). **Not yet pushed → has not run on GitHub yet.**
- **F-02 design** — complete paid-tier design doc (`docs/F-02-online-vault-design.md`): R2 + Workers + D1 + Stripe, per-user namespace, no secrets in client, data model, Worker API, 4 phases, costs, ops/legal, risks.
- Tests **30 → 47**. Build 0/0 Debug + Release. App.axaml.cs whitespace lint fixed.

## 📌 PINNED DECISION — next release is v1.0.5 (user-confirmed 2026-05-31)

**Cut v1.0.5 next session** with the P1 hardening batch (T-07 + T-11 + T-10), mirroring how v1.0.4 shipped "P1 batch 1." F-01 (DB export) becomes a later feature release — do **not** block v1.0.5 on it.
**To do it:** (1) finish the doc reconciliation below; (2) promote the `CHANGELOG.md [Unreleased]` block to a `[1.0.5]` section (bold-lead bullets — see release directive); (3) `git push origin main` first (so the hardening is on GitHub); (4) say **"release it"** → `Invoke-Release.ps1 -BumpType minor`. Pre-flight: kill any running (possibly elevated) `KaptureVault.exe`; confirm `dotnet test` green.

## 🚧 Known loose ends / doc reconciliation NOT finished (tooling went dark mid-reconcile)

The **code is fully committed**; the **doc reconciliation was interrupted** by the empty-tool-output failure. Still TODO (do these first next session, when Read works):
- **CHANGELOG.md** — stage the `[Unreleased]` → v1.0.5 entries (T-07/T-11/T-10), bold-lead format.
- **BUGS.md** — KV-012 + KV-006 already flipped to FIXED (commit `60a89ca`). Still need: **KV-010 → FIXED (T-10)**, KV-045 note (CI added, headless tests pending), snapshot test count **34 → 47**.
- **ROADMAP.md** — T-07/T-11 already done. Still need: **T-10 row → ✅ done**, T-16 row → "🟡 CI added; headless+VM regression pending", test count → 47.
- **TESTING.md** — test count **30 → 47**; add new test files: `ServiceRegistrationTests`, the new `CaptureServiceTests`/`EncryptionServiceTests` cases; note the new `tests.yml` CI job.
- **CLAUDE.md** — add a 2026-05-31 Session Log entry; update Health & Known Issues (KV-012/006/010 done); the doc-map "version" line.
- **AUDIT-LOG.md** — add the 2026-05-31 reconciliation entry (+ the tooling-hazard incident).
- **Managed-doc version bump:** all → **1.5.0** (this HANDOFF already set to 1.5.0; bring the rest in line). App-version stays 1.0.4 until v1.0.5 is actually cut.

## Next moves (recommended order)

1. **Re-verify** (the Start-here block) — confirm 7 commits + 47 tests.
2. **Finish doc reconciliation** (the loose-ends list) + **push** the 7 commits.
3. **Cut v1.0.5** (pinned decision) — or do step 4 first if you'd rather pair a feature with it.
4. **T-16 remainder** — `Avalonia.Headless.XUnit` smoke test + a `MainWindowViewModel` filter-selection regression test. **Do before T-09/T-08** — it's the harness that makes them verifiable.
5. **T-09 / KV-013/032/033** — diff-update `Entries` (NEVER `Clear()` a selection-bound list — see Lessons), debounce `Refresh()`, decrypt off the UI thread. `MainWindowViewModel.cs`; AppList/TagList already diff-update — replicate for Entries.
6. **T-08 / KV-011/010/024** — centralize teardown. Only the tray Quit handler (`App.axaml.cs:266-288`) stops services + SyncOnClose + disposes tray; **four `Shutdown()` paths bypass it**: encryption-cancel (`App.axaml.cs:84`), `SettingsWindow.RestartElevated` (~:236), UAC-cancel (~:249), `RestartNormal` (~:277). `ServiceProvider` never disposed (KV-024). Fix: one idempotent `TeardownAsync` via `ShutdownRequested`; bounded/background sync-on-close so shutdown can't hang. **Pair with T-16** for verifiability.
7. **T-12 / KV-007** (RISKY — secrets) — secret-less desktop OAuth (native + loopback PKCE), stop bundling `client_secret.json`, remove `FallbackClientId`. Code part doable; Cloud Console reconfigure is human. **Prerequisite for F-02.**
8. **F-01** — Settings → "Export Vault Database…" → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy(path)` (already exists, `VACUUM INTO`). Test-first; quick win.

## Resequencing rationale (2026-05-31)

T-08 (shutdown) and T-09 (Entries diff-update — the `Clear()`+bound-`SelectedItem` minefield) are lifecycle/UI code with **no clean unit test + high regression risk**, so they're **paired after T-16's headless harness** so they can be verified, not hoped. T-11 was pulled early as a clean, self-contained win.

## Release pipeline reminder (unchanged)

`Invoke-Release.ps1` builds/packages/bumps/commits-CHANGELOG/**pushes** — it does **NOT** create the GitHub release. The pushed `releases/latest/*.exe` triggers `.github/workflows/auto-release.yml` (the **single** release creator) → VirusTotal scan + GitHub Release. `kapture.tools` reads release + CHANGELOG live from GitHub. **Never re-add `gh release create` to the script.** Stable URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.

## Blockers / human-only (carried over)

- **Google Cloud Console:** confirm old web secret deleted; reconfigure desktop client as secret-less native+PKCE (pairs with T-12); finish OAuth consent screen for `kapture.tools` (authorized domain + TOS/Privacy URLs, exit Testing mode).
- **GitHub Pages + DNS:** point `kapture.tools` at Pages (A `@` → 185.199.108–111.153; CNAME `www` → `vybecode-ltd.github.io`), enforce HTTPS; verify in Google Search Console.
- **Mobile viewer:** paste the web client ID `…70gd1j2j…` into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **Repo hygiene:** move repos off the OneDrive path (see ⚠️ above).

## Build / run quick reference

```powershell
dotnet build -c Debug
.\bin\Debug\net9.0-windows\win-x64\KaptureVault.exe   # kill any running (maybe elevated) instance first
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 47 passing
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Release.ps1 -BumpType minor   # cut v1.0.5
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`.
