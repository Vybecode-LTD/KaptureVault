---
document: HANDOFF
version: 1.18.0
app-version: 1.1.0
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
git log -1 --oneline                                     # client main: Phase 6 on top of v1.1.0 — see commit stack (LOCAL, push held)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 207 passing
```
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **85 vitest**; HEAD `c9c6257` (Phase 6 file hosting, **LOCAL/unpushed**; live Worker serves the v1.1.0/P5c version until the Phase 6 deploy).

> **✅ v1.1.0 RELEASED (2026-06-02) — F-02 Phases 3 + 4 + "P5".** All three repos are **pushed** (client `49c8dd4` incl. the `release: v1.1.0` tag, backend `2bd0bee`, website `0907f51` — the web vault auto-deployed). `auto-release.yml` created the GitHub Release (installer `KaptureVaultSetup-1.1.0-x64.exe`, VT-scanned): <https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.1.0>. v1.1.0 = screenshot sync (Phase 3) + the web vault (Phase 4) + **P5** (decoupled Drive backup ⟂ Online Vault, the main-window Login→Log out/Web Vault/Upload/Sync toolbar with the spinning Sync icon, and the **true web-vault handoff** = browser auto-login). Client suite **182**, backend **65**; both P5 audits PASS / "SAFE TO DEPLOY". **Backend Worker is now live with the handoff endpoint** (the maintainer ran `db:schema:remote` + `wrangler deploy`) and `kapture.tools` is an authorized Google JS origin.
> **🟢 Phase 6 (paid file hosting + share links) BUILT + AUDITED (2026-06-02; LOCAL/unpushed).** A pop-open **Files manager** behind the paid Upload button: upload **Private 🔒** (client-encrypted, owner-only) or **Shareable 🔗** (public capability-link), virtual **folders**, download, copy-link, delete. Backend `/files/*` (paid-gated) + `/s/{token}` public download; files + vault share one quota. **Independent security audit: "SAFE TO DEPLOY"** (all 10 checks PASS; only LOW/INFO — orphan-row GC, CORS DELETE-when-web-needs-it, lapsed-share policy). Client **207** / backend **85**. **➡️ Go-live (human):** (1) `cd C:\dev\kapturevault-backend; npx wrangler d1 execute kapturevault --remote --file=./migrations/0001_files_encrypted_folder.sql` (one-time column add) + `npx wrangler deploy`; (2) push all three repos (client + backend; the website is unchanged this phase); (3) smoke on a **subscribed** account (upload private/shareable, folder, share link); (4) `Invoke-Release.ps1 -BumpType major` → **v1.2.0**; (5) extend `kapture.tools` **ToS/Privacy** for hosted files + share links + a DMCA/takedown path. **Then** Phase 5 (`/account`) · P2 backlog (T-35) · the LOW GC/rate-limit hardening.
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.1.0** (2026-06-02 — the Online Vault).

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
5. ✅ **v1.1.0 RELEASED (2026-06-02)** — Phases 3 + 4 + P5 shipped (see the top callout).
6. 🟢 **Phase 6 (paid file hosting + share links) BUILT + AUDITED (2026-06-02; LOCAL)** — Files-manager window (encrypt/share per file, folders) + `/files/*` + `/s/{token}`; "SAFE TO DEPLOY". Ships **v1.2.0** after the go-live in the top callout (deploy backend + migration → push → smoke on a paid account → release → extend ToS/Privacy).
7. **After v1.2.0:** **Phase 5** — email/password auth + `/account`. Or the **P2 backlog** (T-18..T-26, **T-35** = route Drive through the broker to close residual KV-007). **T-34** (website-repo consolidation) remains an optional cleanup. Plus the LOW Phase 6 hardening (orphan-row GC + put-url rate-limit; CORS `DELETE` when the web vault gains file delete).

## Recent commit stack (verify with `git log --oneline`)

**Client `KaptureVault` (⚠️ Phase 6 LOCAL — push held; last PUSHED = `b4c4218` = v1.1.0 docs, tag `v1.1.0`):** `7956410` **6D-3** Files manager window (folders + encrypt/share) · `006bcc9` **6D-2** client encrypt/download · `34ecbeb` **6C** FileHostingService + api client · *(v1.1.0 stack below is pushed:)* `b4c4218` v1.1.0 docs · `49c8dd4` release v1.1.0 · `84306aa`..`3fe7082` P5. **Backend `kapturevault-backend` (⚠️ Phase 6 LOCAL/unpushed; last PUSHED = `2bd0bee`):** `c9c6257` **6D-1** encrypt flag + folders · `171c3cd` **6B** share links · `c990834` **6A** file API · `2bd0bee` P5c handoff (pushed). **Website `Kapture.Tools-Website`:** unchanged this phase (`0907f51` = P5c, pushed).

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
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj   # 207 passing
# Backend (C:\dev\kapturevault-backend)
npm test                                                   # 85 vitest passing
```
Inno Setup ISCC: `C:\Users\vybec\AppData\Local\Programs\Inno Setup 6\ISCC.exe`. Release: `scripts\Invoke-Release.ps1` (see `CLAUDE.md` release directive — never re-add `gh release create`).
