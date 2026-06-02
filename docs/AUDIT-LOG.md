---
document: AUDIT-LOG
version: 1.17.0
app-version: 1.0.7
last-updated: 2026-06-02
last-audit: 2026-06-02
managed-by: manual-reconciliation
see-also: [CLAUDE.md, docs/BUGS.md, docs/ROADMAP.md, docs/TESTING.md, docs/HANDOFF.md]
---

# AUDIT-LOG.md — KaptureVault

## 2026-06-02 (PM-2) — F-02 "P5" UX redesign (decouple + main-window + true handoff) BUILT + AUDITED; docs → v1.17.0

**Trigger:** After the v1.1.0 smoke, the maintainer's feedback was that Google Drive backup and "logging into our own system" were conflated/confusing, plus UX requests (a spinning Sync indicator, a main-window layout, and a *true* web-vault handoff). Built as "P5" (a/b/c), test-first, each sub-slice its own commit, with an independent audit per major piece.

### P5a — decouple Drive backup ⟂ Online Vault (`3fe7082`)
The two were conflated behind one selectable "active provider"; now INDEPENDENT. Google Drive backup = Settings → "Google Drive Backup" (`AppSettings.DriveBackupEnabled`); the Online Vault auto-syncs whenever signed in + a vault password is set. `CloudSyncManager.SyncAsync(provider)` + two timers + `SyncDriveNowAsync`/`SyncOnlineVaultNowAsync`. **Retired** the active-provider model (`SetActiveProvider`/`GetActiveProvider`/`StartPeriodicSync`) + `ISyncProviderController` + the Settings "Use the Online Vault for sync" control. Legacy settings migrate in `SettingsService.Load` (only a former "Google Drive" provider → `DriveBackupEnabled`). New `CloudSyncManagerTests` (9). **Independent audit: PASS** (decouple complete + correct; no orphaned refs; DI/lifecycle/migration sound; leak-free `_syncing` guard).

### P5b — main-window UX (`03f43cf` + layout `84306aa`)
Toolbar **Login** (signed out) → **Log out · Web Vault · Upload · Sync** (signed in), in the filter bar far-left; type filters moved to the entry-list **column header**; the **Sync icon spins** while syncing (`Classes.spin` + `RenderTransformOrigin="50%,50%"` — a bare `0.5,0.5` parses as 0.5 px from the corner → orbits; see CLAUDE Lessons). **Upload** opens a tier-adaptive `UploadDialog` (free → upgrade pitch → Stripe; paid → "coming soon", real hosting = Phase 6). `OnlineAccountViewModel` exposed on `MainWindowViewModel.Account` + `SyncNowCommand`/`ShowLogin` via a minimal `IOnlineVaultSync` seam. Smoke-tested with the maintainer ("perfect" — after fixing the spinner orbit).

### P5c — true web-vault handoff (backend `2bd0bee` + desktop `00c379d` + website `0907f51`)
`POST /auth/handoff/create` (behind `requireSession`) mints a single-use, 120 s, 256-bit url-safe code bound to the caller's uid; `POST /auth/handoff/exchange` (public) atomically consumes it (`DELETE … RETURNING`) and issues a real session+refresh. Desktop "Web Vault" opens `kapture.tools/vault#handoff=<code>`; the web vault reads the fragment, exchanges it, scrubs the URL (`history.replaceState`), and lands signed in — **still prompting for the vault password** (the code carries ONLY the account session; the encryption key never leaves the desktop; the server stays ciphertext-only). `handoff_codes` D1 table + Store methods (+ in-memory fake). **Independent SECURITY audit: SAFE TO DEPLOY** — PASS on all 10 checks (auth-on-create, single-use, expiry, 256-bit entropy, no-key-in-URL, fragment-not-query + scrubbing, audience separation, safe public exchange, leak-free fallback, no SQLi/cross-account). Two LOW notes: no rate-limit/cron-GC on `handoff_codes` (bounded + auth-gated → ROADMAP) and harmless email denormalization.

### Verification + state
Client `dotnet test` **182**, Debug+Release 0/0, `dotnet format --verify` clean (both projects), 0 vulnerable. Backend vitest **65**, `tsc` exit 0. Website `node --check` exit 0 (functional smoke is the maintainer's). Managed docs → **v1.17.0**; CHANGELOG `[Unreleased]`/v1.1.0 updated for the redesign (the old "use for sync" bullet replaced). **All P5 commits LOCAL — pushes held per the maintainer** (client `3fe7082`..`84306aa`+`00c379d`; backend `2bd0bee`; website `0907f51` — auto-deploys).

**Auditor:** Claude (Opus 4.8) + two independent sub-agent audits. **Next (human go-live for v1.1.0):** backend D1 schema (`npm run db:schema:remote`) + `wrangler deploy`; add the `kapture.tools` Google JS origin; push all three repos; end-to-end smoke (Login → Web Vault auto-login → password → vault); `Invoke-Release.ps1 -BumpType major`. Then Phase 6 (file hosting behind Upload) / Phase 5 (`/account`) / P2 backlog.

## 2026-06-02 (PM) — v1.0.8 smoke → KV-046 fix + F-02 Phase 4 (web vault) BUILT → re-scoped to v1.1.0; docs → v1.16.0

**Trigger:** Maintainer ran the Phase-3 smoke ("run the app so I can smoke test it"); it surfaced real defects, which led into building Phase 4. The release is re-scoped from v1.0.8 to **v1.1.0** (Phase 3 + 4).

### Live smoke debugging (DEBUG_PROTOCOL — proof, not assertion)
- **Capture "not working" → environment, not code.** A clipboard-image probe with the app running produced a new `.bmp` → capture is fine. The user's screenshots weren't captured due to (a) `captureAdminApps=true` self-elevating the app (every agent-shell launch bounced on UAC → not really running) and (b) the self-exclusion (skips capture while KaptureVault is the foreground window). Resolved by toggling the setting off for testing + explaining the clipboard/foreground mechanics.
- **App froze / exited right after the vault password → KV-046 (real bug, fixed).** A `dotnet-dump` full dump showed the UI thread wedged purely in Avalonia's compositor (`HandlePaint → SyncWaitCompositorBatch → Task.Wait`, zero app frames); a normal launch produced a **clean exit with no crash event**. Root cause: `App.OnFrameworkInitializationCompleted` showed+closed a temporary owner window for the unlock dialog while `ShutdownMode` was still the default `OnLastWindowClose` (it was set to `OnExplicitShutdown` only *after* the unlock block). Fix: set the mode first. Maintainer-verified the app now opens. Commit `cbfbf5e`; BUGS KV-046 + a CLAUDE Lesson.
- **"Salt not found in Drive" in the web vault → expected.** `kapture.tools/vault` was the **old Google-Drive** viewer; it doesn't read R2. Confirmed the desktop's active sync provider was still "Google Drive" (so the Phase-3 R2 sync never ran for the user). Both gaps → Phase 4.

### F-02 Phase 4 — web vault (BUILT; pending provisioning + deploy)
- **Web vault** (`Kapture.Tools-Website/vault/index.html`, `b5e2fc7`, **unpushed** — auto-deploys): an "Open Online Vault" path beside the Drive viewer — Google Identity Services → `POST /auth/session` → session JWT; `POST /vault/get-url` (presigned R2) → sql.js; `GET /vault/meta` → derive the AES key from the meta's **salt + iterations** (fixes the hardcoded PBKDF2 **100k → 600k**, on the Drive path too); WebCrypto AES-GCM decrypt (reused); **screenshots**: `GET /vault/objects` + `POST /vault/object/get-url` → binary AES-GCM `DecryptBytes` → `<img>`. Password verified via AES-GCM's own auth tag (**no KeyHash oracle on the server** — a deliberate improvement over the proposed meta-KeyHash). Syntax verified via `node --check` (exit 0); functional smoke is the maintainer's (needs the Google JS-origin + a synced vault).
- **Desktop** (`86cfc30`, suite **162 → 168**, full ledger green): the smoke's other gap — once signed in there was no way to make the Online Vault the sync target, and sign-in only wrote settings (the live `CloudSyncManager` kept syncing to Drive). New `ISyncProviderController` seam (CloudSyncManager) + `OnlineAccountViewModel.UseForSyncCommand` / `IsSyncTarget` / `CanUseForSync` + a Settings button; sign-in/out now switch the live provider. Tests +6.

### Verification + state
Client: `dotnet test` **168**, Debug+Release 0/0, `dotnet format --verify` clean, 0 vulnerable, publish ok. **`KaptureVault` pushed** (`origin/main` = `86cfc30`, incl. Phase 3 + KV-046). **`Kapture.Tools-Website` `b5e2fc7` is LOCAL/unpushed** (auto-deploys → waits on the Google JS-origin + review). Backend untouched. Managed docs → **v1.16.0**; CHANGELOG `[Unreleased]` re-scoped to v1.1.0.

**Auditor:** Claude (Opus 4.8). **Next (human):** add `https://kapture.tools` as a JS origin to the sign-in Google client → push the website repo → smoke the web vault end-to-end → cut **v1.1.0** (`Invoke-Release.ps1 -BumpType major`). Then Phase 5 (email/password + `/account`) / Phase 6 (file hosting) / the P2 backlog.

---

## 2026-06-02 — F-02 Phase 3 slices F + G + H (screenshot sync COMPLETE) → v1.15.0

**Trigger:** User — "let's do F, G and H in order. After each one, I want full spectrum testing done with a subsequent audit of the work for verification that everything was done properly. Please document everything in great detail… just proceed unless there is a major issue."

**Process per slice:** implement (test-first where it added value) → full evidence ledger → independent audit (a fresh subagent reviewing the diff against the design) → fix audit findings → commit. Client suite **130 → 162**; backend untouched (**59**). Baseline at session start: 130 green.

### Slice F — client screenshot sync pipeline (`a00ee25`, suite 130 → 152)
- **Client object API** (the backend had it since D/E; the client didn't): `GetObjectPutUrlAsync`/`GetObjectGetUrlAsync`/`DeleteObjectAsync`/`ListObjectsAsync` on `IKaptureOnlineApiClient`; `VaultObject`/`VaultObjectList` DTOs; `OnlineApiException.IsPayloadTooLarge` (413). Wire contract pinned against the Worker (`GET /vault/objects` returns `{key,size}` only — the design draft's `uploaded` field doesn't exist).
- **`SkiaScreenshotImageCodec`** (BMP→PNG, lossless). Hardened during testing: `SKBitmap.Decode` *throws* `ArgumentNullException` (internal null `SKCodec`) for undecodable bytes rather than returning null — normalized all decode/encode failures to one `InvalidOperationException` so a corrupt screenshot is skipped, never fatal. (Root-caused via a probe per DEBUG_PROTOCOL.)
- **`ScreenshotSyncService.SyncUpAsync`**: enumerate non-expired screenshots whose file exists (dedupe by filename, oldest-first) → re-encode → `EncryptBytes` → upload only the ones not already on R2, oldest-first within the quota; orphan cleanup (delete R2 screenshots the DB no longer references; never touches `vault.db`/`.meta`); meta-recommit so the server banks usage, trimming the newest upload + retrying on a 413. Wired into `CloudSyncManager` after an Online-Vault upload/in-sync (best-effort).
- **Deviation (documented):** the live remote object list is the source of truth for "already uploaded" — **no local `online_sync_state.json`** (design § 5.4.2). More robust across devices/reinstalls, no drift, and the list is needed anyway for orphan/quota.
- **Independent audit verdict: correct & safe** — no plaintext-leak path (encrypt is always applied + double-gated); wire contract + key regex match; quota/413 logic terminates and trims correctly; orphan cleanup can't touch `vault.db`. Closed the one Medium gap it found: a test for the "no remote `vault.db.meta` yet" commit branch. Ledger: Debug+Release 0/0, format clean (after CRLF normalization), 0 vulnerable, publish ok.

### Slice G — restore (`5cc03e6`, suite 152 → 162)
- **`ScreenshotSyncService.RestoreAsync`**: `GET /vault/objects` → download each screenshot missing locally → `DecryptBytes` → write the PNG into the local screenshots dir keyed by filename. Runs on a download-wins sync. Decrypt-before-write confirmed: a tampered/wrong-key/truncated blob throws `DecryptionException` → skipped (never written).
- **Deviation (documented):** resolve-by-filename at **display** (`CaptureEntry.ScreenshotPath` falls back to `ScreenshotDirectory` by filename) instead of mutating `Content` in the DB (design § 5.7/5.8). Mutating per device would ping-pong device-local paths under whole-DB LWW; the display fallback gives the same result with zero added churn.
- **🔴 Audit caught a real blocker:** the new `ScreenshotPath` fallback was **bound nowhere** — the UI read raw `Content`, so restored images were written to disk yet never displayed. **Fixed all four read sites** to use `ScreenshotPath`: reader-pane preview (`MainWindow.axaml`), `ContentViewerWindow`, the Save command (`MainWindowViewModel`), and the annotation editor (`ScreenshotEditorWindow`). The audit named three; a completeness `grep` caught the fourth (the editor). Also serialized the two test classes that mutate the static `ScreenshotDirectory` into an xUnit collection (audit-flagged race). Re-ran the full ledger green (no-incremental Release build to recompile the XAML binding).

### Slice H — UX + docs (this commit)
- **UX was already delivered** by F/G: `CloudSyncManager` folds the screenshot result into `LastSyncStatus`, which Settings already shows in `SyncStatusText` ("· N screenshot(s)", "· N not synced — over quota", "· N restored"). No new UI code needed.
- **Docs reconciled to v1.15.0:** all six managed-doc frontmatters + the CLAUDE body reference bumped together (`grep ^version:` confirmed); F-02 design § 11 marked F/G/H done + the two deviations recorded; CHANGELOG `[Unreleased]` gained the user-facing screenshot-sync entry (staged for v1.0.8); CLAUDE Health/Lessons/Session-Log, HANDOFF, ROADMAP (slice tracker + Phase-3 row), BUGS, TESTING (counts 130→162, new suites, the CloudSyncManager known gap) updated. Two new Lessons: CRLF/`dotnet format`, and "a model fallback is dead code unless the views bind it."

### Verification (proof, not assertion)
Final ledger (client): `dotnet build` 0/0, `dotnet build -c Release --no-incremental` 0/0, `dotnet test` **162/162** (+ coverage), `dotnet format --verify-no-changes` exit 0, `dotnet list package --vulnerable` none, `dotnet publish -c Release -r win-x64` ok. Backend not touched (no run needed). **Commits `a00ee25` (F), `5cc03e6` (G), + this v1.15.0 docs commit are LOCAL — not pushed.** v1.0.8 deferred to the maintainer pending a fresh live end-to-end smoke (sign-in → capture a screenshot → sync → restore on a second device).

**Auditor:** Claude (Opus 4.8) + two independent review subagents. **Next:** live smoke → cut v1.0.8, or the P2 backlog (T-18..T-26, **T-35**).

---

## 2026-06-01 (PM-7) — F-02 Phase 3 slices B–E + handoff reconciliation → v1.14.0

**Trigger:** User — continue Phase 3 ("keep going"), then "reconcile the docs now (capture A–E, bump version, handoff-ready), document everything in detail, update the to-do lists, ensure proper @ directives, make the memory thorough and well organized."

### Slices delivered (test-first, RED→GREEN, each its own commit, every push CI-verified green)

**Client (`KaptureVault`), suite 124 → 130:**
- **B — encryption interlock** (`c716d20`). The Online Vault is end-to-end encrypted, so a vault password is now REQUIRED to use it (Phase-3 design decision). Two layers: *(backstop)* `R2StorageProvider.UploadFileAsync` throws `InvalidOperationException` when `!_encryption.IsActive` — no path can push a plaintext vault to R2; *(UX gate)* `OnlineAccountViewModel` only sets the Online Vault as the sync target when encryption is active, otherwise prompts "Set a vault password in Settings → Encryption …" + the "that password is the only key — lose it and the online vault can't be recovered" warning (new `VaultPasswordRequired` bindable + a panel `TextBlock`). *Bonus fix:* `SignInAsync` had gated provider-set on `IsPaid` — stale since Phase 2 made vault sync free — now gated on encryption (free sync needs a password, not a subscription); the two stale `IsPaid` VM tests were repurposed. Tests: R2 refuse-when-unencrypted; VM persist-when-encrypted / prompt-when-not / `VaultPasswordRequired`.
- **C — binary encryption** (`912821a`). `EncryptBytes`/`DecryptBytes` on `IEncryptionService`/`EncryptionService`: AES-256-GCM over raw bytes (nonce[12]+tag[16]+ciphertext, no `ENC:` prefix/base64) — the prerequisite for encrypting screenshots client-side (slice F) and for the web vault to decrypt them (Phase 4, mirrored in WebCrypto). `DecryptBytes` throws `DecryptionException` on tamper/corruption/wrong-key; both throw `InvalidOperationException` if encryption isn't active (never silently emit plaintext). Tests: round-trip, tamper→throw, wrong-key→throw, not-active→throw.

**Backend (`kapturevault-backend`), vitest 51 → 59:**
- **D — vault object API** (`0193551`). `/vault/object/{put,get,delete}-url` + `GET /vault/objects` under the user's vault namespace, so screenshots sync as separate (encrypted) objects. `vaultObjectKey(uid, relKey)` validates the relative key to `^screenshots/[A-Za-z0-9._-]+$` (rejects traversal/out-of-namespace/nesting) before presign (still flows through `assertOwnedKey`). `/vault/objects` lists relative keys + sizes (restore discovery + quota), cursor-paginated. Free (`requireSession` only). `FakeR2.list` added. Tests: presign scoping, key validation, get, delete, list isolation, session required.
- **E — multi-object quota** (`6e4570c`). The `PUT /vault/meta` commit enforces the quota against the **sum of all objects** under the user's vault prefix (R2 `list`, cursor-paged), replacing the Phase-2 single-`vault.db` HEAD. Over quota → `413 {used, quota}` *without deleting* (with multiple objects the server can't safely choose what to drop; the client trims oldest-first + retries); `storage_used` is set to the summed total on success. Still ignores any client-reported size. Tests: multi-object sum rejects/accepts; over-quota reject-without-delete.

### Verification (proof, not assertion)
Per-slice RED then GREEN. Client: `dotnet test` **130**, `dotnet build -c Release` 0/0, `dotnet format --verify` clean. Backend: `npm test` **59**, `tsc --noEmit` clean. **Both repos pushed; CI watched to GREEN on every push.** Client HEAD `912821a`; backend HEAD `6e4570c`.

### Handoff reconciliation → shared `version` 1.14.0
- **Fixed a real drift:** `CLAUDE.md`'s YAML frontmatter `version` had been stuck at **1.9.0** through the 1.10–1.13 passes (only the body "currently X" line was bumped). All six managed docs are now uniformly **1.14.0** / `last-updated` 2026-06-01. (New Lessons guard added so it doesn't recur — grep `^version:` across the set.)
- **@ directives:** the STANDING DIRECTIVES section now presents the binding set as proper `@` directives (`@../../DEBUG_PROTOCOL.md`, `…/TESTING_PROCEDURES.md`, `…/DOCUMENTATION_MANAGER.md`, `…/VERSION_CONTROL.md`, `…/SOFTWARE_RELEASE.md`; added the missing `VERSION_CONTROL`; noted `SEO_OPTIMIZATION` is non-binding for this repo — the desktop app isn't web-facing). Large docs/design references stay read-on-demand backtick links (deliberately **not** `@`-imported, to avoid bloating every session's context). Documentation Map lists the three F-02 design references.
- **Memory:** rewrote `.claude/plan.md`, which was dangerously stale (dated 2026-05-19, describing the pre-fork full "Kapture" — System Tweaks/Services/Dashboard/Profiles/Privacy, v1.0.27, `requireAdministrator` — none of which exist in this vault-only fork), into an accurate current-state pointer to the canonical docs.
- Refreshed the stale "Known test gaps (T-16)" line in CLAUDE (gaps long closed). Updated ROADMAP (Phase 3 A–E done, F–H remain), TESTING (client 130 / backend 59 + the `vault-objects` suite + new client tests), BUGS (header), HANDOFF (canary). CHANGELOG left unchanged — Phase 3 is internal groundwork; the shipped app stays v1.0.7.

**Auditor:** Claude (Opus 4.8). **Next:** F-02 **slice F** — the client screenshot sync pipeline (enumerate non-expired screenshots → re-encode BMP→PNG via SkiaSharp → `EncryptBytes` → upload via the object API, incremental sync-state, oldest-first trim on a 413, orphan cleanup), then **G** (restore) and **H** (UX + docs/release).

---

## 2026-06-01 (PM-6) — Audit + quality gate; CI flake found & fixed; Phase 2 go-live verified; Phase 3 design + slice A

**Trigger:** User — "complete audit and testing to make sure everything is solid" before continuing Phase 3.

### Quality gate (proof, not assertion) — both repos GREEN
- **Client (`KaptureVault`):** `dotnet build -c Release` **0/0**; `dotnet test` **124 passing**; `dotnet format --verify` clean; `dotnet list package --vulnerable --include-transitive` **none**; tree clean = `origin/main`.
- **Backend (`kapturevault-backend`):** `npm test` **51 passing**; `tsc --noEmit` clean; `npm audit --omit=dev` **0 vulnerabilities**; tree clean = `origin/main`.

### CI flake found + fixed (the audit's catch)
The client **CI had failed** on the slice-A push (run 26792950330) although local was green: `CaptureServiceTests.Flush_DoesNotBlockTheHookThreadOnTheDatabaseWrite` (T-07/KV-012) timed out its **2 s** wait for the background `Channel` writer task to begin the gated insert — task-scheduling starvation under parallel-test load on the windows-2025 runner. The core non-blocking assertion (producer < 2 s) passed; only the secondary "writer began off-thread" wait flaked. **Fix:** widened that ceiling to 30 s (it asserts *off-thread*, not latency; returns in ms in the green path) — commit `7275594`. **Verified GREEN** on the same runner (`gh run watch` exit 0, run 26793266479).

### Earlier this session (folded in)
- **Phase 2 deployed LIVE + smoke-passed:** Worker `17ba084b`; R2-bucket CORS applied (`r2-cors.json`, `0103f5b`); Google + Stripe-live secrets rotated. The maintainer ran the full **Part F** smoke end-to-end (sign-in → sync → quota → checkout) and **confirmed it works**.
- **Phase 3 designed + APPROVED** (`docs/F-02-PHASE-3-DESIGN.md`, `d0190ff`): decisions locked — PNG; **require a vault password** to use the Online Vault; oldest-first over-quota; client pre-check + server prefix-sum quota; 8 slices A–H.
- **Phase 3 slice A done** (`3b5c131`): KDF params (salt/iterations/kdf) carried in `vault.db.meta` (v2) for web-vault key derivation; new `IEncryptionService.GetKdfInfo`; suite 123 → 124.

### Docs reconciled → shared `version` 1.13.0
HANDOFF/ROADMAP/TESTING/BUGS/CLAUDE/AUDIT-LOG + the Phase-3 design status; CLAUDE **Lessons** gained a guard about timing-tight waits on background-task pickup flaking on loaded CI. CHANGELOG unchanged (no client release cut — Phase 3 is internal groundwork; the shipped app stays v1.0.7).

**Auditor:** Claude (Opus 4.8). **Next:** Phase 3 **slice B** — the encryption interlock (require a vault password to enable the Online Vault; "sole key" warning).

---

## 2026-06-01 (PM-5) — F-02 Phase 2 backend (free vault + quota + token/CORS/tier) + client storage wiring

**Trigger:** User — absorb the docs, run an audit, then "let's get at it." Verified a green baseline first (client **120/120**, backend **26/26**, both trees clean, 19 client + 3 backend commits unpushed), then built **F-02 Phase 2** test-first, each slice its own commit. User decisions mid-session: **wire the desktop panel + reconcile docs now, defer the `/account` page to Phase 4/5, push both repos.**

### Backend (`kapturevault-backend`) — 5 slices, vitest **26 → 51**, `tsc` clean
- **`f657b87` refresh ≠ session token** — distinct JWT audiences (`kapturevault-client` vs `kapturevault-refresh`); a refresh can no longer act as a session bearer, nor a session be used at `/auth/refresh`. `test/auth-tokens.test.ts` (RED→GREEN).
- **`ab32c78` free vault sync** — dropped the `/vault/*` `requireEntitled` gate, so any signed-in account syncs. Entitlement is still computed + surfaced on `/me` and will gate `/files/*` in Phase 6. New `free-vault.test.ts`; the old "402-until-subscribed" assertions in `api`/`broker-meta` were repointed to the free-sync contract (deliberate requirement change, not lost coverage).
- **`5f98fe9` quota + server-side size cap** (the MANDATORY abuse-hole fix) — `PUT /vault/meta` is the upload commit: HEADs the **real** R2 object (never the client-reported size), rejects + deletes an over-quota vault (413), maintains `storage_used`; the meta body is capped at 64 KB. Quotas 250 MB free / 10 GB paid, tunable via `wrangler [vars]`. New `quota.ts` + `Store.setStorageUsed`; `quota`/`vault-quota` tests.
- **`ebed5c5` CORS** — `hono/cors` for the site origin (+ www) and localhost; unknown origins refused; non-CORS requests untouched. R2-**bucket** CORS is a separate Phase-4 config step (added to the runbook).
- **`e61a3ad` /me tier** — adds `{tier, features{vaultSync,fileHosting}, quota, used}` (kept `entitled`/`storageUsed` for the existing client). `me-tier.test.ts`.

### Client (`KaptureVault`) — storage display, suite **120 → 123**
- **`09f2fee`** — `MeResponse` + new `OnlineFeatures` parse the enriched `/me`; `IOnlineAccountService` caches `QuotaBytes`/`UsedBytes` each refresh; `OnlineAccountViewModel.StorageSummary` ("X of Y used") shows in the Settings → Online Vault panel once signed in. Tests: service caching (RED→GREEN) + VM formatting (incl. empty-when-unknown).

### Deferred / not done (tracked)
- **`/account` page → Phase 4/5** (needs web auth to be a real dashboard; the Stripe-redirect 404 does not affect the desktop flow).
- **Live steps — ✅ DONE later this same session:** `wrangler deploy` (Worker version `17ba084b`; `/health` ok + a live CORS-header check confirmed Phase 2 is serving), **R2-bucket CORS applied** (`r2-cors.json`, committed `0103f5b` + pushed; first attempt used the S3 schema and was safely rejected — wrangler wants the R2-native `rules` shape), and the **Google + Stripe-live secrets rotated** (maintainer-confirmed). Recommended follow-up: runbook **Part F** end-to-end smoke (sign-in → checkout → sync) to confirm the rotated secret values are the ones now stored in the Worker.

### Verification (proof, not assertion)
Backend `npm test` **51 passed** + `tsc --noEmit` clean. Client `dotnet test` **123 passed**, `dotnet build -c Release` **0 warn / 0 err**, `dotnet format --verify` clean (slnx).

### Docs reconciled → shared `version` 1.12.0
CLAUDE (Session Log + Health), HANDOFF, ROADMAP (Phase 2 status), TESTING (backend 51 + new suites; client 123), BUGS (header), this entry; `F-02-PROVISIONING.md` (quota note, R2-bucket CORS step, free-sync smoke test, `/account` deferral). CHANGELOG left as-is (Phase 2 still inactive until deployed — no shipped user-facing change).

**Auditor:** Claude (Opus 4.8). **Next:** F-02 **Phase 3** (client vault-sync v2: multi-object incl. screenshots, quota-aware) or the **P2 backlog** (incl. T-35); recommended first: runbook **Part F** smoke test of the now-live deploy.

---

## 2026-06-01 (PM-4) — F-02 provisioned LIVE + Phases 0–1 (polish, desktop UX, Settings layout fix)

**Trigger:** User — take the built F-02 Phase 2 live, then build out the agreed free/paid model. Long session. All commits **LOCAL/unpushed** (client HEAD `97f4ca8`; backend HEAD `8480022`).

### Provisioning (human-driven, agent-guided) — the Online Vault is now LIVE
- Wrote `docs/F-02-PROVISIONING.md` — a Cloudflare/Google/Stripe go-live runbook mapping every value to its destination (`wrangler.toml [vars]` vs `wrangler secret put` vs client `OnlineVaultConfig`).
- **Cloudflare:** account `4558289c…`; D1 `kapturevault` (`89330b45…`); R2 bucket `kapturevault`; R2 S3 keys (Account API token, Object R&W). Non-secrets → `wrangler.toml` (`R2_ACCOUNT_ID`, D1 id); R2 keys → Worker secrets. Registered the `kapture.workers.dev` subdomain; deployed → **`https://kapturevault-backend.kapture.workers.dev`** (`/health` → `{"ok":true}`).
- **Google:** dedicated OIDC sign-in client (Web-app type), redirect `http://localhost:48722/`, scopes openid+email → client id in `wrangler.toml [vars] GOOGLE_CLIENT_ID` + client `OnlineVaultConfig.DefaultGoogleClientId`; client SECRET → `GOOGLE_CLIENT_SECRET` Worker secret (`POST /auth/google` brokers the exchange so the client stays secret-less). JS-origins left empty (loopback code flow needs only the redirect URI).
- **Stripe (LIVE, user's choice):** $49/yr price `price_1TdVtY…` + live secret key + live webhook → `STRIPE_PRICE_ID`/`STRIPE_SECRET_KEY`/`STRIPE_WEBHOOK_SECRET`. `SESSION_JWT_SECRET` generated + set.
- Client pointed at the Worker URL + sign-in client id (`624f351`). Sign-in verified end-to-end (Google consent → loopback → `/auth/google` → session).
- **Secret hygiene:** the Google + Stripe-live secrets were pasted into chat → advised rotation before wide use; only non-secret values were ever written to files (each staged diff was checked).

### Phase 0 — polish (`e0c49f2`)
- `OAuthHelper` "Connected" page: was `text/html` (no charset) over UTF-8 bytes → mojibake. Now `text/html; charset=utf-8` + a branded page (inline styles + HTML entities, ASCII-safe source). Shared by Drive + Online sign-in.
- `IOnlineAccountService.Email` cached from `/me` (cleared on sign-out); panel shows "Signed in as {email}" not the uid.

### Phase 1 — desktop UX
- **1a (`d4e1ff8`):** `OnlineVaultConfig.WebVaultUrl` + `OnlineAccountViewModel.OpenVaultCommand`.
- **1b (`7c7a7f8`):** Settings "Online Vault" panel reworked into the free/paid funnel (sign-in → Open Vault → upgrade pitch); intro states vault sync is free.
- **1c (`8b8e964`):** relocated **Export DB** + **Run-on-startup** off the main toolbar into a new **Settings → General** card (frees room for the future paid Upload button). Run-on-startup binds to `MainWindowViewModel.ToggleStartupCommand` (kept on the VM → no ctor/DI/test churn); Export DB moved to `SettingsWindow.ExportDb_Click` code-behind so its picker parents to the Settings dialog. Removed `MainWindowViewModel.ExportVaultDatabase` + the two toolbar buttons.
- **www fix (`97f4ca8`):** `Open Vault` + About-dialog link/text → `https://kapture.tools` (site has no www); `/vault` for Open Vault.

### ⭐ Settings panel layout overflow — root cause + DEFINITIVE fix (this keeps recurring — documenting fully)
- **Symptom:** long text in Settings cards ran off the right edge / pushed the panel wider than the window. Cards whose first child is a long *wrapping* paragraph (Encryption, Online Vault) overflowed; short-text cards were fine; the affected cards were *different widths* — the tell.
- **Root cause:** the settings `ScrollViewer` measures its content at **UNBOUNDED width** in this layout, so `TextWrapping="Wrap"` never fires (it wraps only to the *available* width, which is effectively infinite) and the paragraph lays out single-line and spills past the card. `HorizontalScrollBarVisibility="Disabled"` did **NOT** constrain it; a `MaxWidth="{Binding #sv.Viewport.Width}"` binding was *unstable* and made it worse.
- **Fix that worked (`55f2279`):** in `SettingsWindow` code-behind, pin the content `StackPanel.MaxWidth` to the ScrollViewer's real visible width (`Bounds.Width − Padding`) on every `Bounds` change — a stable, window-driven value that forces wrapping and adapts on resize. Plus: moved the Cloud Sync `SyncNow` status out of a *horizontal* StackPanel (infinite child width) onto its own wrapping line (`d21efe2`); added `TextWrapping="Wrap"` to every variable status TextBlock.
- **If it recurs: do NOT rely on `HorizontalScrollBarVisibility="Disabled"` — bind the content `MaxWidth` to the viewer width.** (Also in CLAUDE Lessons.)

### Avalonia / iteration gotchas (now in CLAUDE Lessons)
- **Incremental builds keep STALE compiled XAML** — `.axaml`-only edits may not recompile; the app showed the OLD layout despite "Build succeeded." Use `dotnet build --no-incremental` when iterating on XAML.
- **Elevated app (Capture Admin Apps ON) can't be killed by a non-elevated `Stop-Process`** and locks the build output → required a manual tray-Quit on each rebuild; toggling it off restarts de-elevated.
- The PowerShell tool has no `tail` (masked a passing build as failed once).

### Verification (proof, not assertion)
Client `dotnet test` **120 passing** (118 at the Phase-2 reconcile; +1 Email, +1 OpenVault); Debug **and** Release builds **0/0**; `dotnet format --verify` clean; backend `npm test` **26** + `tsc` clean; Worker `/health` ok. Tree clean; HEAD `97f4ca8`.

### Docs reconciled → shared `version` 1.11.0
ROADMAP restructured to the agreed tier model + 0–6 build phases (Engine/0/1 done, Phase 2 next); HANDOFF refreshed; TESTING → 120; BUGS + CLAUDE (Session Log + Health + Lessons) updated; this entry.

**Auditor:** Claude (Opus 4.8). **Next:** F-02 **Phase 2** (backend free-vault tier — drop `/vault/*` gate + quota/size cap + refresh-token fix + CORS + `/me` tier + `/account`), then Phase 3 (vault-sync v2 incl. screenshots). Push the local stack when ready.

---

## 2026-06-01 (PM-3) — F-02 Phase 2 client Online Vault built (test-first, local/unpushed)

**Trigger:** User — "work on the F-02 phase, then the P2 backlog." Decisions: secret-less sign-in via the backend broker (Route B); cloud accounts not yet provisioned → build + unit-test against a mocked Worker.

**Delivered (7 commits; client `6ad70e5`..`9bd7369` + backend `9a969d9`; all LOCAL/unpushed):**
- **Backend** (`kapturevault-backend`): `POST /auth/google` (secret-less PKCE broker — exchanges the desktop code with Google holding the new `GOOGLE_CLIENT_SECRET`) + `PUT /vault/meta`; `exchangeGoogleCode` injected so tests need no network. vitest **19 → 26**, `tsc --noEmit` clean.
- **Client**: `KaptureOnlineApiClient` (typed Worker contract), `OnlineAccountService`/`IOnlineAccountService` (DPAPI session via `CloudTokenStore` "online" key, auto-refresh on near-expiry + 401, cached entitlement from `/me`, checkout/portal URLs), `LoopbackGoogleSignIn`, `R2StorageProvider : ICloudStorageProvider` (encrypted vault ↔ R2 via presigned URLs + `vault.db.meta`), DI wiring (`CloudSyncManager` now takes `IEnumerable<ICloudStorageProvider>`), `OnlineAccountViewModel` + the Settings "Online Vault" panel (compiled-binding, entitlement-gated).

**Verification (proof, not assertion):** per slice `dotnet test` (**71 → 118**), Debug **and** Release builds 0/0, `dotnet format --verify` clean; backend `npm test` **26** + `tsc` clean.

**KV-007 (honest status):** secret-less delivered + in use **for the Online Vault**; `GoogleDriveProvider` still bundles `client_secret.json` for Drive → residual closed by new **T-35** (route Drive through the broker).

**Live gating:** inactive until Cloudflare/Stripe/Google are provisioned and `OnlineVaultConfig` (`ApiBaseUrl`+`GoogleClientId`) + Worker secrets (incl. `GOOGLE_CLIENT_SECRET`) are filled. **Docs reconciled → shared `version` 1.10.0:** HANDOFF/ROADMAP/BUGS/TESTING/AUDIT-LOG + CLAUDE; CHANGELOG `[Unreleased]` groundwork note; design doc status → Phases 1–2 built.

**Auditor:** Claude (Opus 4.8). **Next:** provision + go live, or start the P2 backlog (user's stated next step).

---

## 2026-06-01 (PM-2) — v1.0.7 released + kapture.tools "Freeware" rebrand + visibility/doc fixes

**Released v1.0.7** (tag `v1.0.7` = `2d09aa3`; GitHub Release via `auto-release.yml`) — ships the P1 hardening batch (T-16/T-09/T-08) that was staged in CHANGELOG `[Unreleased]`. Tests CI green.

**kapture.tools** is served from a **separate repo** `Kapture.Tools-Website` (cloned to `C:\DEV\Kapture.Tools-Website`), NOT this repo's `docs/`. Changes there: hero badge "Free & Open Source" → **"v{version} - Freeware"**, auto-updated from the KaptureVault Releases API via the existing `download.js` (`42b6199`); fixed the stale download card (v1.0.0 + wrong-repo href → v1.0.7 + KaptureVault asset, `de75682`). That repo has **no GitHub Pages** (404) — it deploys via an external host wired to the repo (verify the live deploy).

**Doc fixes:** the misdirected `docs/index.html` footer edit (that page is a redundant, now-superseded landing page with its own stale `kapture.tools` CNAME) was reverted (`8c33a0b`). Corrected this repo's visibility in CLAUDE.md — **private → public** (gh confirms PUBLIC; the `kapturevault-backend` repo stays private). Managed docs bumped to **version 1.9.0 / app-version 1.0.7**; HANDOFF Start-here de-pinned from an exact SHA (the OneDrive hazard that motivated it is retired).

**Auditor:** Claude (Opus 4.8). **Next:** F-02 Phase 2 and/or the P2 backlog.

---

## 2026-06-01 (PM) — v1.0.6 released, P1 backlog (T-16/T-09/T-08) completed, docs → v1.8.0

**Trigger:** Same-day continuation after the repo relocation (entry below). User: cut v1.0.6 (ship F-01), then run the P1 backlog T-16 → T-09 → T-08.

**v1.0.6 released (single-creator pipeline held):** promoted CHANGELOG `[Unreleased]` (F-01 Export Vault Database) → `[1.0.6] — 2026-06-01`; ran `Invoke-Release.ps1 -BumpType minor` from the new `C:\DEV` location (bumped csproj+iss to 1.0.6, published, built the installer, committed `aee32b5`, tagged `v1.0.6`, pushed). `auto-release.yml` (`github-actions[bot]`) VirusTotal-scanned + created the **GitHub Release v1.0.6** with `KaptureVaultSetup-1.0.6-x64.exe` — verified live via `gh`.

**P1 backlog COMPLETE (test-first, each its own commit; on `main`, unreleased — ships v1.0.7):**
- **T-16** (`ff78e6d`) — `Avalonia.Headless.XUnit` harness (`TestAppBuilder` over the real `App`); `MainWindowViewModelFilterTests` (filter/selection survive a background Refresh) + `MainWindowSmokeTests` (headless MainWindow constructs/binds). Closes KV-045.
- **T-09** (`53f0ad4`) — `SyncEntries` diff-updates `Entries` in place (reuse-by-Id, reorder, trim) replacing `Clear()`+rebuild (KV-013); debounced `RequestRefresh` (KV-032); query/decrypt off the UI thread via `RefreshAsync` + `Task.Run` (KV-033); `CaptureEntry.IsPinned/Tags` made observable; `MainWindowViewModelEntriesDiffTests`.
- **T-08** (`bf3658c`) — centralized idempotent teardown: `ShutdownCoordinator` + `OnShutdownRequested` wired to `desktop.ShutdownRequested`; stops capture, bounded sync-on-close once (restarts pass false via `App.ShutdownForRestart`), disposes tray + ServiceProvider → HotkeyService/CloudSyncManager (KV-011/KV-024); encryption-cancel + the 3 SettingsWindow restart paths now route through it; `ShutdownCoordinatorTests`.

**Verification (proof, not assertion):** at each step `dotnet test` (49 → 56 → 58 → 63 → 71, all passing), `dotnet build -c Release` 0/0, `dotnet format --verify` clean. Backlog pushed (`aee32b5..bf3658c`); `git ls-remote` HEAD = `bf3658c` = origin/main.

**Documentation reconciliation → shared `version` 1.8.0 / `app-version` 1.0.6:** CLAUDE, HANDOFF, ROADMAP, BUGS, TESTING, AUDIT-LOG bumped; CHANGELOG `[Unreleased]` staged for v1.0.7 (UI-responsiveness + clean-shutdown). Marked fixed: KV-011, KV-013, KV-024, KV-032, KV-033, KV-045 (+ KV-010 disposal residual). **All P1 audit issues are now resolved.** ROADMAP↔code, BUGS↔code, TESTING↔suite (8 suites / 71 tests) reconciled.

**Auditor:** Claude (Opus 4.8). **Next:** F-02 Phase 2 (client Online Vault) and/or P2 backlog; cut **v1.0.7** to ship the P1 batch whenever desired.

---

## 2026-06-01 — Repo relocated off OneDrive to C:\DEV\Utilities\KaptureVault

**Trigger:** User — "move the repo folder to `C:\DEV\Utilities` and make it the permanent location, change all known links, then work from there." Then: cut **v1.0.6** (F-01) and run the P1 backlog **T-16 → T-09 → T-08**.

**Move (verified — proof, not assertion):** robocopy of the OneDrive working tree → `C:\DEV\Utilities\KaptureVault`, preserving `.git` and the two gitignored-but-required secrets (`client_secret.json`, `kaptureweb_clientsecret.json`), excluding regenerable `bin/obj/publish`. Verified at the destination *before* deleting the source: `git fsck --connectivity-only` clean (only harmless dangling objects), `git rev-parse HEAD` = **`dc615ce`** = `origin/main`, `git status` clean, and restore + build + **`dotnet test` 49/49 passing**. The OneDrive source was then removed (`Test-Path` → False) and the `C:\DEV` copy reconfirmed intact. The destination `Utilities` is the off-OneDrive twin of the parent repo (already held `Basefra.me` + `Kapture`), and `C:\DEV\` already carries the constitution + six directive files, so KaptureVault's `../../` directive references resolve correctly at the new path.

**Why:** retires the OneDrive tooling hazard (stale / fabricated / empty tool output) documented since 2026-05-31; the client now sits alongside `kapturevault-backend` under `C:\DEV`.

**Reference fixes (the "known links"):** repo-root path updated in `CLAUDE.md`; `HANDOFF.md` "Start here" + the CRITICAL hazard section reframed as RESOLVED with the new `cd` path and corrected expected SHA (`dc615ce`); the "repo hygiene: move off OneDrive" item marked ✅ DONE in HANDOFF + ROADMAP; the CLAUDE Lessons OneDrive-hazard bullet marked RESOLVED; a Session Log entry added; the 4 project memory files updated and copied to the new project key so future `C:\DEV\Utilities` sessions inherit them. The `%LOCALAPPDATA%\KaptureVault` runtime data path is unchanged (independent of repo location). `.claude/plan.md` "OneDrive provider" mentions were left intact — they refer to the cloud-sync *feature*, not the repo.

**Auditor:** Claude (Opus 4.8). **Next:** v1.0.6 release, then T-16 → T-09 → T-08.

---

## 2026-05-31 (PM-2) — F-01 shipped to main + F-02 Phase 1 backend built

**Trigger:** Continuation of the same-day session, *after* the v1.0.5 reconciliation (the PM entry below). Two deliverables landed and a final reconciliation pass took the managed doc set to **`version` 1.7.0** (app-version stays **1.0.5** — F-01 is unreleased).

**Ground truth re-verified first (proof, not assertion — OneDrive tooling hazard):** client `git ls-remote` HEAD **`ddc3ce4` = `origin/main`**; working tree carries only the in-flight doc edits (CLAUDE/HANDOFF/ROADMAP/TESTING already at 1.7.0, this AUDIT-LOG + BUGS being brought up now); `git ls-files` confirms `DatabaseServiceBackupTests.cs`, `ServiceRegistrationTests.cs`, `tests.yml`, `auto-release.yml`, and `docs/F-02-online-vault-design.md` all present (a parallel `Glob` returned empty — a known OneDrive reporting artifact, *not* missing files; git is authoritative). Backend repo present at `C:\dev\kapturevault-backend` with commits `4758a50` + `8795110`. Client test count **49** confirmed by attribute + `[InlineData]` expansion (Capture 4 / Encryption 6 / Search 3 / Replace 1 / Crud 4 / **Backup 2** / ServiceRegistration 13 / Converter 16 = 49); backend **19** `it/test` cases across 4 spec files.

**F-01 (Export Vault Database) IMPLEMENTED — on `main`, UNRELEASED (ships v1.0.6):** an **Export DB** toolbar button → `SaveFilePickerAsync(.db)` → `DatabaseService.CreateBackupCopy` (WAL-safe `VACUUM INTO`) run **off the UI thread**; encrypted vaults export as-is (restoring needs the password; labelled in the tooltip). Regression: `DatabaseServiceBackupTests` (2 — standalone copy with every row opened as an independent file connection + empty-vault still a valid DB). Commit **`ddc3ce4`** (= HEAD = `origin/main`). Client tests **47 → 49**. Staged under CHANGELOG **`[Unreleased]`** → promotes to **v1.0.6**; not yet released.

**F-02 Phase 1 — BACKEND BUILT in a NEW separate private repo `kapturevault-backend`:** `https://github.com/Vybecode-LTD/kapturevault-backend`, on disk `C:\dev\kapturevault-backend` — **deliberately off OneDrive** (the tooling-hazard mitigation). A Cloudflare **Worker** (TypeScript) providing: Google-token verify → first-party **session JWT**; **Stripe** billing + webhook → **D1** subscription state machine; **presigned R2 URLs scoped to `users/{uid}/`**; an **entitlement gate**; and the **D1 schema**. **19 vitest tests + `tsc --noEmit` clean + GitHub Actions CI green** — commits **`4758a50`** (foundation) + **`8795110`** (router/store/billing/webhook/presign). No client code yet; it cannot run live until the human cloud prereqs are filled (Cloudflare R2/Workers/D1, Stripe keys + $49/yr price id, Google OIDC client) — see HANDOFF.

**Decision — KV-007 / T-12 RETIRES via the backend broker (lands in F-02 Phase 2):** with `kapturevault-backend` now brokering Google's desktop token exchange, the client will hold only the public client ID + PKCE and **no `client_secret`**. T-12 is therefore no longer a standalone P1 — the client-side cutover (stop bundling `client_secret.json`, remove `FallbackClientId`) is **F-02 Phase 2** client work. The v1.0.5 installer still bundles `client_secret.json` meanwhile (a Google-"non-confidential", PKCE-protected desktop credential); **do not widen distribution on that assumption** until Phase 2 ships.

**Documentation reconciliation (this pass) — doc set → `version` 1.7.0:** all managed docs now carry shared **`version` 1.7.0** + **`app-version` 1.0.5** (CLAUDE/HANDOFF/ROADMAP/TESTING were bumped earlier this session; AUDIT-LOG + BUGS brought up in this pass — BUGS had lagged at 1.6.0). Reconciled to ground truth across CLAUDE.md, CHANGELOG.md, ROADMAP.md, BUGS.md, TESTING.md, HANDOFF.md: F-01 uniformly **implemented-but-unreleased (ships v1.0.6)**; client test count **49** everywhere; F-02 Phase 1 backend **DONE** in the separate `kapturevault-backend` repo (19 tests + tsc + CI); **KV-007/T-12 retired-via-F-02-backend** (Phase 2), not a standalone open task. Cross-document checks: frontmatter versions uniform (CHANGELOG has none by design); ROADMAP↔code, BUGS↔code, TESTING↔suite (8 suites / 49 tests), CLAUDE↔reality, HANDOFF↔state all consistent; cross-links resolve (`tests.yml`, `auto-release.yml`, `docs/F-02-online-vault-design.md` all verified on disk via `git ls-files`). No CRITICAL/HIGH reconciliation failures; nothing left needing manual review beyond the carried-over human cloud prereqs for F-02 live testing.

**Auditor:** Claude (Opus 4.8), single session. **Next audit due:** next session start, or at the next release (v1.0.6).

---

## 2026-05-31 (PM) — v1.0.5 release, format-debt closure + CI hardening, KV-007 decision, doc reconciliation

**Trigger:** User — "read & give a rundown." That established a verified baseline, then drove the rest of the session: cut **v1.0.5**, close the open `dotnet format` debt and the **KV-007/T-12** question, and re-reconcile every managed doc. This is a *later same-day event* than the P1-batch-2 entry below (which staged v1.0.5 but did not cut it).

**Baseline re-verified first (proof, not assertion):** `git ls-remote` (HEAD `d92952d` = `origin/main`), `git status` (tree clean), `dotnet build` Debug+Release (0 warnings / 0 errors), `dotnet test` (**47 passed / 0 failed / 0 skipped**), `dotnet list package --vulnerable` (none). Only after that did any change proceed.

**v1.0.5 cut (single-creator pipeline held):** `scripts/Invoke-Release.ps1` built/packaged/bumped/committed-CHANGELOG/**pushed**; `.github/workflows/auto-release.yml` (the **single** release creator, `github-actions[bot]`) then VirusTotal-scanned the installer and created the **GitHub Release**. Tag **`v1.0.5` = commit `ffc3c9d`**. `CHANGELOG.md` has the `[1.0.5] — 2026-05-31` section. **v1.0.5 = P1 hardening batch 2:** T-07/KV-012 (SQLite INSERT moved off the WH_KEYBOARD_LL hook thread → bounded `Channel<CaptureEntry>` + single writer task; `Stop()` drains), T-11/KV-006 (PBKDF2 → 600k + persisted KDF params; legacy vaults default to 100k and still open), T-10/KV-010 (`HotkeyService` + `MainWindowViewModel` resolved from DI via `ServiceRegistration.AddKaptureServices()`).

**Corrected doc/tooling drift (DEBUG_PROTOCOL "proof, not assertion" — applied to our own docs & tooling):** prior docs carried two false claims, both OneDrive fabrication-hazard artifacts caught by re-running authoritative commands:
- **"~11–12 commits unpushed"** — FALSE. `git ls-remote` showed HEAD already on `origin/main`; nothing was unpushed. The earlier "push didn't happen" was a stale/fabricated harness result, not reality.
- **"format-clean"** — FALSE. `dotnet format --verify-no-changes` failed with ~130 pre-existing whitespace violations. The repo was *not* clean.
Both were corrected at the point of discovery rather than carried forward — the same "verify with verbatim command output, never trust a prior summary" discipline we apply to code, turned on our own documentation and tooling.

**Format-debt closure + CI hardening (completes the format/vuln half of T-16/KV-045):** the ~130 whitespace failures were fixed across two commits — app **`51dc9fd`** + test project **`12b7122`** — and the whole solution now verifies clean. The gates are now **CI-enforced**: `.github/workflows/tests.yml` (commit **`d92952d`**) runs, on every push/PR to `main` (windows-latest, .NET 9), `dotnet build` → `dotnet format --verify-no-changes` → `dotnet list package --vulnerable --include-transitive` → `dotnet test` (TRX + Cobertura). **VERIFIED GREEN on GitHub** — Actions run **26725669973**, all steps ✓, 2m6s. Remaining T-16 work: `Avalonia.Headless.XUnit` UI smoke + a `MainWindowViewModel` filter-selection regression test.

**KV-007 / T-12 decision (user) — DEFER secret-less OAuth to F-02 Phase 1 (backend broker):** investigated `GoogleDriveProvider` (it *hard-requires* `_clientSecret` — `AuthenticateAsync` refuses without it, and it's sent in the token exchange) and confirmed via Google's docs that the desktop token endpoint still expects `client_secret`. Conclusion: a purely client-side secret-less fix isn't safely confirmable, so the correct fix is the **F-02 backend** brokering the OAuth code/refresh exchange — the client then holds only the public client ID + PKCE and **no secret**. The v1.0.5 installer still bundles `client_secret.json` meanwhile (a Google-"non-confidential", PKCE-protected desktop credential); **distribution should not widen on that assumption.** T-12 is no longer a standalone pre-distribution P1 — it is folded into F-02 Phase 1.

**Documentation reconciliation (this pass):** all managed docs bumped to shared **`version` 1.6.0** and **`app-version` 1.0.5** (was 1.5.0 / 1.0.4; CLAUDE.md had lagged at 1.4.0 and was brought to 1.6.0). Reconciled to ground truth across CLAUDE.md, CHANGELOG.md, ROADMAP.md, BUGS.md, TESTING.md, HANDOFF.md: v1.0.5 marked **shipped** (not staged); test count **47** everywhere (no stale "30"); KV-012/KV-006 ✅ FIXED-in-v1.0.5 and KV-010 🟡 partial (DI done, disposal still needs T-08); KV-007/T-12 uniformly **deferred-to-F-02**; T-16 shown as format/vuln-CI-done with headless + VM-filter regression remaining; CHANGELOG has the `[1.0.5]` entry + tag link. Cross-document checks: frontmatter versions uniform (CHANGELOG has none by design); ROADMAP↔code, BUGS↔code, TESTING↔suite (7 suites / 47 tests), CLAUDE↔reality, HANDOFF↔state all consistent; cross-links resolve (`tests.yml` and `docs/F-02-online-vault-design.md` both verified present on disk). No CRITICAL/HIGH reconciliation failures; no drift left needing manual review.

**Auditor:** Claude (Opus 4.8), single session. **Next audit due:** next session start, or at the next release.

---

## 2026-05-31 — P1 remediation batch 2 (T-07/T-11/T-10), CI, F-02 design + reconciliation

**Trigger:** User — "knock out P1 tech debt first, then F-01, then design F-02 in full." Carried across a context-limit boundary; this entry reconciles the whole 2026-05-31 effort.

**Code delivered (all test-first, RED→GREEN; each its own commit; tests 30 → 47):**
- **T-07 / KV-012** (`e5977dd`) — SQLite INSERT moved **off the WH_KEYBOARD_LL hook thread**. `CaptureService.Flush()` enqueues to a bounded `Channel<CaptureEntry>` (non-blocking `TryWrite`, `AllowSynchronousContinuations=false`); a single writer task (`ProcessWriteQueueAsync`, started in `Start()`) performs Open()+INSERT+AES off-thread; `Stop()` completes+drains (≤5 s) so no final-entry loss. Serializes writes through one writer (was up to 3 threads). Tests: `Flush_DoesNotBlockTheHookThreadOnTheDatabaseWrite`, `Stop_DrainsBufferedEntriesAndDoesNotLoseData`. *(Initial thread-identity test was rejected as flaky — `Stop().Wait()` can inline the writer on the draining thread — and replaced with a non-blocking-behaviour assertion.)*
- **T-11 / KV-006** (`5748f9f`) — PBKDF2 raised to **600k** (OWASP 2023) for new vaults; `encryption.json` persists KDF params (`Iterations`, `Kdf`). `Unlock` derives with the stored count, defaulting pre-T-11 files (no count) to 100k → **existing vaults still open** (no lockout). Tests: `Configure_StoresStrongKdfParams_AndDerivesWithThem`, `Unlock_LegacyVaultWithoutStoredIterations_StillUnlocksAndDecrypts`. Deferred: legacy re-keying (needs KV-021/T-20 transactional bulk path) + Argon2id.
- **T-10 / KV-010** (`0351500`) — composition root extracted to `ServiceRegistration.AddKaptureServices()` (new `ServiceRegistration.cs`); `HotkeyService` + `MainWindowViewModel` resolved from DI instead of `new`'d in `App`. Explicit factory for the VM (mirrors the original ctor; it has a design-time ctor too) → behaviour-identical. New `ServiceRegistrationTests`. Residual KV-015 (View `App.Services` locator) left for T-22.
- **T-16 partial / KV-045** (`24cd3f2`) — `.github/workflows/tests.yml`: CI `dotnet test` on push/PR to main (windows-latest, .NET 9, TRX+Cobertura; ignores docs/releases/installer). Headless smoke + VM filter-regression tests still pending.

**Design delivered:** **F-02** full paid-Online-Vault design (`docs/F-02-online-vault-design.md`, `b57b22d`) — R2 + Workers + D1 + Stripe; per-user namespace; no client secrets (Worker brokers presigned URLs); data model, Worker API, 4 phases, costs, ops/legal, risks. No code. Hard prereq: T-12.

**Resequencing decision:** T-08 (shutdown teardown) + T-09 (Entries diff-update) deferred to pair **after** T-16's headless harness — they're lifecycle/UI with no clean unit test + high regression risk, so they must be verifiable not hoped. T-11 was pulled early as a clean, self-contained win.

**Release decision (user-pinned):** next release is **v1.0.5** with the T-07/T-11/T-10 hardening batch (mirrors v1.0.4's "P1 batch 1"); F-01 becomes a later feature release. CHANGELOG `[Unreleased]` still to be promoted — see HANDOFF "Known loose ends".

**⚠️ Tooling-hazard incident (process, not code):** the repo is on a **OneDrive path** and the tool harness returned **stale / delayed / fabricated / empty** results repeatedly. The first 05-31 pass *reported* commits, a CI file, the F-02 doc, and a `git push` that had **not actually happened** — caught at this session's start by re-running `git log`/`git status`/`Test-Path` (DEBUG_PROTOCOL "verify with proof, not assertion" — applied to our own tooling). Mitigations that held: verify every consequential action with a second authoritative command (esp. git); `Write` (full-file) is safe blind/repeatable; `Edit` is safe-on-failure; one PowerShell per turn (a non-zero exit cancels parallel siblings); build+test after every change. Underlying git/build/test were sound — only result *reporting* was unreliable. **Recommendation logged: move the repo off OneDrive.**

**Docs:** all managed docs bumped to `version` 1.5.0 (app-version stays 1.0.4 until v1.0.5 is cut). BUGS KV-012/KV-006 → FIXED (`60a89ca`); KV-010 / test-count / CHANGELOG reconciliation noted as remaining in HANDOFF. `docs/F-02-online-vault-design.md` added as a non-managed design reference (in `see-also`).

**Auditor:** Claude (Opus 4.8), single-session. **Next audit due:** at the v1.0.5 cut, or next session start.

---

## 2026-05-30 (PM-4) — AlfaFF incident (external) + feature roadmap added

**Trigger:** User reported a "fail-closed network-capture filter" in KaptureVault taking the machine offline; then two new feature requests.

**AlfaFF incident — investigated, root-caused, NOT KaptureVault:**
- Per DEBUG_PROTOCOL, verified the hypothesis against code before changing anything. Searched the entire `Utilities` tree (KaptureVault + the original full Kapture + scaffolding) for `AlfaFF`/WFP/`Fwpm`/`DeviceIoControl`/`.sys`/`fltmc`/driver/packet/TLS-intercept → **zero matches**. KaptureVault's only capture surface is `KeyboardHookService`/`ClipboardMonitorService`/`ScreenshotService` (user-mode); manifest is `asInvoker` (cannot load a boot-start kernel driver); installer ships no driver/SDK. **No code path exists** for KaptureVault to program AlfaFF. Refused to fabricate a fix.
- Read-only machine investigation identified the real owner: **"Monitoring Software" by PCM** (paycomputermonitoring.com, v3.00.0018, installed 2026-05-14), a commercial surveillance product that bundles `AlfaFF.sys`/`.dll` + `instaff.exe` + `PCMFilterService`/`PCMActivityService` in a disguised path (`…\Common Files\Microsoft Shared\IC\bin\`). User resolved it (reboot; AlfaFF already `Start=Disabled`). **No KaptureVault change.**

**Feature roadmap added** (see `ROADMAP.md → 🚀 FEATURE ROADMAP`): **F-01** export vault DB to local disk (free tier, small); **F-02** paid "Online Vault" epic — accounts + Cloudflare R2 + file hosting + share links, $49/yr. Settled decisions: per-user namespace (not bucket-per-user); one feature-gated app (not two versions); no storage/Stripe secrets in the client (backend brokers presigned URLs — makes T-12 a prerequisite). Stack: R2 + Workers + D1 + Stripe + existing Google sign-in. Not started.

**Docs:** bumped to `version` 1.4.0; HANDOFF re-prioritized to lead with F-01/F-02.

---

## 2026-05-30 (PM-3) — release pipeline split + v1.0.4

**Trigger:** User — analysis of the two release "workflows," then "remove `gh release create`," then "add changelog-to-notes and release v1.0.4."

- **Single release creator:** removed `gh release create` (+ the `-SkipGitHub` flag) from `Invoke-Release.ps1`. The local script now only builds/packages/bumps/commits-CHANGELOG/pushes; `.github/workflows/auto-release.yml` is the sole creator. This fixed the race where the local `gh release create` pre-empted the workflow (its VirusTotal step was effectively dead).
- **Richer notes:** the workflow now slices the version's `## [X.Y.Z]` section out of `CHANGELOG.md` (awk `index()`), appends the download/platform/VirusTotal footer, and marks the release `--latest`.
- **Released v1.0.4** (P1 batch 1). **Verified end-to-end:** workflow run succeeded; release created by `github-actions[bot]` with the sliced changelog notes **and** a real VirusTotal badge (the >32 MB upload-URL path worked). First successful run of the workflow as sole creator.
- Docs bumped to `version` 1.3.0 / app 1.0.4; status flips (unreleased → shipped) across CLAUDE/BUGS/ROADMAP/HANDOFF.

---

## 2026-05-30 (PM-2) — P1 hardening + full doc reconciliation

**Trigger:** User — "let's do P1," then "get documentation all aligned and reconciled … prepare for a handoff … note the standing testing, debugging and documentation directives as well as the new release directives."

**P1 code (all test-first / verified):** ✅ KV-008 (gate on all DB methods), KV-009 (name-based column reads), KV-014/023/018 (annotation editor bitmap/RTB disposal + SaveAs guard), KV-013 partial (cached row brushes + 1000-row entry cap). Tests **10 → 30**, all green; app builds 0/0. 6 commits on `main`, unreleased (→ v1.0.4).

**Documentation reconciliation (this pass):**
- Captured the **standing directives** in `CLAUDE.md` (new "STANDING DIRECTIVES" section: Testing, Debugging/anti-loop, Documentation, Release), pointing at the authoritative parent files `../../DEBUG_PROTOCOL.md`, `../../TESTING_PROCEDURES.md`, `../../DOCUMENTATION_MANAGER.md`. Added a **Documentation Map** and a **Lessons** section to `CLAUDE.md`.
- **Version synchronization:** adopted the standard frontmatter field `version` across all managed docs and bumped to the shared **1.2.0** (was split `doc-version`/`app-version`; `app-version` 1.0.3 retained). Added `see-also` cross-links to every doc.
- Reconciled content vs. code: ROADMAP P1 statuses (T-13/14/15 ✅, T-09 🟡, T-16 🟡, T-17 folded into T-15), human/one-time to-dos added; BUGS progress header; TESTING inventory (30 tests) + required-checks directive; HANDOFF rewritten to current state; CHANGELOG Unreleased section.
- **Cross-document checks:** ROADMAP↔code ✅, BUGS↔code ✅ (fixed issues marked + test refs), TESTING↔suite ✅ (6 suites / 30 tests match disk), CHANGELOG↔versions ✅ (1.0.0–1.0.3 entries present; 1.2.0 doc version is the doc-set version, distinct from app version), cross-refs resolve. No CRITICAL/HIGH reconciliation failures found.

**Reconciliation result:** docs aligned at `version` 1.2.0 / app 1.0.3; HANDOFF (canary) accurate. Ready for a fresh session.

---

## 2026-05-30 (PM) — P0 remediation + v1.0.3 release

**Trigger:** User request — "knock them out one by one" (P0 fixes), then "release it + update docs."

**Code fixes (all test-first, RED→GREEN):**
- **KV-005 / KV-034** — self-exclusion derived from `Process.GetCurrentProcess().ProcessName` (was hardcoded `"Kapture"`); clipboard dedupe updated on the self path. `CaptureServiceTests`.
- **KV-002** — `Decrypt` throws `DecryptionException` on tamper/corruption/wrong-key; `ReadEntries` shows a per-row placeholder, `DecryptAllEntries` skips bad rows. `EncryptionServiceTests`.
- **KV-004** — `Search` filters decrypted candidates in memory when encryption is active. `DatabaseServiceSearchTests`.
- **KV-003** — `ReplaceDatabaseFromAsync` retains the `.pre_sync_backup` recovery point (mitigation; full merge deferred). `DatabaseServiceReplaceTests`.

**Test harness stood up (KV-045):** `KaptureVault.Tests` (xUnit + NSubstitute + FluentAssertions) on `KaptureVault.slnx`; persistence seams added (base-dir for `EncryptionService`, connection-string for `DatabaseService`). **10 tests passing.**

**Security (KV-001) — resolved:**
- All Google OAuth secrets **rotated** (desktop client recreated → new ID `…15r8pqq8…`; web secret rotated). New creds written to gitignored files + `%LOCALAPPDATA%`; `FallbackClientId` updated; stale duplicate removed.
- `Utilities` repo history **purged** with `git filter-repo` (secret file removed, all `GOCSPX-…` values scrubbed), force-pushed, local realigned + pruned, release tag restored on the cleaned commit. Verified clean via fresh remote clone. Repo is PRIVATE.
- **Residual:** new secret still bundled in installer → KV-007/T-12 (secret-less OAuth) before wide release.

**Release:** **v1.0.3** cut via `scripts/Invoke-Release.ps1` (CHANGELOG + version bump + installer + tag + GitHub release). Installer in `releases/latest/`.

**Status:** All P0 items closed (KV-003 mitigated). Next: P1.

---

## 2026-05-30 (AM) — Full codebase audit (v1.0.2)

**Trigger:** User request — "full codebase audit, update all docs, document all issues, prepare for handoff."

**Scope:** Entire KaptureVault source — 39 `.cs` files / 11 `.axaml` (~6,142 LOC), installer, git history, and the existing docs. Read-only; no code changed.

**Methodology:** 5 specialized read-only agents run in parallel, each on a distinct dimension, then cross-validated and synthesized:
1. **Architecture / MVVM / lifecycle / threading** (architecture-mvvm)
2. **Data / SQLite / encryption / Drive sync / secret exposure** (data-persistence)
3. **Performance / memory / bitmaps / virtualization** (performance-profiling)
4. **Testing strategy & gap analysis** (testing-qa)
5. **Correctness bug hunt + documentation drift** (general-purpose)

**Findings:** 45 issues catalogued in `BUGS.md` (KV-001…KV-045): 🔴 4 Critical, 🟠 13 High, 🟡 16 Medium, ⚪ 10 Low, 📄 2 Doc/Process. Prioritized remediation in `ROADMAP.md` (T-01…T-33). Test strategy in `TESTING.md`.

**Headline findings:**
- 🔴 **KV-001** Live Google OAuth secrets in parent-repo git history + on disk, **unrevoked** — only human action (Cloud Console) can close it.
- 🔴 **KV-002** AES-GCM decrypt silently returns ciphertext on auth failure (integrity defeated).
- 🔴 **KV-003** Drive sync whole-DB last-write-wins → silent multi-device data loss.
- 🔴 **KV-004** Content search returns nothing when encryption is on (LIKE on ciphertext).
- 🟠 **KV-005** Self-exclusion broken (`SelfProcessName="Kapture"` ≠ runtime `KaptureVault`) — the app captures its own keystrokes/clipboard. **Trivial fix.**

**Verified sound (no action):** the v1.0.2 app/tag filter fix (diff-update + `_suppressFilterRefresh`); `KeyboardHookService` modifier/AltGr/dead-key handling; `HotkeyService` message-only window + STA pump; `CloudTokenStore` DPAPI usage and `drive.file` minimal scope; timer reentrancy guards across all three capture services; `ScreenshotService` DIB offset math and correct self-process name.

### Documentation reconciliation
- **`CLAUDE.md` (local)** — was pervasively stale (described the pre-fork 8-tab "Kapture" v1.0.27 with `requireAdministrator`, `SystemTweaks/`, mutex `…B7E3F4A2`, `%LOCALAPPDATA%\Kapture`). **Rewritten** this session to vault-only reality (v1.0.2, `asInvoker`, mutex `…C9D2E5F6`, `%LOCALAPPDATA%\KaptureVault`). Logged as KV-044 (FIXED).
- **Parent `Development/CLAUDE.md`** — confirmed to be a generic unfilled `REPLACE_WITH_*` constitution template; not KaptureVault-specific, left as-is.
- **Created this session:** `docs/BUGS.md`, `docs/ROADMAP.md`, `docs/TESTING.md`, `docs/AUDIT-LOG.md`, `docs/HANDOFF.md`. Project memory files updated (overview/state/patterns).

### Doc set versioning
This is the first managed documentation set for KaptureVault — doc-version **1.0.0**, app-version **1.0.2**. All `docs/*.md` carry YAML frontmatter per the project constitution.

**Auditor:** Claude (Opus 4.8) orchestrating 5 sub-agents. **Next audit due:** after P0/P1 remediation, or before the next minor release.
