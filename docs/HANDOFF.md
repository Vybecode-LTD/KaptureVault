---
document: HANDOFF
doc-version: 1.0.0
app-version: 1.0.2
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: codebase-audit
---

# HANDOFF.md — KaptureVault

> Read this first when picking up the project. Pairs with `CLAUDE.md` (project facts), `BUGS.md` (issue register), `ROADMAP.md` (fix order), `TESTING.md` (test plan).

## TL;DR

KaptureVault is the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, with settings, optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, and a screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. Currently **v1.0.2**, on `main`, clean working tree, building 0 warnings / 0 errors.

A **full audit landed 2026-05-30** (45 issues). The code *runs well* but has real **security and data-integrity gaps** that must be addressed before wider distribution. Nothing from the audit has been fixed yet.

## Current state

- **Branch:** `main` @ `15c3889` (`release: v1.0.2`), pushed, GitHub release live.
- **Build:** `dotnet build -c Debug` → clean. App runs.
- **Release:** `scripts/Invoke-Release.ps1 -BumpType minor|major` does version-bump → publish → Inno Setup → copy to `releases/latest/` → commit (incl. `CHANGELOG.md`) → tag → push → `gh release create`. Stable download URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.
- **Mobile viewer:** static web app at `docs/vault/` (served at `kapture.tools/vault`); read-only Drive + in-browser SQLite/WebCrypto. Needs a Web OAuth client ID pasted in (`docs/vault/index.html`, `GOOGLE_WEB_CLIENT_ID`).
- **Tests:** none (0%). See `TESTING.md`.

## What shipped this session

1. Capture Admin Apps setting (opt-in self-elevation) — `Program.cs` elevation check, `SettingsWindow` restart logic.
2. About dialog (`?` button).
3. Screenshot **Save as image** (PNG/JPEG/BMP via SkiaSharp) + **annotation editor** (`ScreenshotEditorWindow`); pen-tool redraw fix.
4. Installer icon fixed (BMP-frame `app.ico`).
5. `kapture.tools` domain wiring (CNAME, canonical metas).
6. Release automation + `CHANGELOG.md`.
7. App/tag **sidebar filter selection fix** (diff-update instead of `Clear()`).
8. Mobile vault viewer web app.
9. v1.0.1 and v1.0.2 releases.
10. **This audit** + the full `docs/` set + `CLAUDE.md` rewrite.

## ⚠️ Top priorities for the next session (from `ROADMAP.md`)

**Must-do before any wider release (P0):**
1. **T-01 — Revoke + rotate the 3 Google OAuth secrets in Cloud Console.** Human-only; Claude can't. The old history secret was never revoked. Most urgent item in the project.
2. **T-02 — Purge the secret from the parent `Utilities` repo git history** and confirm repo visibility.
3. **T-03 — Fix self-exclusion (KV-005):** one-line — `SelfProcessName = "KaptureVault"` in `CaptureService.cs:13` and `ClipboardMonitorService.cs:13`. The app is currently logging its own keystrokes/clipboard. **Easiest high-value win — start here.**
4. **T-04 — Stop swallowing decrypt failures (KV-002).**
5. **T-05 — Fix content search under encryption (KV-004).**
6. **T-06 — Address Drive multi-device data loss (KV-003)** — at minimum document "single-device only" + keep the pre-sync backup.

**Then P1:** move DB writes off the hook thread (KV-012), centralize shutdown/teardown (KV-011), virtualize the entry list (KV-013), DI for HotkeyService + ViewModels (KV-010), bump PBKDF2 / secret-less OAuth (KV-006/007), stand up the test project (KV-045).

## Blockers / things only the human can do

- **Google Cloud Console:** revoke/rotate secrets (T-01), reconfigure desktop client as secret-less native+PKCE (T-12), and finish OAuth consent screen for `kapture.tools` (authorized domain + TOS/Privacy URLs, exit Testing mode).
- **DNS / GitHub Pages:** point `kapture.tools` at Pages (A records + CNAME `www`), enable HTTPS — see `project-kapture-state` memory for the exact records.
- **Git history rewrite + force-push** on the parent repo (destructive — confirm before doing).

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
