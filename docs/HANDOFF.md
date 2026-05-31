---
document: HANDOFF
version: 1.4.0
app-version: 1.0.4
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
3. **Build the new features (current focus):** start with **F-01 — Export vault DB to local disk** (quick win, test-first), then decide on **F-02 — paid Online Vault** (design-in-full or scaffold Phase 1 backend). See `ROADMAP.md → 🚀 FEATURE ROADMAP`. The P1 remediation backlog continues in parallel/after. Work **test-first** (RED→GREEN); end with a handoff.

## TL;DR

KaptureVault is the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, with settings, optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, and a screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. **Released: v1.0.4** (latest). Clean tree, builds 0/0, **30 tests passing**.

A full audit landed 2026-05-30 (45 issues). **All P0 fixed + shipped in v1.0.3** (incl. OAuth rotation + history purge). **P1 in progress:** first batch shipped in v1.0.4; the bigger threading/lifecycle items remain before wide distribution.

## Current state

- **Branch:** `main` @ `release: v1.0.4` (released, workflow-created). Next work goes on top → cut **v1.0.5** when ready (say "release it").
- **Build/test:** `dotnet build -c Debug` → clean; `dotnet test` → **30/30 green**.
- **Tests:** `KaptureVault.Tests` (xUnit + NSubstitute + FluentAssertions + coverlet) on `KaptureVault.slnx`. 6 suites; seams: base-dir (`EncryptionService`), connection-string (`DatabaseService`). Inventory in `TESTING.md`.
- **Release (3-stage, single creator):** `scripts/Invoke-Release.ps1` builds/packages/version-bumps/commits-CHANGELOG/**pushes** (it no longer creates the release). The pushed `releases/latest/*.exe` triggers `.github/workflows/auto-release.yml`, which VirusTotal-scans and **creates the GitHub Release** (~30 s). The `kapture.tools` site reads the latest release + `CHANGELOG.md` live from GitHub (`download.js`/`changelog.js`) — nothing pushed to it. **Do not re-add `gh release create` to the script** (it would race the workflow). Stable URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.
- **Security:** all OAuth secrets rotated; `Utilities` history purged + verified clean. New creds in gitignored `client_secret.json` / `kaptureweb_clientsecret.json` + `%LOCALAPPDATA%`. Desktop client ID `…15r8pqq8…`. **Residual:** the new secret is still bundled in the installer → close via T-12 before wide release.
- **Mobile viewer:** static web app `docs/vault/` (→ `kapture.tools/vault`). Needs the web client ID `…70gd1j2j…` pasted into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **Docs:** all managed docs reconciled at shared `version` 1.4.0 (app 1.0.4), cross-linked via `see-also`.
- **Resolved incident (not a KaptureVault bug):** a report blamed KaptureVault for an `AlfaFF.sys` network filter taking the machine offline (browser TLS dropped). **Verified false** — KaptureVault has *zero* network/WFP/driver code (it only hooks keyboard/clipboard/screenshot, runs `asInvoker`). Root cause was a **third-party surveillance product** ("Monitoring Software" by PCM, paycomputermonitoring.com) that bundles AlfaFF. No KaptureVault change. Detail in `AUDIT-LOG.md` (PM-4). *If a "network capture filter" request resurfaces — it isn't ours.*

## What shipped / done recently

- **P1 (done, unreleased on `main` → v1.0.4):** ✅ KV-008 (consistent DB gate), KV-009 (name-based column reads), KV-014/023/018 (annotation editor leaks + SaveAs guard), KV-013 partial (cached row brushes + 1000-row entry cap). Tests 10 → **30**.
- **v1.0.3 (P0 + security):** self-exclusion (KV-005/034), decrypt-integrity (KV-002), encrypted-search (KV-004), pre-sync backup retention (KV-003), test harness, **OAuth rotation + history purge** (KV-001).
- **v1.0.1–1.0.2:** Capture Admin Apps, About dialog, screenshot save-as-image + annotation editor, BMP installer icon, `kapture.tools` wiring, release automation, sidebar filter fix, mobile vault viewer, full audit + `docs/` set.

## ⚠️ Top priorities for the next session — FEATURES (from `ROADMAP.md → 🚀 FEATURE ROADMAP`)

The user's stated next focus is the two new features. Lead with these:

1. **F-01 — Export vault DB to local disk** *(free tier, ~hours, START HERE).* Settings button → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy(path)` (already exists, `VACUUM INTO`). Test-first. Quick win for momentum.
2. **F-02 — Paid "Online Vault" (accounts + Cloudflare R2 + file hosting, $49/yr)** *(epic).* Decide first: **design F-02 in full**, or **scaffold Phase 1 backend** (new repo: Worker + R2 + D1 + Stripe + auth). Three non-negotiables: per-user *namespace* (not bucket-per-user); one feature-gated app (not two versions); **no storage/Stripe secrets in the client** — backend brokers presigned URLs (this makes **T-12** a prerequisite). Full breakdown + phases in `ROADMAP.md`.

### Also outstanding — audit-remediation backlog (tech debt; do alongside/after features, *before wide distribution*)
- **T-07 / KV-012** — move the SQLite INSERT off the keyboard-hook thread (top latency risk; riskiest change).
- **T-08 / KV-011/010/024** — centralize shutdown/teardown (dispose tray + HotkeyService + ServiceProvider; SyncOnClose once).
- **T-09 remainder / KV-013/032/033** — diff-update `Entries`, debounce `Refresh()`, decrypt off the UI thread.
- **T-12 / KV-007/006** — secret-less OAuth + stop bundling `client_secret.json`; PBKDF2≥600k/Argon2id. **Prerequisite for F-02's backend security model.**
- **T-16 / KV-045** — headless smoke tests, VM filter regression test, CI test job.

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
