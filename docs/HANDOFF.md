---
document: HANDOFF
doc-version: 1.1.0
app-version: 1.0.3
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: codebase-audit
---

# HANDOFF.md — KaptureVault

> Read this first when picking up the project. Pairs with `CLAUDE.md` (project facts), `BUGS.md` (issue register), `ROADMAP.md` (fix order), `TESTING.md` (test plan).

## TL;DR

KaptureVault is the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, with settings, optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, and a screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. Currently **v1.0.3**, on `main`, clean working tree, building 0 warnings / 0 errors, **10 tests passing**.

A full audit landed 2026-05-30 (45 issues). **All P0 items are now fixed and shipped in v1.0.3** — including the OAuth secret rotation + git-history purge. The app is in good shape; remaining work is **P1** hardening (perf hot paths, lifecycle, secret-less OAuth) before wide distribution.

## Current state

- **Branch:** `main` @ `release: v1.0.3`, pushed, GitHub release live.
- **Build/test:** `dotnet build -c Debug` → clean; `dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj` → 10/10 green.
- **Release:** `scripts/Invoke-Release.ps1 -BumpType minor|major` → version-bump → publish → Inno Setup → copy to `releases/latest/` → commit (incl. `CHANGELOG.md`) → tag → push → `gh release create`. Convention: add a CHANGELOG entry first. Stable URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.
- **Tests:** `KaptureVault.Tests` (xUnit + NSubstitute + FluentAssertions) on `KaptureVault.slnx`. Seams: base-dir (`EncryptionService`), connection-string (`DatabaseService`). Coverage is narrow (P0 fixes only) — broaden per `TESTING.md`.
- **Security:** all OAuth secrets rotated; `Utilities` history purged + verified clean. New creds live in gitignored `client_secret.json` / `kaptureweb_clientsecret.json` + `%LOCALAPPDATA%`. Desktop client ID now `…15r8pqq8…`.
- **Mobile viewer:** static web app at `docs/vault/` (served at `kapture.tools/vault`). Still needs the Web OAuth client ID pasted into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID` → the web client `…70gd1j2j…`).

## What shipped recently

**v1.0.3 (P0 remediation + security):** self-exclusion fix (KV-005/034), decrypt-integrity (KV-002), encrypted-search (KV-004), pre-sync backup retention (KV-003), test harness (10 tests), **OAuth secret rotation + `Utilities` history purge** (KV-001).
**v1.0.1–1.0.2:** Capture Admin Apps, About dialog, screenshot save-as-image + annotation editor, BMP installer icon, `kapture.tools` wiring, release automation, sidebar filter fix, mobile vault viewer, full audit + `docs/` set + `CLAUDE.md` rewrite.

## ⚠️ Top priorities for the next session — P1 (from `ROADMAP.md`)

All P0 done. Recommended P1 order:
1. **T-07 / KV-012** — move the SQLite INSERT off the keyboard-hook thread (bounded `Channel` + writer task). Current biggest latency risk (input stutter / hook eviction).
2. **T-08 / KV-011, KV-010, KV-024** — centralize shutdown/teardown (`ShutdownRequested`/`OnExit`): stop all services, dispose tray + `HotkeyService` + `ServiceProvider`, run SyncOnClose once. Today only the tray-Quit path cleans up.
3. **T-09 / KV-013, KV-032, KV-033** — make the entry `ListBox` virtualize: `LIMIT`/paging, diff-update `Entries` (same pattern already used for the sidebar lists), cache converter brushes.
4. **T-12 / KV-007, KV-006** — reconfigure the desktop OAuth client as **secret-less** (native + loopback PKCE) and stop bundling `client_secret.json`; bump PBKDF2 ≥600k / plan Argon2id. **Do before wide distribution** — closes the residual exposure from KV-001.
5. **T-16 / KV-045** — broaden tests (LanguageDetector, AppSettings, CaptureEntry, converters, the filter regression) + add a CI test job.

## Blockers / things only the human can do

- **Google Cloud Console:** confirm the OLD web secret is deleted (desktop client was recreated, so its old secret is dead); reconfigure desktop client as secret-less native+PKCE (T-12); finish the OAuth consent screen for `kapture.tools` (authorized domain + TOS/Privacy URLs, exit Testing mode).
- **DNS / GitHub Pages:** point `kapture.tools` at Pages (A records + CNAME `www`), enable HTTPS — exact records in the `project-kapture-state` memory.
- **Mobile viewer:** paste the web client ID `…70gd1j2j…` into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **OneDrive caution:** repos live on a OneDrive path; let it finish syncing before git ops (it can corrupt `.git`). Consider moving repos off OneDrive.

## Gotchas (learned this session)

- **Running app locks the build output.** The single-instance app hides to tray; a stale instance will block `dotnet build`/publish with MSB3027. Kill `KaptureVault.exe` first. It may be running **elevated** (Capture Admin Apps) → `Stop-Process` needs an elevated shell, or restore the hidden window via Win32 `ShowWindow`.
- **Secrets on disk are gitignored but real.** `client_secret*.json`, `*clientsecret*.json` patterns cover them. Never `git add -A` without checking `git diff --cached --name-only` for secrets first (done this session).
- **PowerShell 5.1 mangles non-ASCII** in scripts — keep `.ps1` files ASCII-only (the release script was rewritten for this).
- **Avalonia `ListBox.Clear()` + two-way `SelectedItem`** posts a *deferred* null back to the bound property → the filter-selection bug. Fix pattern: diff-update the collection, never `Clear()` a list whose selection is bound. (Already applied to `AppList`/`TagList`; **`Entries` still uses Clear()+rebuild — KV-013.**)

## Build / run / release quick reference

```powershell
# Build & run (kill any running instance first)
dotnet build -c Debug
.\bin\Debug\net9.0-windows\win-x64\KaptureVault.exe

# Full release (bump + package + push + GitHub release)
powershell -ExecutionPolicy Bypass -File scripts\Invoke-Release.ps1 -BumpType minor
```

Inno Setup: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe` · installer script `installer/KaptureVaultSetup.iss`.
