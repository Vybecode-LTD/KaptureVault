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
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **51 vitest**; HEAD `e61a3ad` (pushed).

> **✅ F-02 Phase 2 built + pushed (2026-06-01).** Both repos are pushed (CI runs the format/vuln/test gates). The Phase-2 **backend is not deployed yet** — it goes live on `wrangler deploy` (a human step); see Live status. (Earlier F-02 stack: client `6ad70e5`..`97f4ca8`; backend through `e61a3ad`.)
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.0.7**.

**The current initiative is F-02 "Online Vault"** (paid file hosting + free cloud sync). The engine is live-provisioned and Phases 0–1 (polish + desktop UX) shipped; **Phase 2 is now built** (2026-06-01): free vault sync (the `/vault/*` paywall dropped), per-user quota + server-side vault size cap, refresh≠session token, Worker CORS, `/me` tier model, and the desktop panel shows quota/used. Client suite **123**, backend **51**, Release **0/0**, format clean, **both repos pushed**. **Next: `wrangler deploy` + rotate secrets (human), then F-02 Phase 3** (client vault-sync v2).

## Agreed product model (Revision 2)

| Capability | Free (offline) | Free (registered) | Paid — $49/yr |
|---|:---:|:---:|:---:|
| App · local vault · DB export · Drive sync | ✓ | ✓ | ✓ |
| Account — Google **or** email/password | — | ✓ | ✓ |
| Online vault sync (capture DB + re-encoded screenshots) + web vault | — | ✓ (≤250 MB) | ✓ (~10 GB) |
| File hosting + private/public + share links | — | — | ✓ |

Paid differentiator = **file hosting + share links**; vault sync is **free** for any account. Full design + the build phases (Engine/0/1 done, 2–6 to go) are in `ROADMAP.md` (§ F-02) and `docs/F-02-online-vault-design.md` (§ Revision 2).

## Next moves (recommended order)

0. **Go live with Phase 2 (human, not done this session):** `cd C:\dev\kapturevault-backend && npm run deploy` (`wrangler deploy`) to take the Phase-2 Worker live; **rotate the Google + Stripe-live secrets** pasted in chat during provisioning; for the future web vault add **R2-bucket CORS** (runbook). On deploy the new token audience re-requires one sign-in.
1. ✅ **F-02 Phase 2 — DONE (2026-06-01, pushed):** free vault sync (paywall dropped), quota + server-side size cap (HEAD-on-commit), refresh≠session token, Worker CORS, `/me` tier model, desktop storage display. **`/account` page deferred to Phase 4/5** (needs web auth). Backend vitest 26→51, client 123.
2. **Phase 3 — client vault-sync v2 (NEXT):** multi-object sync (`vault.db` + re-encoded screenshot images), quota-aware; salt/KDF in `vault.db.meta` for web unlock. The backend now enforces quota on the `PUT /vault/meta` commit (413 over-quota) — the client should surface that as "over quota".
3. **Phase 4** — web vault (needs the **T-34** repo-consolidation decision) + the deferred **`/account`** page; **Phase 5** — email/password auth; **Phase 6** — file hosting (paid).
4. **Or** pause F-02 and clear the **P2 backlog** (T-18..T-26, **T-35** = route Drive through the broker to close residual KV-007).

## Recent commit stack (pushed — verify with `git log --oneline`)

**Backend (`kapturevault-backend`):** `e61a3ad` /me tier · `ebed5c5` CORS · `5f98fe9` quota+size-cap · `ab32c78` free vault sync · `f657b87` refresh≠session — **Phase 2**; atop `8480022` (Phase 1 + engine). **Client:** v1.12.0 docs reconcile · `09f2fee` storage display (quota/used) — **Phase 2 client**; atop the F-02 stack `97f4ca8`..`6ad70e5` over `505adf1`.

## Live status / human prereqs

**Online Vault engine is provisioned + LIVE** (Cloudflare R2+D1+Workers, Stripe **live** price `price_1TdVtY…` + keys + webhook, Google OIDC sign-in client; secrets set; D1 schema applied). Runbook: `docs/F-02-PROVISIONING.md`. **The Phase-2 changes are pushed but NOT deployed** — run `npm run deploy` (`wrangler deploy`) in `kapturevault-backend` to take them live.
- **⚠️ Rotate the exposed secrets (STILL OUTSTANDING):** the Google client secret + Stripe **live** secret key were pasted into chat during setup — roll both before wide use (Google Console → reset secret; Stripe → Developers → roll key; re-`wrangler secret put`).
- **R2-bucket CORS:** required before the Phase-4 web vault (the browser fetches R2 directly via presigned URLs); not needed for desktop sync (runbook).
- **`www` DNS:** the site has no `www` host yet (app links bare `kapture.tools`); the maintainer plans to add the `www` record.
- **After deploy, vault sync is FREE for any signed-in account** (the `/vault/*` paywall is gone, quota-enforced); cancelling a subscription locks only paid features (file hosting, Phase 6) — it no longer 402s vault sync.

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
