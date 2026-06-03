---
document: HANDOFF
version: 1.18.1
app-version: 1.2.0
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
git log -1 --oneline                                     # client main = v1.2.0 released (Phase 6); all pushed (tag v1.2.0)
dotnet test KaptureVault.Tests/KaptureVault.Tests.csproj # expect 226 passing (Phase 5.5 added +19)
```
Backend (separate repo): `cd C:\dev\kapturevault-backend && npm test` → **114 vitest**; HEAD `92e1639` (**Phase 5 auth — LOCAL/unpushed**; Phase 6 `c9c6257` is the last PUSHED — deployed live, `GET /files` unauth → 401).

> **✅ v1.1.0 RELEASED (2026-06-02) — F-02 Phases 3 + 4 + "P5".** All three repos are **pushed** (client `49c8dd4` incl. the `release: v1.1.0` tag, backend `2bd0bee`, website `0907f51` — the web vault auto-deployed). `auto-release.yml` created the GitHub Release (installer `KaptureVaultSetup-1.1.0-x64.exe`, VT-scanned): <https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.1.0>. v1.1.0 = screenshot sync (Phase 3) + the web vault (Phase 4) + **P5** (decoupled Drive backup ⟂ Online Vault, the main-window Login→Log out/Web Vault/Upload/Sync toolbar with the spinning Sync icon, and the **true web-vault handoff** = browser auto-login). Client suite **182**, backend **65**; both P5 audits PASS / "SAFE TO DEPLOY". **Backend Worker is now live with the handoff endpoint** (the maintainer ran `db:schema:remote` + `wrangler deploy`) and `kapture.tools` is an authorized Google JS origin.
> **✅ v1.2.0 RELEASED (2026-06-02) — F-02 Phase 6 (paid file hosting + share links).** Client + backend **pushed** (client `ab07f63` incl. the `release: v1.2.0` tag, backend `c9c6257`); the **backend is deployed live** (`migrations/0001` applied + `wrangler deploy`; `GET /files` unauth → 401 = live + gated). `auto-release.yml` created the Release (installer `KaptureVaultSetup-1.2.0-x64.exe`): <https://github.com/Vybecode-LTD/KaptureVault/releases/tag/v1.2.0>. v1.2.0 = a pop-open **Files manager** behind the paid Upload button — upload **Private 🔒** (client-encrypted, owner-only) or **Shareable 🔗** (public capability-link), virtual **folders**, download/copy-link/delete; `/files/*` (paid-gated) + `/s/{token}`; files + vault share one quota. **Audit "SAFE TO DEPLOY".** Maintainer smoke-tested on a subscribed account. Also fixed the website changelog parser (`4468bb2`) so a `[Unreleased]` section no longer renders as "vUnreleased". **➡️ NEXT:** **Phase 5** (`/account`) · **P2 backlog** (T-35) · the LOW Phase-6 hardening (orphan-row GC + put-url rate-limit; CORS `DELETE` when the web vault gains file delete; lapsed-share policy) · extend `kapture.tools` **ToS/Privacy** for hosted files + share links + a DMCA/takedown path.
> **🟢 F-02 Phase 5 (email/password auth + `/account`) — CLIENT+SERVER CORE BUILT (2026-06-02; LOCAL/unpushed).** Identity model **= one account per VERIFIED email** (Google + email/password for the same verified address = the SAME account; subscription/files/vault shared). **Backend** (`kapturevault-backend`, vitest **85 → 114**, tsc clean; commits `431dee7`..`92e1639`): **5.1** PBKDF2 hashing + schema (nullable `google_sub`, `email_verified`, `password_hash`, unique-email index, `auth_tokens`; migration `0002` + the unify-by-verified-email upsert + injectable `EmailSender`/Resend seam), **5.2** `POST /auth/register`+`/auth/verify` (password unusable until verified; **pre-registration attack defused** — Google-unify drops an unverified password), **5.3** `/auth/login`+`/auth/reset-request`+`/auth/reset` (generic 401, no-enumeration reset), **5.4** D1 rate-limit on login/register/reset-request (per email+IP → 429), **5.6-backend** `DELETE /account` (cancel Stripe + purge R2 + wipe D1) + `867a69a`/`7c95ccb` (trailing-slash verify/reset/account links + audit-L3 login length bound). **Desktop** (`KaptureVault`, suite **207 → 226**, Debug+Release 0/0, format clean; commit `89936bb`): a main-window **Login dialog** (email/password sign-in, register, forgot, + Continue-with-Google) wired to the API client + `OnlineAccountService` methods; the **§42 interlock** — registering an account password equal to the configured vault password is REFUSED (new `EncryptionService.VerifyPassword`). **✅ 5.6 web DONE** (`Kapture.Tools-Website` `636d786`, **LOCAL/unpushed** — that repo auto-deploys): `/verify/`, `/reset/`, `/account/` directory pages (account = sign-in via email/password / Google / handoff → subscription + storage, upgrade/manage-billing, change-password, confirmed Delete-account) reusing the web-vault theme; node --check clean; verify/reset are noindex; Account nav link + sitemap added. **✅ 5.7 audit DONE — independent verdict "SAFE TO DEPLOY"** (0 critical/high/medium; LOW only — L1 native-CF rate-limit binding + L2 cron GC, both deferred; L3 login length bound = fixed `7c95ccb`). **➡️ REMAINING (maintainer go-live for Phase 5):** set `RESEND_API_KEY` + `EMAIL_FROM` secrets (else verify/reset emails are no-op'd — accounts can register but can't verify); apply **`migrations/0002_email_password.sql`** (`wrangler d1 execute kapturevault --remote --file=…` — run the dup-email pre-flight in the migration header first); `wrangler deploy`; **push** client `89936bb` + backend (`431dee7`..`7c95ccb`) + **review then push the website** `636d786`; smoke (register → verify email → desktop login → /account → delete) on a throwaway account; then a `perform handoff` to formally reconcile + version-bump the docs (this pass is a content update only, frontmatter still **1.18.1**).
> **⚠️ Capture Admin Apps self-elevates the app** — when it's running you can't `Stop-Process` it (it locks the build output); tray-Quit it before rebuilding, or toggle it off (Settings → Advanced) to iterate freely.

## TL;DR

KaptureVault = vault-only fork (keystroke/clipboard/screenshot → SQLite, AES-256-GCM, Drive sync, Quick Paste, annotation editor). C# 13 / .NET 9 / Avalonia 11.3.12. Repo `C:\DEV\Utilities\KaptureVault` (off OneDrive), public. Latest release **v1.2.0** (2026-06-02 — paid file hosting + share links; v1.1.0 was the Online Vault).

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
