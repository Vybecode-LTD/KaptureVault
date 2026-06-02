---
document: HANDOFF
version: 1.16.0
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
git log -1 --oneline                                     # client main = Phase 3 + KV-046 fix + Phase 4 desktop + v1.16.0 docs (all pushed)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 168 passing
```
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **59 vitest**; HEAD `6e4570c` (pushed; Worker live at version `17ba084b`).

> **✅ F-02 Phase 3 DONE + PUSHED; Phase 4 (web vault) BUILT (2026-06-02). Next release re-scoped to v1.1.0 (Phase 3 + 4 bundled).** Phase 3 (screenshot sync) shipped to `origin/main` along with the **KV-046 ShutdownMode crash fix** (`cbfbf5e`, maintainer-verified). **Phase 4** adds the **web vault** that reads the Online Vault (R2) and shows screenshots — built in the **separate `Kapture.Tools-Website` repo** (`C:\DEV\Kapture.Tools-Website`, commit `b5e2fc7`, **LOCAL — NOT pushed**: that repo auto-deploys, so it waits on the Google JS-origin provisioning + review), plus a desktop **"Use the Online Vault for sync"** control + live provider switch (`86cfc30`, KaptureVault repo). Client suite **168**, backend **59**. **➡️ To go live (human):** (1) add `https://kapture.tools` as an authorized JS origin to the sign-in Google client `…p6c6gmi0…`; (2) `git push` the website repo (auto-deploys); (3) smoke the web vault (desktop: sign in → "Use the Online Vault for sync" → capture a screenshot → Sync Now; browser: kapture.tools/vault → Open Online Vault → unlock → see the screenshot); (4) cut **v1.1.0** via `Invoke-Release.ps1 -BumpType major`.
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.0.7**.

**The current initiative is F-02 "Online Vault"** (paid file hosting + free cloud sync). The engine is live-provisioned and Phases 0–1 (polish + desktop UX) shipped; **Phase 2 is now built** (2026-06-01): free vault sync (the `/vault/*` paywall dropped), per-user quota + server-side vault size cap, refresh≠session token, Worker CORS, `/me` tier model, and the desktop panel shows quota/used. Client suite **130**, backend **59**, Release **0/0**, format clean, **both repos pushed + CI green**, Worker **deployed LIVE + smoke-verified** (version `17ba084b`; R2 CORS applied; secrets rotated). **Phase 3 is COMPLETE + PUSHED and Phase 4 (web vault) is BUILT (2026-06-02)** — screenshots sync to the Online Vault (pipeline `a00ee25`, restore `5cc03e6`) and a new **web vault** at `kapture.tools/vault` reads the Online Vault + shows screenshots (`b5e2fc7` in `Kapture.Tools-Website`, **unpushed**), plus a desktop "Use the Online Vault for sync" control (`86cfc30`) and the **KV-046 ShutdownMode crash fix** (`cbfbf5e`). Client suite **168**, backend **59**. **The next release is re-scoped from v1.0.8 to v1.1.0** (Phase 3 + 4); it goes live after the 4 human steps in the callout (Google JS-origin → push website → smoke → release).

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
3. 🟢 **Phase 4 — web vault (BUILT 2026-06-02; pending provisioning + deploy).** `Kapture.Tools-Website/vault/index.html` (`b5e2fc7`, LOCAL) gained an "Open Online Vault" path: Google Identity Services → `/auth/session` → session JWT → `GET /vault/get-url` + `/vault/meta` → derive the key from the meta's salt+iterations (fixes the hardcoded PBKDF2 100k) → decrypt (reuses the Drive viewer's WebCrypto) → and **screenshots**: `GET /vault/objects` + `/vault/object/get-url` → binary AES-GCM decrypt → show the image. Password verified via AES-GCM's own auth tag (no KeyHash oracle on the server). Desktop gained the **"Use the Online Vault for sync"** control + a live `ISyncProviderController` switch (`86cfc30`). **To finish:** the 4 human steps in the callout above (Google JS-origin → push website → smoke → cut **v1.1.0**). **`/account`** page still deferred (Phase 5).
4. **After v1.1.0:** **Phase 5** — email/password auth + `/account`; **Phase 6** — file hosting (paid). Or clear the **P2 backlog** (T-18..T-26, **T-35** = route Drive through the broker to close residual KV-007). **T-34** (website-repo consolidation) did **not** block Phase 4 — the web vault was built directly in `Kapture.Tools-Website`; consolidation remains an optional cleanup.

## Recent commit stack (verify with `git log --oneline`)

**Client `KaptureVault` (pushed, `origin/main` = `86cfc30`):** `86cfc30` Phase-4 desktop "use Online Vault for sync" + live switch · v1.16.0 docs reconcile (this) · `cbfbf5e` **KV-046** ShutdownMode crash fix · `6b4099b` v1.15.0 Phase-3 reconcile · `5cc03e6` **G** restore · `a00ee25` **F** screenshot pipeline · `8d19fad` v1.14.0. **Website `Kapture.Tools-Website` (⚠️ LOCAL, NOT pushed — auto-deploys):** `b5e2fc7` Phase-4 web vault (Online Vault read + screenshots). **Backend `kapturevault-backend` (pushed, untouched this session):** `6e4570c` **E** · `0193551` **D** + Phase 2 stack atop `8480022`.

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
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 168 passing
# Backend (C:\dev\kapturevault-backend)
npm test                                                   # 59 vitest passing
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`. Release: `scripts\Invoke-Release.ps1` (see `CLAUDE.md` release directive — never re-add `gh release create`).
