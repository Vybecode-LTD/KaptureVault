# F-02 Online Vault — Go-Live Provisioning Runbook

> **Purpose:** everything a human must do (Cloudflare + Google + Stripe) to take the **built but
> inactive** F-02 Phase 2 Online Vault live. The code is done (client + backend); this is the
> account/secret setup only. Work top-to-bottom — later steps reuse values from earlier ones.
>
> **Repos:** backend = `C:\dev\kapturevault-backend` (the Cloudflare Worker). client = `C:\DEV\Utilities\KaptureVault`.
> Until this is finished the Settings → Online Vault panel shows *"not available in this build yet."*

---

## 0. The values you'll collect (and where each one goes)

You'll end up with ~12 values. There are **three** destinations — keep them straight:

| Value | Destination | Secret? |
|---|---|---|
| Cloudflare **account id** | `wrangler.toml` → `[vars] R2_ACCOUNT_ID` | no (committed) |
| **D1 database id** | `wrangler.toml` → `[[d1_databases]] database_id` | no (committed) |
| Google **sign-in client id** | `wrangler.toml` → `[vars] GOOGLE_CLIENT_ID` **and** client `OnlineVaultConfig.GoogleClientId` | no (public) |
| Worker **deployed URL** | client `OnlineVaultConfig.ApiBaseUrl` | no |
| Google **client secret** | `wrangler secret put GOOGLE_CLIENT_SECRET` | **YES** |
| Stripe **secret key** (`sk_…`) | `wrangler secret put STRIPE_SECRET_KEY` | **YES** |
| Stripe **webhook signing secret** (`whsec_…`) | `wrangler secret put STRIPE_WEBHOOK_SECRET` | **YES** |
| Stripe **price id** (`price_…`, the $49/yr) | `wrangler secret put STRIPE_PRICE_ID` | **YES** |
| R2 **S3 access key id** | `wrangler secret put R2_ACCESS_KEY_ID` | **YES** |
| R2 **S3 secret access key** | `wrangler secret put R2_SECRET_ACCESS_KEY` | **YES** |
| **Session JWT secret** (you generate, 32+ random bytes) | `wrangler secret put SESSION_JWT_SECRET` | **YES** |

`[vars]` already set in `wrangler.toml` and usually fine as-is: `R2_BUCKET="kapturevault"`,
`APP_BASE_URL="https://kapture.tools"`, `SESSION_TTL_SECONDS="3600"`.

**Golden rule:** the 7 "YES" secrets go **only** through `wrangler secret put NAME` (encrypted, never
committed). Never put them in `wrangler.toml` or any client file.

**Prerequisites:** Node.js + the repo's dev deps (`cd C:\dev\kapturevault-backend && npm install`),
then authenticate the CLI once: `npx wrangler login`.

---

## A. Cloudflare (R2 + D1 + Workers)

1. [ ] **Create / sign in to a Cloudflare account** → <https://dash.cloudflare.com>.
2. [ ] **Find your Account ID.** Dashboard → any zone or the **R2** overview → right sidebar shows
       **Account ID**. Copy it → this is `R2_ACCOUNT_ID`. (It also forms the R2 S3 endpoint
       `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`, which the Worker already builds.)
3. [ ] **Create the D1 database** (from `C:\dev\kapturevault-backend`):
       ```bash
       npx wrangler d1 create kapturevault
       ```
       Copy the printed **`database_id`** → paste into `wrangler.toml` under `[[d1_databases]]`
       (replace `REPLACE_WITH_D1_DATABASE_ID`). The `database_name` is already `kapturevault`.
4. [ ] **Create the R2 bucket** (name must match the binding `kapturevault`):
       ```bash
       npx wrangler r2 bucket create kapturevault
       ```
5. [ ] **Create an R2 S3 API token.** Dashboard → **R2** → **Manage R2 API Tokens** → **Create API
       token** → permission **Object Read & Write** (scope to the `kapturevault` bucket). It shows an
       **Access Key ID** and **Secret Access Key** **once** — copy both now. → `R2_ACCESS_KEY_ID`,
       `R2_SECRET_ACCESS_KEY`.
6. [ ] **Generate the session signing secret** (any 32+ random bytes), e.g.:
       ```bash
       node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
       ```
       → `SESSION_JWT_SECRET`.

*(Don't run `wrangler secret put` yet — do all secrets together in Part D, step 3, after the Worker exists.)*

---

## B. Google (OIDC sign-in client)

This is a **dedicated, minimal sign-in client** (separate from the Drive client). Console:
<https://console.cloud.google.com>.

1. [ ] **Pick or create a project** (top project selector). A dedicated "KaptureVault" project is cleanest.
2. [ ] **Configure the OAuth consent screen** → *APIs & Services → OAuth consent screen*:
       - User type **External**.
       - App name **KaptureVault**, your support email, app logo (optional).
       - **Authorized domain:** `kapture.tools`.
       - **App home / Privacy policy / Terms URLs:** `https://kapture.tools`, `https://kapture.tools/privacy`, `https://kapture.tools/tos`.
       - **Scopes:** add `openid` and `email` (that's all the broker needs — no Drive scope here).
       - **Publish** the app (or, while testing, leave it in *Testing* and add your Google account under **Test users**).
3. [ ] **Create the OAuth client** → *APIs & Services → Credentials → Create credentials → OAuth client ID*:
       - **Application type: Web application** (recommended — its secret is genuinely confidential and
         only the Worker will hold it). Name it e.g. "KaptureVault Online sign-in".
       - **Authorized redirect URIs → add exactly:** `http://localhost:48722/`
         *(trailing slash required; this is the client's loopback port `OnlineVaultConfig.LoopbackPort`).*
       - Create → copy the **Client ID** → `GOOGLE_CLIENT_ID` (and the client `GoogleClientId`).
       - Copy the **Client secret** → `GOOGLE_CLIENT_SECRET` (the Worker holds this; the desktop app never sees it).

   > *Alternative:* a **Desktop app** client also works (Google auto-allows loopback, no need to register
   > the port), but its secret is the "non-confidential" kind. Web application is preferred for F-02's
   > "no real secret on the client" goal. Either way the **same Client ID** goes in both `wrangler.toml`
   > and `OnlineVaultConfig`, because the Worker verifies the ID token's audience equals it.

---

## C. Stripe (subscription + webhook)

Dashboard: <https://dashboard.stripe.com>. Do this in **Test mode** first (toggle, top-right), then
repeat the keys for live mode when ready.

1. [ ] **Create the product + price** → *Product catalog → Add product*:
       - Name **KaptureVault Online Vault**.
       - Price: **$49.00**, **Recurring → Yearly**.
       - Save → open the price → copy its **API ID** (`price_…`) → `STRIPE_PRICE_ID`.
2. [ ] **Get the secret API key** → *Developers → API keys* → copy the **Secret key** (`sk_test_…`,
       later `sk_live_…`) → `STRIPE_SECRET_KEY`.
3. [ ] **Create the webhook** *(do this after the Worker is deployed in Part D so you have its URL;
       come back here)* → *Developers → Webhooks → Add endpoint*:
       - **Endpoint URL:** `https://<your-worker-url>/stripe/webhook`.
       - **Events to send:** `customer.subscription.created`, `customer.subscription.updated`,
         `customer.subscription.deleted`.
       - Add endpoint → copy its **Signing secret** (`whsec_…`) → `STRIPE_WEBHOOK_SECRET`.

   > Checkout returns the browser to `https://kapture.tools/account?checkout=success|cancel`
   > (`APP_BASE_URL`). Entitlement is driven by the **webhook → D1**, and the desktop app reads it via
   > **Refresh** (`/me`) — so that return page is cosmetic. A simple `kapture.tools/account` page is nice
   > but not required for the flow to work.

---

## D. Configure + deploy the Worker

From `C:\dev\kapturevault-backend`:

1. [ ] **Edit `wrangler.toml` `[vars]`** with the non-secret values:
       - `GOOGLE_CLIENT_ID` = your Google sign-in **Client ID** (Part B).
       - `R2_ACCOUNT_ID` = your Cloudflare **Account ID** (Part A.2).
       - `[[d1_databases]] database_id` = the **D1 id** (Part A.3).
       - Leave `R2_BUCKET`, `APP_BASE_URL`, `SESSION_TTL_SECONDS` as-is unless changing them.
2. [ ] **Apply the D1 schema** (creates the `users`/`subscriptions`/`files`/`shares` tables):
       ```bash
       npm run db:schema:remote
       ```
3. [ ] **Set the 7 secrets** (paste each value when prompted):
       ```bash
       npx wrangler secret put SESSION_JWT_SECRET
       npx wrangler secret put GOOGLE_CLIENT_SECRET
       npx wrangler secret put STRIPE_SECRET_KEY
       npx wrangler secret put STRIPE_PRICE_ID
       npx wrangler secret put R2_ACCESS_KEY_ID
       npx wrangler secret put R2_SECRET_ACCESS_KEY
       npx wrangler secret put STRIPE_WEBHOOK_SECRET   # paste after creating the webhook (Part C.3)
       ```
4. [ ] **Deploy:** `npm run deploy` (i.e. `wrangler deploy`). Note the printed **Worker URL**
       (e.g. `https://kapturevault-backend.<subdomain>.workers.dev`, or a custom route like
       `https://api.kapture.tools` if you map one). → this is `ApiBaseUrl`.
5. [ ] **Finish Part C.3** (create the Stripe webhook using `https://<worker-url>/stripe/webhook`,
       then `wrangler secret put STRIPE_WEBHOOK_SECRET`).
6. [ ] **Sanity check:** open `https://<worker-url>/health` → should return `{"ok":true}`.

---

## E. Configure + rebuild the client

In `C:\DEV\Utilities\KaptureVault`, edit `Services/CloudSync/Online/OnlineVaultConfig.cs`:

- [ ] Set `DefaultApiBaseUrl` to your **Worker URL** (Part D.4).
- [ ] Set `DefaultGoogleClientId` to the **same Client ID** as `wrangler.toml`'s `GOOGLE_CLIENT_ID` (Part B.3).
- [ ] Leave `LoopbackPort = 48722` (must match the Google redirect URI you registered).

> Tell me when you've done A–D and I'll make the `OnlineVaultConfig` edit for you (or I can wire it to a
> settings file instead of hard-coded defaults if you'd prefer not to bake the URL into the build).

Then rebuild / cut a release:
```powershell
dotnet build -c Debug      # IsConfigured becomes true -> the Online Vault panel activates
```

---

## F. Smoke test (end-to-end)

1. [ ] Launch the app → **Settings → Online Vault** → the panel is active (no "not available" note).
2. [ ] **Sign in with Google** → browser opens → consent → returns "Signed in". (`/auth/google` brokered the code.)
3. [ ] **Subscribe ($49/yr)** → browser opens Stripe Checkout → pay with a Stripe **test card**
       (`4242 4242 4242 4242`, any future expiry/CVC) → return to the app → click **Refresh** →
       status flips to **active** (the webhook updated D1).
4. [ ] Enable Cloud Sync with provider **Online Vault** → **Sync Now** → the encrypted vault uploads
       to R2 (`users/{uid}/vault/vault.db`).
5. [ ] On a **second machine** (or after deleting the local `vault.db`): sign in → Sync → the vault
       downloads and restores (last-writer-wins, with the retained `.pre_sync_backup`).
6. [ ] **Manage billing** → opens the Stripe Customer Portal. Cancelling there → next **Refresh** →
       vault endpoints return 402 and paid features lock.

---

## Go-live notes & gotchas

- **Test vs live Stripe:** do the whole flow in Test mode first. For production, swap `STRIPE_SECRET_KEY`
  to `sk_live_…`, create a live price + live webhook, and re-`wrangler secret put` those.
- **Redirect URI is exact:** `http://localhost:48722/` with the trailing slash. A mismatch → Google
  "redirect_uri_mismatch".
- **Consent screen publishing:** in *Testing* mode only listed test users can sign in; **Publish** before
  real users.
- **Quotas/abuse (Phase 4):** per-user storage cap, the 250 MB file cap, and DMCA/takedown handling are
  Phase 3/4 — not needed to validate Phase 2 vault sync.
- **KV-007 residual:** this provisions the **sign-in** client (secret-less on the desktop). Google **Drive**
  sync still uses its own bundled `client_secret.json` until ROADMAP **T-35** routes it through the broker too.
- **Secrets discipline:** none of the 7 secrets ever go in git or a client file. `wrangler secret put` only.

---

*Once Parts A–D are done and you give me the Worker URL + sign-in Client ID, I'll make the
`OnlineVaultConfig` edit, rebuild, and we'll run Part F together. See `F-02-online-vault-design.md`
for the architecture and `kapturevault-backend/README.md` for backend specifics.*
