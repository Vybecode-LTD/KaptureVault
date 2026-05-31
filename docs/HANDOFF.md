---
document: HANDOFF
version: 1.6.0
app-version: 1.0.5
last-updated: 2026-05-31
last-audit: 2026-05-31
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/ROADMAP.md, docs/BUGS.md, docs/TESTING.md, docs/AUDIT-LOG.md, CHANGELOG.md, docs/F-02-online-vault-design.md]
---

# HANDOFF.md — KaptureVault

> **Canary doc — read first when picking up the project.** Pairs with `CLAUDE.md` (project facts + **standing directives**), `ROADMAP.md` (all to-dos), `BUGS.md` (issue register), `TESTING.md` (test plan), `AUDIT-LOG.md` (history), `F-02-online-vault-design.md` (paid-tier design).

## ▶ Start here (fresh session) — DO THIS FIRST

This repo lives on a OneDrive path; tooling can return stale/fabricated output (see ⚠️ below). Establish ground truth with these exact commands and compare to expected values **before trusting anything**:

```powershell
cd C:\Users\vybec\OneDrive\Documents\Development\Utilities\KaptureVault
git ls-remote origin refs/heads/main refs/tags/v1.0.5   # main expected = d92952d; tag v1.0.5 should exist
git status --porcelain                                   # expect CLEAN
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 47 passing
```

**If those match, the repo is healthy** (v1.0.5 shipped, tree clean) — proceed to "Next moves". If they don't, the tooling may be lying; reconcile against the "Recent commit stack" below before editing anything.

## TL;DR

KaptureVault = the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. **v1.0.5 SHIPPED** (P1 hardening batch 2 + format-debt closure + CI format/vuln gates). `dotnet test` → **47 passing**; build Debug+Release **0/0**. HEAD `d92952d` = `origin/main` (verified), tree clean. **KV-007 (secret-less OAuth) deferred to F-02** (backend broker).

## ⚠️ CRITICAL — environment hazard (OneDrive path)

The repo lives on a **OneDrive path** and the tool harness has repeatedly returned **stale, delayed, fabricated, or empty** tool output — including reporting commits, files, a CI workflow, and a `git push` as DONE when none had happened, and going **dark (empty results)** on the read/verify channel mid-task while `Write`/`git commit` kept working.

**Mitigations that worked:** (1) verify every consequential action with a SECOND authoritative command before relying on it — *especially git* (`git log` / `git status` / `git ls-remote` / `Test-Path`); (2) `Write` (full-file overwrite) is safe blind and safe to repeat; (3) targeted `Edit` is safe-on-failure (errors, never corrupts) but needs an in-session `Read` first; (4) run ONE PowerShell per turn (a non-zero exit cancels parallel siblings); (5) build+test after every change. **The actual filesystem/git/build/test operations were sound — only result *reporting* was unreliable.**
**STRONGLY consider cloning the repo to a non-OneDrive path (e.g. `C:\dev\KaptureVault`) for the next session** — faster, and removes the most likely root cause.

## ✅ What shipped this session (2026-05-31)

- **v1.0.5 RELEASED** — tag `v1.0.5` = commit `ffc3c9d`; GitHub Release created by `auto-release.yml` (the single release creator). CHANGELOG has a `[1.0.5] — 2026-05-31` section. This shipped the **P1 hardening batch 2**:
  - **T-07 / KV-012** — SQLite INSERT moved **off** the WH_KEYBOARD_LL hook thread: `CaptureService.Flush()` `TryWrite`s to a bounded `Channel<CaptureEntry>`; a single writer task does the INSERT off-thread; `Stop()` drains it.
  - **T-11 / KV-006** — PBKDF2-HMAC-SHA256 → **600k** (OWASP 2023 floor) + **persisted KDF params** (`Iterations`, `Kdf`); legacy vaults (no stored count) default to 100k so they still unlock.
  - **T-10 / KV-010** — `HotkeyService` + `MainWindowViewModel` resolved from **DI** (composition root in `ServiceRegistration.cs`) instead of `new`'d in `App`.
- **Format debt CLOSED + CI-gated** — app `51dc9fd` + tests `12b7122` `dotnet format`-clean; `tests.yml` CI now runs `dotnet test` + `dotnet format --verify-no-changes` + `dotnet list package --vulnerable`, **verified GREEN** (run 26725669973). This completes the **format/vuln half of T-16**.
- **KV-007 / T-12 decision** — **DEFER** secret-less OAuth to **F-02 Phase 1** (the backend broker removes the client secret entirely). Installer keeps bundling `client_secret.json` in the meantime.

## Recent commit stack (`origin/main`, 2026-05-31 — verify with `git log --oneline`)

Newest first; HEAD = `origin/main` (verified via `git ls-remote`). **If yours differ, trust `git log`, not this table** (tooling has fabricated SHAs before — see ⚠️).

| Commit | What |
|---|---|
| `d92952d` | ci: format + vuln gates in tests.yml *(HEAD = origin/main)* |
| `12b7122` | chore: format tests project |
| `ffc3c9d` | release: v1.0.5 *(tag v1.0.5 here)* |
| `51dc9fd` | chore: format app project |
| `b08ae0a` | _(v1.0.5 batch)_ |
| `a89ea13` | _(v1.0.5 batch)_ |
| `0351500` | refactor(di): HotkeyService + MainWindowViewModel in DI (T-10, KV-010) |
| `5748f9f` | fix(crypto): PBKDF2 → 600k + persisted KDF params (T-11, KV-006) |
| `e5977dd` | fix(capture): SQLite INSERT off the keyboard-hook thread (T-07, KV-012) |

## Next moves (recommended order)

1. **T-16 remainder** — `Avalonia.Headless.XUnit` smoke test + a `MainWindowViewModel` filter-selection regression test. **Do FIRST** — it's the harness that makes T-09/T-08 verifiable. (Format/vuln half is already done + CI-gated.)
2. **T-09 / KV-013/032/033** — **diff-update** `Entries` (NEVER `Clear()` a selection-bound list — see Lessons), debounce `Refresh()`, decrypt off the UI thread. `MainWindowViewModel.cs`; AppList/TagList already diff-update — replicate for Entries.
3. **T-08 / KV-011/024** — **centralize shutdown/teardown.** Only the tray Quit handler stops services; four other `Shutdown()` paths bypass it, and `ServiceProvider` is never disposed. Fix: one **idempotent `TeardownAsync`** (via `ShutdownRequested`) that disposes `HotkeyService` + `ServiceProvider`; bounded/background sync-on-close so shutdown can't hang. **Pair with T-16** for verifiability.
4. **F-01** — Settings → "Export Vault Database…" → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy(path)` (already exists, `VACUUM INTO`). Test-first; quick win.
5. **F-02** — paid **Online Vault** (R2 + Workers + D1 + Stripe; `docs/F-02-online-vault-design.md`). **Now ABSORBS T-12/KV-007:** the backend broker removes the bundled client secret, so secret-less OAuth ships as part of F-02 Phase 1 rather than as a standalone desktop change.

## Release pipeline reminder (unchanged)

`Invoke-Release.ps1` builds/packages/bumps/commits-CHANGELOG/**pushes** — it does **NOT** create the GitHub release. The pushed `releases/latest/*.exe` triggers `.github/workflows/auto-release.yml` (the **single** release creator) → VirusTotal scan + GitHub Release. `kapture.tools` reads release + CHANGELOG live from GitHub. **Never re-add `gh release create` to the script.** Stable URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.

## Blockers / human-only (carried over)

- **Google Cloud Console:** finish the OAuth consent screen for `kapture.tools` (authorized domain + TOS/Privacy URLs, exit Testing mode). **Secret-less OAuth now lands via the F-02 backend broker (KV-007)** — the Cloud Console reconfigure happens as part of that backend work, not standalone.
- **GitHub Pages + DNS:** point `kapture.tools` at Pages (A `@` → 185.199.108–111.153; CNAME `www` → `vybecode-ltd.github.io`), enforce HTTPS; verify in Google Search Console.
- **Mobile viewer:** paste the web client ID `…70gd1j2j…` into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **Repo hygiene:** move the repo off the OneDrive path (see ⚠️ above).

## Build / run quick reference

```powershell
dotnet build -c Debug
.\bin\Debug\net9.0-windows\win-x64\KaptureVault.exe   # kill any running (maybe elevated) instance first
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 47 passing
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`.
