---
document: HANDOFF
version: 1.11.0
app-version: 1.0.7
last-updated: 2026-06-01
last-audit: 2026-06-01
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/ROADMAP.md, docs/BUGS.md, docs/TESTING.md, docs/AUDIT-LOG.md, docs/F-02-online-vault-design.md, docs/F-02-PROVISIONING.md]
---

# HANDOFF.md — KaptureVault

> **Canary doc — read first.** Pairs with `CLAUDE.md` (facts + standing directives + Lessons), `ROADMAP.md` (all to-dos + the F-02 build phases), `BUGS.md`, `TESTING.md`, `AUDIT-LOG.md` (full session history — see the **2026-06-01 PM-4** entry for everything below in detail), `F-02-online-vault-design.md` (§ Revision 2 = the agreed product model), `F-02-PROVISIONING.md` (go-live runbook).

## ▶ Start here (fresh session)

```powershell
cd C:\DEV\Utilities\KaptureVault
git status --porcelain                                   # expect CLEAN
git log -1 --oneline                                     # expect 97f4ca8 (the F-02 stack is LOCAL/unpushed)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 120 passing
```
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **26 vitest**; HEAD `8480022`.

> **⚠️ The F-02 work is committed LOCALLY but NOT pushed** — client `6ad70e5`..`97f4ca8` (16 commits) on top of `origin/main` = `505adf1`; backend `9a969d9`+`8480022`. Push when ready (CI runs format/vuln/test gates).
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.0.7**.

**The current initiative is F-02 "Online Vault"** (paid tier + free cloud sync). This session: the F-02 **engine was provisioned and deployed LIVE** (Cloudflare Worker at `kapturevault-backend.kapture.workers.dev`, `/health` ok; D1 + R2 + Stripe-live + Google OIDC all wired), and **Phases 0–1** shipped (polish + the desktop Online-Vault panel + the Export-DB/Run-on-startup relocation + the Settings-layout overflow fix). Client suite **120**, backend **26**, Debug+Release **0/0**, format clean. **Next: F-02 Phase 2** (backend free-vault tier + quota).

## Agreed product model (Revision 2)

| Capability | Free (offline) | Free (registered) | Paid — $49/yr |
|---|:---:|:---:|:---:|
| App · local vault · DB export · Drive sync | ✓ | ✓ | ✓ |
| Account — Google **or** email/password | — | ✓ | ✓ |
| Online vault sync (capture DB + re-encoded screenshots) + web vault | — | ✓ (≤250 MB) | ✓ (~10 GB) |
| File hosting + private/public + share links | — | — | ✓ |

Paid differentiator = **file hosting + share links**; vault sync is **free** for any account. Full design + the build phases (Engine/0/1 done, 2–6 to go) are in `ROADMAP.md` (§ F-02) and `docs/F-02-online-vault-design.md` (§ Revision 2).

## Next moves (recommended order)

1. **F-02 Phase 2 — backend free-vault + foundations** (in `kapturevault-backend`, + a little client). The functional unlock:
   - Drop the `/vault/*` `requireEntitled` gate (move entitlement to the future `/files/*`) so **free** accounts sync.
   - **Quota + a server-pinned object-size cap** — MANDATORY: `/vault/put-url` currently signs an *unbounded* PUT and `storage_used` is never enforced; free R2 writes without a cap are an abuse hole. Pin max size in the presigned signature and/or HEAD the object; maintain `storage_used`; reject over-quota.
   - **Fix refresh ≠ session token** (today both are minted identically — a 30-day refresh is accepted as a session bearer). Give refresh a distinct audience/`typ`.
   - Add **CORS** (Worker **and** the R2 bucket); `/me` → `{tier,features,quota,used}`; build the **`/account`** page (Stripe redirects there + it 404s).
2. **Phase 3** — client vault-sync v2: multi-object sync (`vault.db` + re-encoded screenshot images), quota-aware; salt/KDF in `vault.db.meta` for web unlock.
3. **Phase 4** — web vault (needs the **T-34** repo-consolidation decision); **Phase 5** — email/password auth; **Phase 6** — file hosting (paid).
4. **Or** pause F-02 and clear the **P2 backlog** (T-18..T-26, **T-35** = route Drive through the broker to close residual KV-007).

## Recent commit stack (client, local/unpushed — verify with `git log --oneline`)

`97f4ca8` www→kapture.tools · `8b8e964` Phase 1c relocate Export/startup · `1941c56` lessons · `55f2279` Settings overflow real fix · `d21efe2` overflow partial · `7c7a7f8` Phase 1b panel · `d4e1ff8` Phase 1a Open Vault · `e0c49f2` Phase 0 polish · `ead72ab` design Rev 2 · `624f351` config→Worker · `bbc5a80` provisioning runbook · `f703a77` reconcile v1.10.0 · `9bd7369`..`6ad70e5` Phase 2 engine (slices 5b→1). Backend: `8480022` client-id + `9a969d9` broker/meta on top of Phase 1 (`8795110`/`4758a50`).

## Live status / human prereqs

**Online Vault is provisioned + LIVE** (Cloudflare R2+D1+Workers, Stripe **live** price `price_1TdVtY…` + keys + webhook, Google OIDC sign-in client; secrets set; D1 schema applied; Worker deployed). Runbook: `docs/F-02-PROVISIONING.md`.
- **Rotate the exposed secrets:** the Google client secret + Stripe **live** secret key were pasted into chat during setup — roll both before wide use (Google Console → reset secret; Stripe → Developers → roll key; re-`wrangler secret put`).
- **`www` DNS:** the site has no `www` host yet (app now links bare `kapture.tools`); the maintainer plans to add the `www` record.
- **Vault sync currently still 402s for non-subscribers** until Phase 2 lifts the `/vault/*` paywall.

## Gotchas (also in CLAUDE Lessons)
- **Settings layout overflow** (recurs): the settings `ScrollViewer` measures content at **unbounded width**, so wrapping text won't wrap and spills out. `HorizontalScrollBarVisibility="Disabled"` did NOT fix it — `SettingsWindow` code-behind pins the content `StackPanel.MaxWidth` to the ScrollViewer `Bounds.Width − Padding`. Don't undo that.
- **Avalonia incremental builds keep stale compiled XAML** — use `dotnet build --no-incremental` when iterating on `.axaml`.
- **Elevated app can't be killed from a non-elevated shell** — tray-Quit, or toggle Capture Admin Apps off.

## Build / run quick reference
```powershell
dotnet build -c Debug                                      # kill/tray-quit any running instance first
.\bin\Debug\net9.0-windows\win-x64\KaptureVault.exe
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 120 passing
# Backend (C:\dev\kapturevault-backend)
npm test                                                   # 26 vitest passing
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`. Release: `scripts\Invoke-Release.ps1` (see `CLAUDE.md` release directive — never re-add `gh release create`).
