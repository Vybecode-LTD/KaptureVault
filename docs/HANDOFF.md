---
document: HANDOFF
version: 1.2.0
app-version: 1.0.3
last-updated: 2026-05-30
last-audit: 2026-05-30
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/ROADMAP.md, docs/BUGS.md, docs/TESTING.md, docs/AUDIT-LOG.md, CHANGELOG.md]
---

# HANDOFF.md — KaptureVault

> **Canary doc — read first when picking up the project.** Pairs with `CLAUDE.md` (project facts + **standing directives**), `ROADMAP.md` (all to-dos), `BUGS.md` (issue register), `TESTING.md` (test plan), `AUDIT-LOG.md` (history).

## ▶ Start here (fresh session)
1. Read `CLAUDE.md` (esp. the **STANDING DIRECTIVES** section: testing / debugging / documentation / release) and this file.
2. Establish a green baseline: `dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj` → expect **30 passing**.
3. Pick up at **P1 / T-07** (see priorities below). Work **test-first** (RED→GREEN). End the session with a handoff.

## TL;DR

KaptureVault is the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, with settings, optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, and a screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. **Released: v1.0.3.** `main` has **unreleased P1 work** on top (→ v1.0.4). Clean tree, builds 0/0, **30 tests passing**.

A full audit landed 2026-05-30 (45 issues). **All P0 fixed + shipped in v1.0.3** (incl. OAuth rotation + history purge). **P1 in progress:** several reliability/robustness items done; the bigger threading/lifecycle items remain before wide distribution.

## Current state

- **Branch:** `main`. Released tip = `v1.0.3`; **6 unreleased P1 commits on top** (→ cut **v1.0.4** when ready: say "release it").
- **Build/test:** `dotnet build -c Debug` → clean; `dotnet test` → **30/30 green**.
- **Tests:** `KaptureVault.Tests` (xUnit + NSubstitute + FluentAssertions + coverlet) on `KaptureVault.slnx`. 6 suites; seams: base-dir (`EncryptionService`), connection-string (`DatabaseService`). Inventory in `TESTING.md`.
- **Release:** `scripts/Invoke-Release.ps1 -BumpType minor|major` (add a CHANGELOG entry first). Stable URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.
- **Security:** all OAuth secrets rotated; `Utilities` history purged + verified clean. New creds in gitignored `client_secret.json` / `kaptureweb_clientsecret.json` + `%LOCALAPPDATA%`. Desktop client ID `…15r8pqq8…`. **Residual:** the new secret is still bundled in the installer → close via T-12 before wide release.
- **Mobile viewer:** static web app `docs/vault/` (→ `kapture.tools/vault`). Needs the web client ID `…70gd1j2j…` pasted into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **Docs:** all managed docs reconciled at shared `version` 1.2.0 (app 1.0.3), cross-linked via `see-also`.

## What shipped / done recently

- **P1 (done, unreleased on `main` → v1.0.4):** ✅ KV-008 (consistent DB gate), KV-009 (name-based column reads), KV-014/023/018 (annotation editor leaks + SaveAs guard), KV-013 partial (cached row brushes + 1000-row entry cap). Tests 10 → **30**.
- **v1.0.3 (P0 + security):** self-exclusion (KV-005/034), decrypt-integrity (KV-002), encrypted-search (KV-004), pre-sync backup retention (KV-003), test harness, **OAuth rotation + history purge** (KV-001).
- **v1.0.1–1.0.2:** Capture Admin Apps, About dialog, screenshot save-as-image + annotation editor, BMP installer icon, `kapture.tools` wiring, release automation, sidebar filter fix, mobile vault viewer, full audit + `docs/` set.

## ⚠️ Top priorities for the next session — remaining P1 (from `ROADMAP.md`)

1. **T-07 / KV-012** — move the SQLite INSERT off the keyboard-hook thread (bounded `Channel` + writer task). **Biggest latency risk** (input stutter / hook eviction) and the riskiest change (rewires `CaptureService` threading + its tests) — give it focused attention.
2. **T-08 / KV-011, KV-010, KV-024** — centralize shutdown/teardown (`ShutdownRequested`/`OnExit`): stop all services, dispose tray + `HotkeyService` + `ServiceProvider`, run SyncOnClose once. Today only the tray-Quit path cleans up.
3. **T-09 remainder / KV-013, KV-032, KV-033** — diff-update `Entries` (apply the sidebar pattern, preserve order + selection), debounce `Refresh()`, move whole-table decrypt off the UI thread. (Brush caching + 1000-row cap already done.)
4. **T-12 / KV-007, KV-006** — secret-less desktop OAuth (native + loopback PKCE), stop bundling `client_secret.json`; bump PBKDF2 ≥600k / plan Argon2id (store KDF params in `encryption.json` so existing vaults still unlock). **Do before wide distribution.**
5. **T-16 / KV-045** — Avalonia headless smoke tests, the **VM filter regression** test, CI test job, wire `dotnet format` + vulnerable-scan into the loop.

When ready, **"release it"** to cut v1.0.4 with the P1 work.

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
