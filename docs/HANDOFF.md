---
document: HANDOFF
version: 1.17.0
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
git log -1 --oneline                                     # client main: P5 a/b/c on top of v1.16.0 — see commit stack (LOCAL, push held)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 182 passing
```
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **65 vitest**; HEAD `2bd0bee` (P5c handoff, **LOCAL/unpushed**; live Worker still serves Phase 2 `17ba084b` until the P5c deploy).

> **✅ F-02 Phases 3 + 4 + "P5" (UX redesign) ALL BUILT + AUDITED (2026-06-02). Ships as v1.1.0.** Phase 3 (screenshot sync) + the **KV-046 ShutdownMode crash fix** are on `origin/main`. Phase 4 (web vault) + **P5** (decouple Drive backup from the Online Vault, a main-window Login→Sync/Web Vault/Upload toolbar, and a **true web-vault handoff** = browser auto-login) are built across all three repos, **independently audited** (P5c = "SAFE TO DEPLOY"), and **held LOCAL pending the maintainer's go-live** (see commit stack). Client suite **182**, backend **65**. **➡️ To go live (human, in order):**
> 1. **Backend deploy (NEW — required for the handoff):** `cd C:\dev\kapturevault-backend; npm run db:schema:remote` (adds the `handoff_codes` table) then `npx wrangler deploy`.
> 2. Add `https://kapture.tools` as an authorized JS origin to the sign-in Google client `…p6c6gmi0…`.
> 3. **Push** (the maintainer chose to hold these): client `KaptureVault` (`git push`), backend `kapturevault-backend` (`git push`), and the website `Kapture.Tools-Website` (`git push` — **auto-deploys**).
> 4. **Smoke:** desktop **Login** → **Web Vault** (browser should land signed-in) → enter the vault password → see the vault + screenshots. Try **Sync** (icon spins) and **Upload** (free → upgrade pitch).
> 5. Cut **v1.1.0**: `Invoke-Release.ps1 -BumpType major`.
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.0.7**.

**The current initiative is F-02 "Online Vault"** (paid file hosting + free cloud sync). The engine is live-provisioned and Phases 0–1 (polish + desktop UX) shipped; **Phase 2 is now built** (2026-06-01): free vault sync (the `/vault/*` paywall dropped), per-user quota + server-side vault size cap, refresh≠session token, Worker CORS, `/me` tier model, and the desktop panel shows quota/used. Client suite **130**, backend **59**, Release **0/0**, format clean, **both repos pushed + CI green**, Worker **deployed LIVE + smoke-verified** (version `17ba084b`; R2 CORS applied; secrets rotated). **Phase 3 is COMPLETE + PUSHED and Phase 4 (web vault) is BUILT (2026-06-02)** — screenshots sync to the Online Vault (pipeline `a00ee25`, restore `5cc03e6`) and a new **web vault** at `kapture.tools/vault` reads the Online Vault + shows screenshots (`b5e2fc7` in `Kapture.Tools-Website`, **unpushed**), plus a desktop "Use the Online Vault for sync" control (`86cfc30`) and the **KV-046 ShutdownMode crash fix** (`cbfbf5e`). Client suite **168**, backend **59**. **Then "P5" (2026-06-02) redesigned the Online-Vault UX** — decoupled Google Drive backup from the Online Vault (now auto-syncs when signed in), added a main-window **Login → Log out/Web Vault/Upload/Sync** toolbar (spinning Sync icon; tier-adaptive Upload upgrade popup), and a **true web-vault handoff** (the browser auto-logs-in from a one-time code). Client **182**, backend **65**, both independent audits PASS / "SAFE TO DEPLOY". **The next release is v1.1.0** (Phase 3 + 4 + P5); it goes live after the human steps in the callout (backend deploy → Google JS-origin → push all three repos → smoke → release).

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
4. ✅ **"P5" — Online-Vault UX redesign (BUILT + AUDITED 2026-06-02; LOCAL/unpushed):** **P5a** (`3fe7082`) decouple Drive backup ⟂ Online Vault (auto-sync when signed in; retired the "active provider" model + `ISyncProviderController` + the Settings "Use for sync" control — Phase-4's desktop control above is SUPERSEDED); **P5b** (`03f43cf`+`84306aa`) main-window Login→Log out/Web Vault/Upload/Sync toolbar (spinning Sync; tier-adaptive `UploadDialog`); **P5c** (backend `2bd0bee` + desktop `00c379d` + website `0907f51`) true handoff (`/auth/handoff/{create,exchange}` → browser auto-login, still prompts for the vault password). Client **182**, backend **65**; both audits PASS / SAFE TO DEPLOY. **One LOW deferred:** rate-limit/cron-GC the `handoff_codes` table (see ROADMAP). Go-live = the callout steps.
5. **After v1.1.0:** **Phase 6** — file hosting behind the **Upload** button (the paid differentiator); **Phase 5** — email/password auth + `/account`. Or clear the **P2 backlog** (T-18..T-26, **T-35** = route Drive through the broker to close residual KV-007). **T-34** (website-repo consolidation) remains an optional cleanup.

## Recent commit stack (verify with `git log --oneline`)

**Client `KaptureVault` (⚠️ P5 commits LOCAL — push held by the maintainer; last PUSHED = `6fca01c`):** `84306aa` P5b layout (toolbar→filter-bar, type filters→column header, in-place Sync spinner) · `00c379d` **P5c** desktop Web-Vault handoff · `03f43cf` **P5b** main-window Login/Sync/Web Vault/Upload · `3fe7082` **P5a** decouple Drive backup ⟂ Online Vault · `6fca01c` v1.16.0 docs (pushed) · `86cfc30` Phase-4 desktop · `cbfbf5e` **KV-046**. *(This session's docs reconcile → v1.17.0 sits on top.)* **Website `Kapture.Tools-Website` (⚠️ LOCAL, NOT pushed — auto-deploys):** `0907f51` **P5c** web-vault auto-login (handoff exchange) · `b5e2fc7` Phase-4 web vault. **Backend `kapturevault-backend` (⚠️ P5c LOCAL/unpushed):** `2bd0bee` **P5c** `/auth/handoff/{create,exchange}` (+ `handoff_codes` table) · `6e4570c` **E** · `0193551` **D**.

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
