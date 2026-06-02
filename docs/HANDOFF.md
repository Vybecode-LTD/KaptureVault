---
document: HANDOFF
version: 1.15.0
app-version: 1.0.7
last-updated: 2026-06-02
last-audit: 2026-06-02
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/ROADMAP.md, docs/BUGS.md, docs/TESTING.md, docs/AUDIT-LOG.md, docs/F-02-online-vault-design.md, docs/F-02-PROVISIONING.md]
---

# HANDOFF.md — KaptureVault

> **Canary doc — read first.** Pairs with `CLAUDE.md` (facts + standing directives + Lessons), `ROADMAP.md` (all to-dos + the F-02 build phases), `BUGS.md`, `TESTING.md`, `AUDIT-LOG.md` (full session history — see the **2026-06-01 PM-4** entry for everything below in detail), `F-02-online-vault-design.md` (§ Revision 2 = the agreed product model), `F-02-PROVISIONING.md` (go-live runbook).

## ▶ Start here (fresh session)

```powershell
cd C:\DEV\Utilities\KaptureVault
git status --porcelain                                   # expect CLEAN
git log -1 --oneline                                     # latest = v1.15.0 Phase-3 docs reconcile (LOCAL — F/G/H unpushed)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 162 passing
```
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **59 vitest**; HEAD `6e4570c` (pushed; Worker live at version `17ba084b`).

> **✅ F-02 Phase 3 COMPLETE — all slices A–H landed (2026-06-02).** Phase 2 is LIVE + smoke-verified (Worker `17ba084b`, R2 CORS, secrets rotated). **Phase 3:** A (web-unlock meta) · B (encryption interlock) · C (binary crypto) · **backend** D (object API) · E (multi-object quota) · **F (`a00ee25`) client screenshot sync pipeline** · **G (`5cc03e6`) restore** · **H docs reconcile (this)** — all done. Client suite **162**, backend **59**. Each of F/G/H was implemented → full-spectrum tested → independently audited; the G audit caught a real blocker (restore wrote images that the UI didn't display — fixed). **⚠️ Client commits F/G/H are NOT pushed yet** (this docs commit included). **Next: a live end-to-end smoke, then cut v1.0.8** (or pick up the P2 backlog).
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.0.7**.

**The current initiative is F-02 "Online Vault"** (paid file hosting + free cloud sync). The engine is live-provisioned and Phases 0–1 (polish + desktop UX) shipped; **Phase 2 is now built** (2026-06-01): free vault sync (the `/vault/*` paywall dropped), per-user quota + server-side vault size cap, refresh≠session token, Worker CORS, `/me` tier model, and the desktop panel shows quota/used. Client suite **130**, backend **59**, Release **0/0**, format clean, **both repos pushed + CI green**, Worker **deployed LIVE + smoke-verified** (version `17ba084b`; R2 CORS applied; secrets rotated). **Phase 3 is COMPLETE (2026-06-02, slices A–H)** — screenshots now sync to the Online Vault, end-to-end encrypted + quota-aware (upload pipeline `a00ee25`, restore `5cc03e6`), with display repointed to resolve restored images by filename. Client suite **162**, backend **59**. **Client F/G/H commits are unpushed; v1.0.8 deferred pending a live smoke.**

## Agreed product model (Revision 2)

| Capability | Free (offline) | Free (registered) | Paid — $49/yr |
|---|:---:|:---:|:---:|
| App · local vault · DB export · Drive sync | ✓ | ✓ | ✓ |
| Account — Google **or** email/password | — | ✓ | ✓ |
| Online vault sync (capture DB + re-encoded screenshots) + web vault | — | ✓ (≤250 MB) | ✓ (~10 GB) |
| File hosting + private/public + share links | — | — | ✓ |

Paid differentiator = **file hosting + share links**; vault sync is **free** for any account. Full design + the build phases (Engine/0/1 done, 2–6 to go) are in `ROADMAP.md` (§ F-02) and `docs/F-02-online-vault-design.md` (§ Revision 2).

## Next moves (recommended order)

0. ✅ **Phase 2 LIVE + smoke-verified (2026-06-01):** Worker deployed (version `17ba084b`), R2-bucket CORS applied (`kapturevault-backend/r2-cors.json`), secrets rotated, and the **Part F smoke passed end-to-end** (sign-in → sync → quota → checkout). *(The token-audience change means any pre-existing session needs one fresh sign-in.)*
1. ✅ **F-02 Phase 2 — DONE + LIVE (2026-06-01):** free vault sync, quota + server-side size cap, refresh≠session token, CORS, `/me` tier, desktop storage display. **`/account` deferred to Phase 4/5.** Backend vitest 26→51, client 124.
2. ✅ **Phase 3 — client vault-sync v2 (COMPLETE 2026-06-02, slices A–H):** screenshots sync to the Online Vault, end-to-end encrypted + quota-aware. A–E (prior); **F** (`a00ee25`) client screenshot pipeline (object-API client + `SkiaScreenshotImageCodec` + `ScreenshotSyncService.SyncUpAsync`: enumerate→re-encode→`EncryptBytes`→upload-only-new oldest-first; orphan cleanup; meta-recommit/413-trim backstop); **G** (`5cc03e6`) restore (`RestoreAsync` + `CaptureEntry.ScreenshotPath` resolve-by-filename, all four screenshot read sites repointed); **H** docs (this) + panel status via `LastSyncStatus`. Two deliberate deviations (see `docs/F-02-PHASE-3-DESIGN.md` § 11): remote-list-as-truth (no `online_sync_state.json`), and resolve-by-filename at display (no DB `Content` mutation → no multi-device churn). Client 162 / backend 59. **➡️ NEXT: a live end-to-end smoke (sign-in → capture a screenshot → sync → restore on a 2nd device), then cut v1.0.8.**
3. **Phase 4** — web vault (needs the **T-34** repo-consolidation decision) + the deferred **`/account`** page; **Phase 5** — email/password auth; **Phase 6** — file hosting (paid).
4. **Or** pause F-02 and clear the **P2 backlog** (T-18..T-26, **T-35** = route Drive through the broker to close residual KV-007).

## Recent commit stack (verify with `git log --oneline`)

**Client — ⚠️ the v1.15.0 docs commit + `5cc03e6` + `a00ee25` are LOCAL, NOT pushed:** v1.15.0 Phase-3 handoff reconcile (this) · `5cc03e6` **G** restore · `a00ee25` **F** screenshot pipeline · `8d19fad` v1.14.0 reconcile · `912821a` **C** binary crypto · `c716d20` **B** encryption interlock · `c34a327` v1.13.0 audit · `7275594` de-flake · `3b5c131` **A** KDF meta. **Backend (`kapturevault-backend`, pushed):** `6e4570c` **E** multi-object quota · `0193551` **D** object API; `0103f5b` R2-CORS + `e61a3ad`..`f657b87` Phase 2; atop `8480022` (Phase 1 + engine). Backend was untouched by slices F–H.

## Live status / human prereqs

**Online Vault is provisioned, deployed, and LIVE** (Cloudflare R2+D1+Workers, Stripe **live** price `price_1TdVtY…` + keys + webhook, Google OIDC; **Worker version `17ba084b` serving Phase 2**, `/health` ok). Runbook: `docs/F-02-PROVISIONING.md`.
- **✅ Secrets rotated + verified** — the maintainer rotated the Google client secret + Stripe live key, and the **Part F smoke (sign-in + checkout) passed**, confirming the live values are correct in the Worker.
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
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 162 passing
# Backend (C:\dev\kapturevault-backend)
npm test                                                   # 59 vitest passing
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`. Release: `scripts\Invoke-Release.ps1` (see `CLAUDE.md` release directive — never re-add `gh release create`).
