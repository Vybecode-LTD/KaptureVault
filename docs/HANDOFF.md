---
document: HANDOFF
version: 1.12.0
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
git log -1 --oneline                                     # latest = the v1.12.0 docs reconcile (pushed)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 123 passing
```
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **51 vitest**; HEAD `0103f5b` (pushed, **deployed live**).

> **✅ F-02 Phase 2 built, pushed, and DEPLOYED LIVE (2026-06-01).** Both repos pushed (CI green); the Worker is **deployed** (version `17ba084b`, `/health` ok, CORS header confirmed live) and the **R2 bucket CORS is applied**; **secrets rotated**. (Earlier F-02 stack: client `6ad70e5`..`97f4ca8`; backend through `0103f5b`.)
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.0.7**.

**The current initiative is F-02 "Online Vault"** (paid file hosting + free cloud sync). The engine is live-provisioned and Phases 0–1 (polish + desktop UX) shipped; **Phase 2 is now built** (2026-06-01): free vault sync (the `/vault/*` paywall dropped), per-user quota + server-side vault size cap, refresh≠session token, Worker CORS, `/me` tier model, and the desktop panel shows quota/used. Client suite **123**, backend **51**, Release **0/0**, format clean, **both repos pushed and the Worker is deployed LIVE** (version `17ba084b`; R2 CORS applied; secrets rotated). **Next: F-02 Phase 3** (client vault-sync v2).

## Agreed product model (Revision 2)

| Capability | Free (offline) | Free (registered) | Paid — $49/yr |
|---|:---:|:---:|:---:|
| App · local vault · DB export · Drive sync | ✓ | ✓ | ✓ |
| Account — Google **or** email/password | — | ✓ | ✓ |
| Online vault sync (capture DB + re-encoded screenshots) + web vault | — | ✓ (≤250 MB) | ✓ (~10 GB) |
| File hosting + private/public + share links | — | — | ✓ |

Paid differentiator = **file hosting + share links**; vault sync is **free** for any account. Full design + the build phases (Engine/0/1 done, 2–6 to go) are in `ROADMAP.md` (§ F-02) and `docs/F-02-online-vault-design.md` (§ Revision 2).

## Next moves (recommended order)

0. ✅ **Phase 2 is LIVE (2026-06-01):** Worker deployed (`wrangler deploy`, version `17ba084b`), **R2-bucket CORS applied** (`kapturevault-backend/r2-cors.json`), **secrets rotated**. *(The token-audience change means any pre-existing session needs one fresh sign-in.)* **Still worth doing:** run runbook **Part F** (sign in → subscribe with a test/real card → Refresh → Sync) to confirm the rotated `GOOGLE_CLIENT_SECRET`/`STRIPE_SECRET_KEY` are the values now in the Worker.
1. ✅ **F-02 Phase 2 — DONE (2026-06-01, pushed):** free vault sync (paywall dropped), quota + server-side size cap (HEAD-on-commit), refresh≠session token, Worker CORS, `/me` tier model, desktop storage display. **`/account` page deferred to Phase 4/5** (needs web auth). Backend vitest 26→51, client 123.
2. **Phase 3 — client vault-sync v2 (NEXT):** multi-object sync (`vault.db` + re-encoded screenshot images), quota-aware; salt/KDF in `vault.db.meta` for web unlock. The backend now enforces quota on the `PUT /vault/meta` commit (413 over-quota) — the client should surface that as "over quota".
3. **Phase 4** — web vault (needs the **T-34** repo-consolidation decision) + the deferred **`/account`** page; **Phase 5** — email/password auth; **Phase 6** — file hosting (paid).
4. **Or** pause F-02 and clear the **P2 backlog** (T-18..T-26, **T-35** = route Drive through the broker to close residual KV-007).

## Recent commit stack (pushed — verify with `git log --oneline`)

**Backend (`kapturevault-backend`):** `e61a3ad` /me tier · `ebed5c5` CORS · `5f98fe9` quota+size-cap · `ab32c78` free vault sync · `f657b87` refresh≠session — **Phase 2**; atop `8480022` (Phase 1 + engine). **Client:** v1.12.0 docs reconcile · `09f2fee` storage display (quota/used) — **Phase 2 client**; atop the F-02 stack `97f4ca8`..`6ad70e5` over `505adf1`.

## Live status / human prereqs

**Online Vault is provisioned, deployed, and LIVE** (Cloudflare R2+D1+Workers, Stripe **live** price `price_1TdVtY…` + keys + webhook, Google OIDC; **Worker version `17ba084b` serving Phase 2**, `/health` ok). Runbook: `docs/F-02-PROVISIONING.md`.
- **✅ Secrets rotated** (Google client secret + Stripe live key) — confirmed by the maintainer. *(If any rotation was console-only, re-`wrangler secret put` it; runbook Part F sign-in/checkout would surface a stale one.)*
- **✅ R2-bucket CORS applied** — `kapturevault` bucket allows GET/PUT/HEAD from `kapture.tools` + `www` + `localhost:5173/4173`, exposes `ETag`; policy committed as `kapturevault-backend/r2-cors.json` (`wrangler r2 bucket cors set kapturevault --file r2-cors.json`).
- **`www` DNS:** the site has no `www` host yet (app links bare `kapture.tools`); the maintainer plans to add the `www` record.
- **Vault sync is FREE for any signed-in account** (the `/vault/*` paywall is gone, quota-enforced 250 MB free / 10 GB paid); cancelling a subscription locks only paid features (file hosting, Phase 6) — it no longer 402s vault sync.

## Gotchas (also in CLAUDE Lessons)
- **Settings layout overflow** (recurs): the settings `ScrollViewer` measures content at **unbounded width**, so wrapping text won't wrap and spills out. `HorizontalScrollBarVisibility="Disabled"` did NOT fix it — `SettingsWindow` code-behind pins the content `StackPanel.MaxWidth` to the ScrollViewer `Bounds.Width − Padding`. Don't undo that.
- **Avalonia incremental builds keep stale compiled XAML** — use `dotnet build --no-incremental` when iterating on `.axaml`.
- **Elevated app can't be killed from a non-elevated shell** — tray-Quit, or toggle Capture Admin Apps off.

## Build / run quick reference
```powershell
dotnet build -c Debug                                      # kill/tray-quit any running instance first
.\bin\Debug\net9.0-windows\win-x64\KaptureVault.exe
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 123 passing
# Backend (C:\dev\kapturevault-backend)
npm test                                                   # 51 vitest passing
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`. Release: `scripts\Invoke-Release.ps1` (see `CLAUDE.md` release directive — never re-add `gh release create`).
