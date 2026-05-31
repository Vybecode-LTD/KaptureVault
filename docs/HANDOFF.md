---
document: HANDOFF
version: 1.7.0
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
git ls-remote origin refs/heads/main                     # main expected = ddc3ce4
git status --porcelain                                   # expect CLEAN
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 49 passing
```

**If those match, the client repo is healthy** (v1.0.5 shipped, F-01 merged-but-unreleased, tree clean) — proceed to "Next moves". If they don't, the tooling may be lying; reconcile against the "Recent commit stack" below before editing anything.

**Backend lives in a SEPARATE repo** (off OneDrive): `C:\dev\kapturevault-backend` (own git + GitHub remote `github.com/Vybecode-LTD/kapturevault-backend`, private). Verify with `npm test` there → **19 vitest passing**. See "Related repos / human prereqs".

## TL;DR

KaptureVault = the **vault-only fork** of Kapture: keystroke/clipboard/screenshot capture → SQLite, optional AES-256-GCM encryption, optional Google Drive sync, Quick Paste, screenshot annotation editor. C# 13 / .NET 9 / Avalonia 11.3.12. **v1.0.5 SHIPPED** this session (tag `v1.0.5` = `ffc3c9d`, GitHub Release created by the workflow). **F-01 (Export Vault Database) DONE but UNRELEASED** (on main, CHANGELOG `[Unreleased]`; ships in **v1.0.6**). **F-02 Phase 1 backend DONE** in the separate `kapturevault-backend` repo. Client `dotnet test` → **49 passing**; build Debug+Release **0/0**; format+vuln CI gates green. HEAD `ddc3ce4` = `origin/main` (verified), tree clean. The latest *released* version is **v1.0.5**; the next *new* release will be **v1.0.6** (it promotes the staged F-01).

## ⚠️ CRITICAL — environment hazard (OneDrive path)

The client repo lives on a **OneDrive path** and the tool harness has repeatedly returned **stale, delayed, fabricated, or empty** tool output — including reporting commits, files, a CI workflow, and a `git push` as DONE when none had happened, and going **dark (empty results)** on the read/verify channel mid-task while `Write`/`git commit` kept working.

**Mitigations that worked:** (1) verify every consequential action with a SECOND authoritative command before relying on it — *especially git* (`git log` / `git status` / `git ls-remote` / `Test-Path`); (2) `Write` (full-file overwrite) is safe blind and safe to repeat; (3) targeted `Edit` is safe-on-failure (errors, never corrupts) but needs an in-session `Read` first; (4) run ONE PowerShell per turn (a non-zero exit cancels parallel siblings); (5) build+test after every change. **The actual filesystem/git/build/test operations were sound — only result *reporting* was unreliable.** The **backend repo is already off OneDrive** (`C:\dev\kapturevault-backend`) and did not exhibit this. **STRONGLY consider cloning the client repo to a non-OneDrive path too** (e.g. `C:\dev\KaptureVault`).

## ✅ What shipped this session (2026-05-31)

- **F-01 (Export Vault Database) — DONE** (commit `ddc3ce4`, currently **UNRELEASED**, ships in v1.0.6): Export DB toolbar button → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy` run **off-thread**; covered by `DatabaseServiceBackupTests`. Client test count **47 → 49**. CHANGELOG entry is staged under `[Unreleased]`.
- **F-02 Phase 1 — BACKEND DONE** in a SEPARATE private repo **`kapturevault-backend`** (`github.com/Vybecode-LTD/kapturevault-backend`; on disk `C:\dev\kapturevault-backend`, **off OneDrive**). Cloudflare Worker providing: Google-auth sessions, Stripe billing + webhook → D1 subscription state machine, presigned R2 URLs scoped to `users/{uid}/`, an entitlement gate, and the D1 schema. **19 vitest tests + `tsc` + GitHub CI green** (commits `4758a50`, `8795110`). This **retires KV-007 / T-12** — the backend broker holds the OAuth/token secrets, so the client carries none.
- (Earlier this session, already released) **v1.0.5** — P1 hardening batch 2 (T-07 hook-thread DB writes, T-11 PBKDF2 600k, T-10 DI) + `dotnet format`/vuln CI gates in `tests.yml` (the format/vuln half of T-16).

## Recent commit stack (client `origin/main`, 2026-05-31 — verify with `git log --oneline`)

Newest first; HEAD = `origin/main` (verified via `git ls-remote`). **If yours differ, trust `git log`, not this table** (tooling has fabricated SHAs before — see ⚠️).

| Commit | What |
|---|---|
| `ddc3ce4` | feat: Export Vault Database (F-01) + `DatabaseServiceBackupTests` *(HEAD = origin/main; UNRELEASED → v1.0.6)* |
| `d92952d` | ci: format + vuln gates in tests.yml |
| `12b7122` | chore: format tests project |
| `ffc3c9d` | release: v1.0.5 *(tag v1.0.5 here)* |
| `51dc9fd` | chore: format app project |
| `0351500` | refactor(di): HotkeyService + MainWindowViewModel in DI (T-10, KV-010) |
| `5748f9f` | fix(crypto): PBKDF2 → 600k + persisted KDF params (T-11, KV-006) |
| `e5977dd` | fix(capture): SQLite INSERT off the keyboard-hook thread (T-07, KV-012) |

**Backend repo** (`C:\dev\kapturevault-backend`, own history): `8795110`, `4758a50` — F-02 Phase 1 (Worker + Stripe webhook + R2 presign + D1 schema + 19 vitest).

## Next moves (recommended order)

1. **F-02 Phase 2 (client side)** — wire the desktop app to the backend: `R2StorageProvider : ICloudStorageProvider` (ask the Worker for a presigned URL, then PUT/GET bytes to R2) + Google sign-in UI → `POST /auth/session` (store session + refresh in DPAPI `CloudTokenStore`) + `IEntitlementService` reading `/me` + a subscription gate on the Online Vault. **Blocked on the human prereqs below** for live testing, but the client code can be built/unit-tested against a mocked Worker first.
2. **OR cut v1.0.6 first** — ship the already-done **F-01** (promote `[Unreleased]` → `[1.0.6]`, run `Invoke-Release.ps1 -BumpType minor`). Quick, decouples the shipped F-01 from the larger F-02 client work.
3. **Older client backlog** (post-F-02 or interleaved): **T-16 remainder** — `Avalonia.Headless.XUnit` UI smoke + `MainWindowViewModel` filter-selection regression test (the harness that makes T-09/T-08 verifiable); **T-09 / KV-013/032/033** — **diff-update** `Entries` (NEVER `Clear()` a selection-bound list — see Lessons), debounce `Refresh()`, decrypt off the UI thread; **T-08 / KV-011/024** — one **idempotent `TeardownAsync`** (via `ShutdownRequested`) that disposes `HotkeyService` + `ServiceProvider` (four shutdown paths bypass the current teardown). Pair T-09/T-08 with T-16.

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
- **Repo hygiene:** move the client repo off the OneDrive path (see ⚠️ above); the backend repo is already off it.

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
