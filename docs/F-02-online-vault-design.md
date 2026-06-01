# F-02 · Paid "Online Vault" — Full Design

> **Status:** Phases 1–2 BUILT (2026-06-01; Phase 2 inactive until provisioned) · **Authored:** 2026-05-31 · **Originating discussion:** `AUDIT-LOG.md` (2026-05-30 PM-4) and `ROADMAP.md → 🚀 FEATURE ROADMAP`.
>
> This is a design reference, **not** one of the shared-version managed docs (it carries no managed `version` frontmatter and is exempt from the version-bump rule). It is the "design F-02 in full" deliverable. Implementation is gated on **T-12** (secret-less desktop OAuth) — see §16.

---

## ⭐ Revision 2 (2026-06-01) — agreed tiering, auth & web model (supersedes v1 where noted)

> Decided with the product owner this session. This revision **intentionally reverses three v1 choices**; where §1–§16 below conflict, **this revision wins**. Phases 1–2 (Google-only, paid-vault) were built first; this is the agreed evolution.

### Tiers (the agreed model)

| Capability | Free (offline) | Free (registered) | Paid — $49/yr |
|---|:---:|:---:|:---:|
| Desktop app · local vault · DB export · Google Drive sync | ✓ | ✓ | ✓ |
| Account — **Google OR email/password** | — | ✓ | ✓ |
| **Online vault sync** (encrypted capture DB **+ re-encoded screenshot images**) + **web vault** access | — | ✓ (≤ **250 MB**) | ✓ (~**10 GB**) |
| **File hosting** (upload arbitrary files) · private/public · **shareable links** | — | — | ✓ |

- **Screenshots** are stored as separate image files today and were never synced; the online vault now also syncs them **re-encoded BMP→PNG/JPEG** (SkiaSharp), **counted against the quota**.
- **Quota:** free **250 MB**, paid **~10 GB** (both config values, tunable pre-launch). The free cap is what nudges heavy users to upgrade.

### Three reversals from v1 (with rationale)

1. **Vault sync is FREE** (v1 gated all `/vault/*` behind an active subscription — §1–2, §9). The paid differentiator is now **file hosting + share links**, not vault sync. Rationale: a free online vault drives adoption; file hosting is the genuinely cost-/abuse-heavy surface and stays paid.
2. **Email/password auth ADDED** alongside Google (v1 said "reuse Google sign-in … no new password store" — §3 decision 3 / §7). Rationale: free users must be able to register without a Google account. Additive at the existing uid-minting seam (`issueSession`/`mintSessionToken`).
3. **A web client for the vault is IN SCOPE** (v1 listed it a non-goal — §2). Rationale: the web vault is *how* free + paid users access their vault online; it reuses the existing WASM + WebCrypto viewer.

### Entitlement boundary (the load-bearing change)

- `/vault/*` → **session-only** (any registered user; FREE). Remove the `requireEntitled` gate.
- `/files/*` (new) → **session + active subscription** (PAID).
- **Quota + a server-pinned object-size cap are now MANDATORY** (free users write to R2): `/vault/put-url` currently signs an *unbounded* PUT and `storage_used` is never maintained — fix that *with* the free tier, not later.
- `/me` returns a `{ tier, features, quota, used }` object so both clients render the right gates from one authoritative field.

### Constraints to honor (surfaced by the design critique)

- **Refresh token ≠ session token** — today both are minted with identical claims/audience, so a 30-day refresh is accepted as a session bearer everywhere. Give refresh a distinct audience/`typ` that `requireSession` rejects, **before** password auth multiplies long-lived credentials.
- **Web-viewer KDF** hardcodes PBKDF2 100k; vaults since T-11 use 600k → they silently fail to decrypt in-browser. Read iterations from `encryption.json`/meta.
- **Account password ≠ vault-encryption password** — the vault key derives locally and is never escrowed, so an account-password reset never recovers the vault. Enforce a hard interlock (distinct entry contexts + an explicit "this does NOT recover your vault" confirmation; consider refusing identical strings). Labels alone are insufficient (silent permanent data-loss risk).
- **PBKDF2 on the Worker** is attacker-triggerable shared CPU → choose the *server* iteration count for the Worker budget (not the desktop's 600k) and **rate-limit `/auth/*`** (needs a new Cloudflare KV/DO/Rate-Limiting binding — not yet provisioned).
- **Quota integrity** — never trust client-reported size in `vault.db.meta`; pin max object size in the presigned signature and/or derive it from R2's actual object size (HEAD/event).
- **CORS** on the Worker **and** on the R2 bucket (presigned URLs are a different origin).

### Build order

0. **Polish** (client): charset fix on the loopback "Connected" page; show email not uid; 402 → clear upsell message.
1. **Desktop panel UX** (client): free/paid layout (Sign in → Open Vault → Upgrade); relocate Export-DB + Run-on-startup into Settings; Upload hidden until paid file hosting exists.
2. **Backend free-vault + foundations:** drop `/vault/*` entitlement; quota + size cap; refresh-token fix; CORS; `/me` tier object; build the `/account` page.
3. **Vault sync v2** (client): multi-object sync (vault.db + re-encoded screenshots), quota-aware; carry salt/KDF in meta for web unlock.
4. **Web vault** (needs the kapture.tools repo-consolidation decision): Google + email/password login → read + decrypt + show screenshots/files → subscribe; KDF 100k→600k fix.
5. **Email/password auth:** register/verify/login/reset + transactional email + rate-limiting + the account-vs-vault-password interlock.
6. **File hosting (paid):** `/files/*` + shares + 250 MB-per-file cap + desktop upload UI + public/private + share links.

**Still open:** confirm the paid cap (~10 GB); the kapture.tools repo-consolidation decision (only blocks Phase 4).

---

## 1. Summary

Add a **paid tier ($49/yr)** to KaptureVault. Registered subscribers get:

1. **Online vault** — their encrypted `vault.db` stored in the cloud (an alternative to / superset of the existing Google Drive sync), reachable from any device.
2. **File hosting** — upload arbitrary files **< 250 MB**, get **share links**, and see uploaded files listed inside the vault UI.

The free tier is unchanged: fully offline, with local **DB export** (F-01) and the existing optional Google Drive sync. **One app, feature-gated** — paid features unlock on login when an active subscription is present.

---

## 2. Goals & non-goals

**Goals**
- A single codebase where paid features light up on authenticated, subscribed login.
- Zero storage/billing secrets shipped in the desktop client.
- Per-user isolation that scales to many users without per-user infrastructure.
- Reuse the existing `ICloudStorageProvider` seam so the online vault is "just another sync provider."
- Keep the client's end-to-end encryption guarantee: the server stores **ciphertext** for vault content (the server is a dumb, access-controlled blob store + metadata DB).

**Non-goals (v1)**
- Real-time multi-device merge of the vault DB (still last-writer-wins at the file level, like Drive sync today — see KV-003/T-06). Online vault v1 is convenience + backup, not collaboration.
- Team / multi-seat accounts.
- Server-side search of encrypted content (impossible without leaking; search stays client-side post-decrypt).
- A web client for the paid vault (the existing read-only mobile viewer at `kapture.tools/vault` stays Drive-based; a paid web client is future work).

---

## 3. Three load-bearing decisions (settled)

1. **Per-user *namespace* in ONE shared bucket** — object keys are prefixed `users/{uid}/…`, **not** a bucket-per-user (R2/S3 cap buckets per account; bucket-per-user does not scale). Access is enforced by the backend, which only ever signs URLs scoped to the caller's own prefix.
2. **One feature-gated app**, not two builds. Free = offline + DB export + Drive sync. Paid features (online vault, file hosting) appear when `subscription.active == true`. No separate "Pro" download.
3. **🔒 No storage or Stripe secrets in the desktop client, ever.** The client never holds R2 keys or the Stripe secret key. A backend **Worker brokers short-lived presigned URLs** (and Stripe Checkout/Portal sessions). Same lesson as the KV-001 OAuth-secret leak, higher stakes — which is exactly why **T-12** (make the *desktop OAuth* client secret-less) is a hard prerequisite: prove the "no secrets in the client" discipline on what we already ship before adding storage credentials.

---

## 4. Recommended stack

| Concern | Choice | Why |
|---|---|---|
| Object storage | **Cloudflare R2** | S3-compatible, **no egress fees** (critical for share-link downloads), cheap at rest |
| Backend API | **Cloudflare Workers** | Lives next to R2 (low latency, can sign R2 URLs directly), generous free tier, no servers to run |
| Metadata DB | **Cloudflare D1** (SQLite) | Co-located with Workers; we already think in SQLite; sufficient for users/subscriptions/files/shares |
| Billing | **Stripe** | Checkout + Customer Portal + webhooks; standard subscription tooling |
| Identity | **Reuse Google sign-in** | KaptureVault already does Google OAuth (PKCE) for Drive; reuse it for account identity → no new password store |
| Client integration | **`R2StorageProvider : ICloudStorageProvider`** | Slots next to `GoogleDriveProvider`; the online vault is a sync provider the user can select |

Everything is on one vendor (Cloudflare) except Stripe and Google — minimal moving parts, mostly free at small scale.

---

## 5. Architecture overview

```
┌────────────────────────┐         ┌───────────────────────────────────────────┐
│  KaptureVault desktop   │         │            Cloudflare Worker (API)          │
│  (one feature-gated app)│         │  - verifies Google ID token                 │
│                         │  HTTPS  │  - checks subscription (D1)                  │
│  Login (Google PKCE) ───┼────────▶│  - brokers presigned R2 URLs (scoped uid)   │
│  R2StorageProvider      │  Bearer │  - Stripe Checkout/Portal session creation  │
│   (ICloudStorageProvider)│  JWT   │  - file/share metadata CRUD (D1)            │
│  File hosting UI        │         └───────┬───────────────┬─────────────────────┘
│  Subscription gate      │                 │               │
└──────────┬──────────────┘                 │ presigned     │ SQL
           │ PUT/GET (presigned, direct)     ▼ URL           ▼
           └────────────────────────▶  ┌──────────┐    ┌──────────┐
                                        │   R2     │    │   D1     │
                                        │ users/   │    │ users    │
                                        │  {uid}/  │    │ subs     │
                                        │   vault/ │    │ files    │
                                        │   files/ │    │ shares   │
                                        └──────────┘    └──────────┘
            Stripe ──webhook──▶ Worker (/stripe/webhook) ──▶ D1 (subscriptions)
            Share link  GET kapture.tools/s/{token} ──▶ Worker ──▶ presigned R2 GET (302)
```

**Key property:** large bytes (vault DB, uploaded files, share downloads) flow **directly between client/visitor and R2** via presigned URLs. The Worker only handles small JSON (auth, metadata, URL signing) — cheap and fast.

---

## 6. Data model (D1)

```sql
-- One row per registered user, keyed by the Google "sub" claim (stable per Google account).
CREATE TABLE users (
  uid             TEXT PRIMARY KEY,        -- internal id (uuid)
  google_sub      TEXT UNIQUE NOT NULL,    -- Google subject claim
  email           TEXT,                    -- support / Stripe customer match (not used for auth)
  stripe_customer TEXT,                    -- Stripe customer id
  created_at      TEXT NOT NULL,
  storage_used    INTEGER NOT NULL DEFAULT 0  -- bytes, maintained on upload/delete for quota
);

-- Subscription state, updated by Stripe webhooks (source of truth = Stripe).
CREATE TABLE subscriptions (
  uid                TEXT PRIMARY KEY REFERENCES users(uid),
  stripe_sub_id      TEXT,
  status             TEXT NOT NULL,        -- active | trialing | past_due | canceled | none
  current_period_end TEXT,                -- ISO; client may cache "entitled until"
  updated_at         TEXT NOT NULL
);

-- Hosted files (NOT the vault DB itself; the vault DB lives at a fixed key, see R2 layout).
CREATE TABLE files (
  id           TEXT PRIMARY KEY,          -- uuid
  uid          TEXT NOT NULL REFERENCES users(uid),
  r2_key       TEXT NOT NULL,             -- users/{uid}/files/{id}/{safe_name}
  display_name TEXT NOT NULL,
  size_bytes   INTEGER NOT NULL,
  content_type TEXT,
  sha256       TEXT,                      -- client-computed integrity check
  created_at   TEXT NOT NULL
);
CREATE INDEX idx_files_uid ON files(uid);

-- Share links (capability tokens → a file). Revocable, optionally expiring.
CREATE TABLE shares (
  token          TEXT PRIMARY KEY,        -- random 22+ char url-safe
  file_id        TEXT NOT NULL REFERENCES files(id),
  uid            TEXT NOT NULL REFERENCES users(uid),
  expires_at     TEXT,                    -- nullable = no expiry
  revoked        INTEGER NOT NULL DEFAULT 0,
  download_count INTEGER NOT NULL DEFAULT 0,
  created_at     TEXT NOT NULL
);
```

**R2 key layout (single bucket):**
```
users/{uid}/vault/vault.db            ← the online vault (encrypted SQLite, client-side AES-GCM)
users/{uid}/vault/vault.db.meta       ← small json: version, mtime, sha256 (for sync conflict checks)
users/{uid}/files/{fileId}/{name}     ← hosted files
```
The Worker **never** signs a key outside `users/{callerUid}/`.

---

## 7. Identity & subscription flow

1. **Sign in (client):** reuse the existing Google OAuth PKCE loopback flow. Request an **ID token** (OIDC) in addition to / instead of the Drive scope. The ID token is a signed JWT containing the `sub` claim.
2. **Exchange (client → Worker `POST /auth/session`):** client sends the Google ID token. The Worker **verifies it** against Google's JWKS (issuer, audience = our client id, expiry, signature), upserts the `users` row keyed by `google_sub`, and returns a **first-party session token** (short-lived JWT signed with a Worker secret, ~1h) + a refresh mechanism. All subsequent calls use `Authorization: Bearer <session JWT>`.
3. **Entitlement:** every paid endpoint checks `subscriptions.status ∈ {active, trialing}` for the caller's `uid`. The client may cache `current_period_end` to show/hide paid UI offline, but the **server always re-checks** before signing any URL.

> Why a first-party session token instead of using the Google ID token directly: short-lived, audience-scoped to our API, lets us embed `uid` + entitlement, and decouples API auth from Google token lifetimes.

---

## 8. The presigned-URL brokering model (security core)

The Worker is the **only** holder of R2 credentials. For every storage operation:

- **Upload vault / file:** client calls `POST /vault/put-url` or `POST /files/put-url` → Worker validates auth + subscription + (for files) quota + size ≤ 250 MB → returns a **presigned PUT URL** scoped to `users/{uid}/…`, valid ~5 min, with enforced `Content-Length`/`Content-Type` where possible.
- **Download vault / file:** `POST /vault/get-url` / `GET /files/{id}/get-url` → presigned **GET URL**, ~5 min.
- **Share download:** visitor hits `GET https://kapture.tools/s/{token}` → Worker looks up the share (not revoked, not expired), increments `download_count`, and **302-redirects** to a fresh short-lived presigned GET. The R2 key is never exposed; the capability is the opaque token.

Presigned URLs are generated with AWS SigV4 against the R2 S3 endpoint using credentials bound as Worker secrets/bindings (or via an R2 binding's presign equivalent). **No client ever sees R2 keys.**

---

## 9. Worker API surface (v1)

| Method & path | Auth | Purpose |
|---|---|---|
| `POST /auth/session` | Google ID token | Verify, upsert user, return session JWT |
| `POST /auth/refresh` | refresh token | Rotate session JWT |
| `GET  /me` | session | Profile + subscription status + storage used/quota |
| `POST /billing/checkout` | session | Create Stripe Checkout session → return URL |
| `POST /billing/portal` | session | Create Stripe Customer Portal session → return URL |
| `POST /stripe/webhook` | Stripe sig | Update `subscriptions` (source of truth) |
| `POST /vault/put-url` | session + sub | Presigned PUT for `vault/vault.db` (+ meta) |
| `POST /vault/get-url` | session + sub | Presigned GET for the vault |
| `GET  /vault/meta` | session + sub | Current vault meta (mtime/sha) for conflict check |
| `POST /files/put-url` | session + sub | Register file row + presigned PUT (enforces 250 MB + quota) |
| `POST /files/{id}/commit` | session + sub | Mark upload complete; set size/sha; update storage_used |
| `GET  /files` | session + sub | List the caller's files |
| `DELETE /files/{id}` | session + sub | Delete file (R2 + row) + cascade shares; adjust storage_used |
| `POST /files/{id}/share` | session + sub | Create share token (optional expiry) |
| `DELETE /shares/{token}` | session + sub | Revoke a share |
| `GET  /s/{token}` | public | Resolve share → 302 presigned GET |

All paid endpoints: 401 if no/invalid session, 402/403 if no active subscription, 413 if size > 250 MB, 429 on rate limit.

---

## 10. Client changes (KaptureVault desktop)

1. **`R2StorageProvider : ICloudStorageProvider`** (`Services/CloudSync/`) — mirrors `GoogleDriveProvider`: `Upload(vaultDb)`, `Download()`, conflict check via `/vault/meta`. Internally: call Worker for a presigned URL, then `HttpClient` PUT/GET the bytes directly to R2. Selectable as a sync provider in Settings (`CloudSyncProvider = "Online Vault"`).
2. **Account/login UI** — a "Sign in to KaptureVault Online" panel in Settings; stores the session/refresh token in the **DPAPI-protected token store** (reuse `CloudTokenStore`). Shows subscription status; "Subscribe / Manage billing" open the Stripe Checkout/Portal URLs in the browser.
3. **Subscription gate** — a small `IEntitlementService` exposing `IsPaid` (from `/me`, cached). Paid UI (online vault provider option, Files tab) binds to it; free users see an upsell.
4. **File hosting UI** — a "Files" section: drag-drop / pick file → enforce 250 MB client-side → request put-url → PUT to R2 → commit. List files with copy-share-link, revoke, delete. Optionally surface hosted files in the vault list as a new entry type.
5. **Encryption stays client-side** for the vault DB (server stores ciphertext). Hosted *files* are stored as-is in v1 (so share links serve them directly); an optional "encrypt uploads" toggle is deferred (it would break share-link usability — recipients couldn't decrypt).

---

## 11. Phases & acceptance

| # | Phase | Where | Acceptance |
|---|-------|-------|-----------|
| **1** | **Backend foundation** — Worker + R2 + D1 + Stripe + Google token verification; `/auth/session`, `/me`, billing, webhook, `/vault/{put,get}-url`, `/vault/meta` | **new repo** (`kapturevault-backend`) | A scripted test can: verify a Google token, create a checkout, flip a sub to active via a webhook fixture, and round-trip a blob to `users/{uid}/vault/` via presigned URLs — with **no R2/Stripe secret leaving the Worker**. |
| **2** | **Client online vault** — `R2StorageProvider`, login UI, token store, subscription gate; online vault selectable as a sync provider | KaptureVault | A paid user can pick "Online Vault", and their encrypted DB syncs up/down across two machines (last-writer-wins, with the existing `.pre_sync_backup` safety). Free users can't select it. |
| **3** | **File hosting** — put-url/commit/list/delete, share tokens, `/s/{token}`, Files UI, 250 MB cap (client + server) | both | A paid user uploads a file, gets a share link a logged-out visitor can download, then revokes it and the link stops working. Quota enforced. |
| **4** | **Ops hardening** — quotas/billing portal polish, account + data deletion, abuse/DMCA handling, rate limiting, monitoring/alerts | both | Account deletion removes all R2 objects + D1 rows + cancels Stripe; a DMCA/abuse takedown path exists; dashboards/alerts on errors and storage growth. |

---

## 12. Cost model (order-of-magnitude)

- **R2:** ~$0.015/GB-month at rest; **$0 egress**. 1,000 users × 1 GB ≈ $15/mo storage. Share-link downloads add no egress cost (R2's headline advantage).
- **Workers + D1:** free tier covers low-thousands of users; paid Workers ~$5/mo + usage well beyond that.
- **Stripe:** ~2.9% + $0.30 per charge → ~$1.72 fee on a $49/yr sub → **~$47.28 net**, minus infra.
- **Break-even:** a handful of subscribers covers infra at small scale. The economics work; **the real cost is the operational/legal surface (Phase 4), not the bytes.**

---

## 13. Operational & legal surface (the actual commitment)

Turning KaptureVault into a hosted product adds standing obligations:
- **ToS / Privacy updates** to cover account data, hosted files, and share links (the OAuth consent screen + `kapture.tools/privacy` + `/tos` already exist for Drive — extend them).
- **Abuse / DMCA**: share links can host arbitrary files → need a reporting path + takedown ability (the `revoked` flag + admin tooling).
- **Data deletion / GDPR**: a real "delete my account and data" flow (R2 + D1 + Stripe).
- **Billing support**: failed payments, refunds, plan changes (Stripe Customer Portal offloads most of this).
- **Security**: the Worker is now an attack surface (auth, rate limiting, presigned-URL scoping must be airtight). Storage credentials live only as Worker secrets/bindings.
- **Quotas / fair use**: per-user storage cap (e.g. 5–20 GB on the $49 tier) enforced via `storage_used`.

---

## 14. Open questions / decisions before build

1. **Vault sync semantics v1:** ship last-writer-wins (like Drive today) or invest in per-entry delta merge (KV-003/T-06) first? *Recommendation: LWW + retained backup for v1; delta merge is its own large project.*
2. **Hosted-file encryption:** store as-is (share links work for anyone) vs client-encrypted (private but share links can't decrypt). *Recommendation: as-is for v1.*
3. **Identity scope:** reuse the Drive OAuth client for sign-in, or a separate minimal OIDC client id? *Recommendation: a separate, minimal sign-in client (cleaner consent, decouples from Drive).*
4. **Pricing/quota exact numbers:** $49/yr confirmed; storage cap + max file count TBD.
5. **Region/data residency:** R2 location hint; any EU-residency promise? (affects ToS).
6. **Free trial?** Stripe trial vs none.

---

## 15. Risks & mitigations

| Risk | Mitigation |
|---|---|
| A storage/Stripe secret leaks into the client (repeat of KV-001) | Architectural: client never holds them; all signing server-side. Enforce via review + the VERSION_CONTROL secret-scan discipline. **T-12 first.** |
| Presigned URL lets a user reach another user's objects | The Worker only ever signs keys under `users/{callerUid}/`; never accept a client-supplied full key. |
| Subscription bypass (client lies about being paid) | Server re-checks D1 (sourced from Stripe webhooks) before signing any URL; client cache is UI-only. |
| Share-link abuse (malware/piracy hosting) | Revocation + expiry + abuse-report path + per-user quotas; reserve the right to terminate. |
| Cost runaway from a heavy user | Quotas + 250 MB cap + monitoring/alerts on `storage_used`. |
| Scope creep into a full hosted product mid-implementation | Phase gates with explicit acceptance; ship Phase 1–2 (vault) before Phase 3 (file hosting). |

---

## 16. Prerequisites & next step

- **Hard prerequisite: T-12** (desktop OAuth becomes secret-less native + loopback PKCE; stop bundling `client_secret.json`). Proves the "no secrets in the client" model on what we already ship before adding storage credentials.
- **Helpful precursor:** F-01 (local DB export) — a manual backup escape hatch independent of any cloud.
- **First build step:** stand up the **new backend repo** and complete **Phase 1** (verifiable in isolation, no client changes). Only then start the client `R2StorageProvider` (Phase 2).

*This document is the "design F-02 in full" deliverable. Phases 1–2 are now built (2026-06-01) — see `ROADMAP.md` + `AUDIT-LOG.md`; Phase 2 is inactive until the cloud accounts are provisioned. The one deviation from §7/§9 as designed: the client uses `POST /auth/google` (the Worker brokers the code→token exchange so the client stays secret-less) rather than completing the Google exchange itself and calling `/auth/session` with an id_token.*
