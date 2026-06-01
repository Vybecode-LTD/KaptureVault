---
document: HANDOFF
version: 1.9.0
app-version: 1.0.7
last-updated: 2026-06-01
last-audit: 2026-06-01
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/ROADMAP.md, docs/BUGS.md, docs/TESTING.md, docs/AUDIT-LOG.md, CHANGELOG.md, docs/F-02-online-vault-design.md]
---

# HANDOFF.md — KaptureVault

> **Canary doc — read first when picking up the project.** Pairs with `CLAUDE.md` (project facts + **standing directives**), `ROADMAP.md` (all to-dos), `BUGS.md` (issue register), `TESTING.md` (test plan), `AUDIT-LOG.md` (history), `F-02-online-vault-design.md` (paid-tier design).

## ▶ Start here (fresh session) — DO THIS FIRST

**The client repo was moved off OneDrive to `C:\DEV\Utilities\KaptureVault` on 2026-06-01** (joining `kapturevault-backend` under `C:\dev`), which retires the OneDrive tooling hazard noted below. Establish ground truth with these exact commands:

```powershell
cd C:\DEV\Utilities\KaptureVault
git status --porcelain                                   # expect CLEAN
git ls-remote origin refs/heads/main                     # should equal your local HEAD (git log -1)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 71 passing
```

**If those match, the client repo is healthy** (v1.0.7 shipped — it shipped the P1 backlog; tree clean) — proceed to "Next moves".

**Backend lives in a SEPARATE repo** (off OneDrive): `C:\dev\kapturevault-backend` (own git + GitHub remote `github.com/Vybecode-LTD/kapturevault-backend`, private). Verify with `npm test` there → **19 vitest passing**. See "Related repos / human prereqs".

## TL;DR

KaptureVault = the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. **Repo lives at `C:\DEV\Utilities\KaptureVault` (off OneDrive); public repo.** **Latest release: v1.0.7** (tag `v1.0.7` = `2d09aa3`) — it shipped the **P1 audit backlog**: T-16 (Avalonia.Headless.XUnit harness + VM regressions), T-09 (Entries diff-update + debounce + off-UI decrypt), T-08 (centralized shutdown teardown); **v1.0.6** before it shipped **F-01 (Export Vault Database)**. **All P0 + P1 audit issues resolved.** **F-02 Phase 1 backend DONE** in the separate `kapturevault-backend` repo. Client `dotnet test` → **71 passing**; build Debug+Release **0/0**; format+vuln CI gates green. HEAD = `origin/main`, tree clean. **Next: F-02 Phase 2** (client Online Vault) and/or the P2 backlog.

## ✅ RESOLVED — former OneDrive hazard (repo moved 2026-06-01)

**The client repo was moved off OneDrive to `C:\DEV\Utilities\KaptureVault` on 2026-06-01, so this hazard no longer applies.** Kept here as history. While the repo lived on a OneDrive path, the tool harness repeatedly returned **stale, delayed, fabricated, or empty** tool output — including reporting commits, files, a CI workflow, and a `git push` as DONE when none had happened, and going **dark (empty results)** on the read/verify channel mid-task while `Write`/`git commit` kept working.

**Mitigations that worked (still good practice):** (1) verify every consequential action with a SECOND authoritative command — *especially git* (`git log` / `git status` / `git ls-remote` / `Test-Path`); (2) `Write` (full-file overwrite) is safe blind and repeatable; (3) targeted `Edit` is safe-on-failure but needs an in-session `Read` first; (4) build+test after every change. **The actual filesystem/git/build/test operations were sound — only result *reporting* was unreliable, and only on OneDrive.** The repo now lives alongside `kapturevault-backend` under `C:\DEV`.

## ✅ What happened this session (2026-06-01)

- **Repo relocated off OneDrive** → `C:\DEV\Utilities\KaptureVault` (robocopy + verify + delete source; retires the OneDrive tooling hazard). Path refs + memory updated. **Future sessions: launch Claude Code from `C:\DEV\Utilities`.**
- **v1.0.6 RELEASED** — shipped **F-01 (Export Vault Database)**: Export DB toolbar button → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy` off-thread (`DatabaseServiceBackupTests`). `Invoke-Release.ps1` → `auto-release.yml` created the GitHub Release v1.0.6 (`aee32b5`).
- **P1 backlog COMPLETE (shipped in v1.0.7), test-first:** **T-16** (`ff78e6d` — `Avalonia.Headless.XUnit` harness `TestAppBuilder` + `MainWindowSmokeTests` + `MainWindowViewModelFilterTests`/`…EntriesDiffTests`); **T-09** (`53f0ad4` — `SyncEntries` diff-update + debounced `RequestRefresh` + off-UI `RefreshAsync`; `CaptureEntry` observable — KV-013/032/033); **T-08** (`bf3658c` — `ShutdownCoordinator` + `OnShutdownRequested` centralize teardown; ServiceProvider disposed on every exit — KV-011/024). Tests **49 → 71**.
- **v1.0.7 RELEASED** (`2d09aa3`) — shipped the P1 batch above.
- **F-02 Phase 1 backend** (prior session) remains DONE in `kapturevault-backend` (19 vitest + CI green); retires KV-007/T-12 via the broker. **All P0 + P1 audit issues are now resolved.**
- **kapture.tools is a SEPARATE repo** — `Kapture.Tools-Website` (cloned to `C:\DEV\Kapture.Tools-Website`), **not** this repo's `docs/`. It was rebranded: hero badge "Free & Open Source" → **"v{version} - Freeware"** (auto-updates from the KaptureVault Releases API via `download.js`) + stale download card fixed. It has **no GitHub Pages** — deploys via an external host. This repo's `docs/` is a redundant legacy landing page.

## Recent commit stack (client `origin/main`, 2026-06-01 — verify with `git log --oneline`)

Newest first; HEAD = `origin/main` (verified via `git ls-remote`).

| Commit | What |
|---|---|
| `85d3ddc` | docs: reconcile to v1.9.0 / app 1.0.7; repo visibility private→public *(HEAD = origin/main)* |
| `8c33a0b` | revert: restore docs/index.html footer (wrong site) |
| `9879d83` | feat(site): footer version+Freeware *(superseded — real site is the separate repo)* |
| `2d09aa3` | release: v1.0.7 *(tag v1.0.7; ships the P1 batch T-16/T-09/T-08)* |
| `2155a72` | docs: reconcile to v1.8.0 (P1 backlog complete) |
| `bf3658c` | feat(t-08): centralize shutdown teardown (KV-011/024) |
| `53f0ad4` | feat(t-09): diff-update Entries + debounce + off-UI decrypt (KV-013/032/033) |
| `ff78e6d` | test(t-16): Avalonia headless harness + VM regressions (KV-045) |
| `aee32b5` | release: v1.0.6 *(tag v1.0.6; ships F-01)* |

**Backend repo** (`C:\dev\kapturevault-backend`, own history): `8795110`, `4758a50` — F-02 Phase 1 (Worker + Stripe webhook + R2 presign + D1 schema + 19 vitest).

## Next moves (recommended order)

**All P0 + P1 audit issues are resolved.** Remaining work is the F-02 product initiative and the P2/P3 polish backlog.

1. **F-02 Phase 2 (client side)** — wire the desktop app to the backend: `R2StorageProvider : ICloudStorageProvider` (ask the Worker for a presigned URL, then PUT/GET bytes to R2) + Google sign-in UI → `POST /auth/session` (store session + refresh in DPAPI `CloudTokenStore`) + `IEntitlementService` reading `/me` + a subscription gate on the Online Vault. This also lands the secret-less client OAuth (ex-T-12/KV-007). **Blocked on the human prereqs below** for live testing, but the client code can be built/unit-tested against a mocked Worker first (use the new T-16 headless harness).
2. **OR cut v1.0.7** — ship the completed P1 batch (T-16/T-09/T-08: UI responsiveness + clean shutdown). Promote CHANGELOG `[Unreleased]` → `[1.0.7]`, run `Invoke-Release.ps1 -BumpType minor`. Quick; decouples the hardening from the larger F-02 work.
3. **P2 backlog** (`ROADMAP.md`): T-18 (known-plaintext key verifier, KV-019), T-19 (zero master key on lock, KV-020), T-20 (transactional bulk encrypt/decrypt, KV-021), T-21 (`wal_checkpoint(TRUNCATE)` before DB copy), T-22 (extract Settings/QuickPaste/ContentViewer VMs, KV-015/027/037), T-24 (harden `CloudSyncManager` timer/retry).

## Related repos / human prereqs (NEW — for F-02 live)

The backend (`C:\dev\kapturevault-backend`) is code-complete + CI-green but **cannot run live** until a human provides the cloud accounts and fills the placeholders:

1. **Cloudflare account** with **R2 + Workers + D1** enabled.
2. **Stripe** test **and** live keys + the **$49/yr price id**.
3. A **Google OIDC sign-in client** (web/installed) for `/auth/session`.

Then, in the backend repo: fill `wrangler.toml` `REPLACE_WITH_*` (account id, R2 bucket, D1 db id, price id, Google client id), `wrangler secret put` the secrets (Stripe keys, webhook signing secret, session-signing key), and `npm run db:schema:remote` to apply the D1 schema. **Until then, F-02 stays Phase 1 (local/mocked tests only).**

## Release pipeline reminder (unchanged)

`Invoke-Release.ps1` builds/packages/bumps/commits-CHANGELOG/**pushes** — it does **NOT** create the GitHub release. The pushed `releases/latest/*.exe` triggers `.github/workflows/auto-release.yml` (the **single** release creator) → VirusTotal scan + GitHub Release. `kapture.tools` reads release + CHANGELOG live from GitHub. **Never re-add `gh release create` to the script.** Stable URL: `github.com/Vybecode-LTD/KaptureVault/releases/latest/download/KaptureVaultSetup-<ver>-x64.exe`.

## Blockers / human-only (carried over)

- **Google Cloud Console:** finish the OAuth consent screen for `kapture.tools` (authorized domain + TOS/Privacy URLs, exit Testing mode). **Secret-less OAuth now lands via the F-02 backend broker (KV-007 retired)** — the Cloud Console reconfigure happens as part of wiring the Google OIDC client above, not standalone.
- **GitHub Pages + DNS:** point `kapture.tools` at Pages (A `@` → 185.199.108–111.153; CNAME `www` → `vybecode-ltd.github.io`), enforce HTTPS; verify in Google Search Console.
- **Mobile viewer:** paste the web client ID `…70gd1j2j…` into `docs/vault/index.html` (`GOOGLE_WEB_CLIENT_ID`).
- **F-02 cloud accounts / placeholders:** see "Related repos / human prereqs" above.
- **Repo hygiene:** ✅ DONE 2026-06-01 — client repo moved off OneDrive to `C:\DEV\Utilities\KaptureVault` (now alongside the backend under `C:\dev`).

## Build / run quick reference

```powershell
# Client
dotnet build -c Debug
.\bin\Debug\net9.0-windows\win-x64\KaptureVault.exe        # kill any running (maybe elevated) instance first
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 49 passing
# Backend (C:\dev\kapturevault-backend)
npm test                                                   # 19 vitest passing
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`.
