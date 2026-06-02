# F-02 Phase 3 Design — Client Vault-Sync v2 (multi-object, quota-aware, web-unlock-ready)

> **Status: COMPLETE 2026-06-02 — all slices A–H landed.** Decisions locked (§6). Client suite **130 → 162**,
> backend **59** (unchanged in F–H — those are client-only). Companion to `F-02-online-vault-design.md`
> (§ Revision 2) and `F-02-PROVISIONING.md`. Spans **both** repos: `kapturevault-backend` (Worker, slices
> D/E) + `KaptureVault` (client). **Two deliberate deviations from this design were made while building —
> see § 11.**

## 1. Goal

Sync the encrypted vault **and its screenshots** to the Online Vault (R2), quota-aware, so that:
1. A second device can restore the **full** vault including screenshot images.
2. The Phase-4 **web vault** can derive the key, read + decrypt the DB, and display screenshots.
3. Storage stays within the tier quota (250 MB free / 10 GB paid), enforced server-side.

## 2. Non-goals (stay in later phases)
- The **web vault UI** itself (Phase 4; gated on the **T-34** repo-consolidation decision). Phase 3 only makes the data *consumable* by it.
- **File hosting / share links** (Phase 6).
- Per-entry **delta merge** of the DB — Phase 3 keeps the existing **whole-DB last-writer-wins** model (KV-003); multi-device merge remains a known limitation.

## 3. Current baseline
- **One object per user:** `users/{uid}/vault/vault.db` (encrypted SQLite). Synced via presigned PUT/GET; `vault.db.meta` (`mtime/sha/size`) drives last-writer-wins.
- **Screenshots are never synced** and are **plaintext `.bmp`** in `%LOCALAPPDATA%\KaptureVault\screenshots\`.
- **Backend** presigns only the fixed `vaultKey(uid)`; quota = HEAD that one object on the `PUT /vault/meta` commit.
- **Encryption** is per-DB-row content (AES-256-GCM, key = PBKDF2-SHA256(salt, 600k)); `EncryptionService` exposes only string `Encrypt`/`Decrypt`.

## 4. Load-bearing constraints (from the code, not assumed)
1. **Screenshots must be encrypted before upload.** They're plaintext `.bmp` today; raw upload leaks screen captures. Encrypt the (re-encoded) image bytes with the vault key.
2. **Screenshot identity = filename, not path.** `CaptureEntry.Content` stores a device-local absolute path; only the filename (`sc_<timestamp>.bmp`) is portable. The R2 key and the web vault's lookup both derive from the filename.
3. **`vault.db` is content-encrypted, not whole-file.** Non-content columns (app name, window title, timestamps) are already semi-plaintext on R2 today — unchanged by Phase 3. ("Ciphertext only" is, and remains, *conditional on the user enabling encryption*.)
4. **The Online Vault REQUIRES a vault password (DECIDED 2026-06-01).** Everything uploaded is ciphertext — there is **no plaintext-upload path**. Enabling Online Vault sync is gated on an active vault password (`EncryptionService.IsConfigured`/`IsActive`); with none set, the panel prompts the user to set one first. ⚠️ **The vault password is the sole decryption key** — the server holds only ciphertext, so losing it makes the online data unrecoverable; this must be surfaced (with acknowledgement) when enabling. (This also tightens the existing Phase-2 `vault.db` path, which currently uploads regardless of encryption state.)

## 5. Architecture

### 5.1 R2 object layout
```
users/{uid}/vault/vault.db                      (existing — encrypted SQLite)
users/{uid}/vault/vault.db.meta                 (existing — now also carries KDF params, §5.2)
users/{uid}/vault/screenshots/{filename}.enc    (NEW — re-encoded PNG, then AES-GCM encrypted)
```
`{filename}` = the DB's screenshot filename (`sc_<timestamp>.bmp`); the `.enc` blob is the encrypted PNG. Keys are deterministic from the DB, so **no separate manifest is needed for discovery** — reading the DB yields each screenshot's filename → its key.

### 5.2 Web-unlock meta (KDF params) — *slice A, no-regret*
Extend `vault.db.meta` so the browser can derive the key:
```jsonc
{ "mtime": "...", "sha256": "...", "size": 1234, "version": 2,
  "kdf": "PBKDF2-SHA256", "iterations": 600000, "salt": "<base64>",
  "encrypted": true }            // always true — the Online Vault requires a vault password
```
- `EncryptionService` gains a public read-only accessor for `{salt, iterations, kdf, encrypted}` (these are **not secret** — salt + iteration count are public by PBKDF2 design).
- The client writes them into the meta on every upload. The Worker treats meta as opaque JSON (no backend change for this slice).
- Phase 4's web viewer reads `iterations` from here instead of its current hardcoded 100k (the bug noted in the design doc); with 600k vaults it currently fails to decrypt in-browser.

### 5.3 Binary encryption (screenshot blobs)
Add to `EncryptionService` / `IEncryptionService`:
```csharp
byte[] EncryptBytes(byte[] plaintext);   // nonce(12) + tag(16) + ciphertext, no "ENC:" prefix
byte[] DecryptBytes(byte[] blob);        // throws DecryptionException on tamper (mirrors Decrypt)
```
Same AES-GCM construction as the string path, just raw bytes (no base64/prefix). The web vault replicates `DecryptBytes` in WebCrypto (it already does AES-GCM for content strings; same nonce/tag layout).

### 5.4 Screenshot sync pipeline (client)
On each sync, after the DB object settles (§5.7):
1. **Enumerate** the desired set = screenshot entries in the (winning) DB that are **non-expired** and whose local file exists.
2. **Skip unchanged** via a local `online_sync_state.json` (`filename → {uploaded:true, sha}`) so only new screenshots upload. (Screenshots are immutable once captured, so an `uploaded` flag suffices; `sha` is belt-and-suspenders.)
3. For each new one: **re-encode** BMP→PNG (SkiaSharp) → **encrypt** (§5.3 — always; a vault password is required to use the Online Vault) → request a presigned PUT for its key (§5.5) → upload.
4. **Orphan cleanup:** delete R2 screenshot objects not referenced by the DB (entries deleted/expired) — quota hygiene.
5. **Quota-aware:** before uploading, compare `used + pending` against `/me` `quota`; if it won't fit, upload what fits oldest-first… *(decision §6)*, and surface "N screenshots not synced — over quota."

### 5.5 Backend object API (NEW — `kapturevault-backend`)
Generalize beyond the single vault.db key, scoped strictly to the caller's vault namespace:
- `POST /vault/object/put-url` `{ key: "screenshots/<name>.enc" }` → validate `key` matches `^screenshots/[A-Za-z0-9._-]+$` (no `..`, no extra `/`), then presign PUT to `users/{uid}/vault/<key>` (reuses `assertOwnedKey`).
- `POST /vault/object/get-url` `{ key }` → presigned GET (same validation).
- `POST /vault/object/delete` `{ key }` → Worker-side `BUCKET.delete` (orphan cleanup).
- `GET /vault/objects` → `BUCKET.list({ prefix: users/{uid}/vault/ })` → `[{key, size, uploaded}]` (restore discovery + quota display; **paginate** — R2 list caps at 1000/call).

### 5.6 Multi-object quota model
The single-object HEAD no longer suffices. Recommended:
- **Server backstop (authoritative):** on the `PUT /vault/meta` commit, `BUCKET.list({prefix})` and **sum all object sizes** = `storage_used`; if `> quota` → reject `413 {used, quota}` and **do not** advance the meta. (Replaces the Phase-2 single-object HEAD with a prefix-sum.)
- **Client pre-check (UX):** the client uses cached `/me` `used`/`quota` to avoid uploading past the cap and to show usage; the server backstop catches races.
- **Residual (documented):** between presign and commit a client could transiently write over-cap objects (bounded by the short presign TTL); same shape as the Phase-2 single-object residual. A fully-preventive design (reserved-size accounting or per-object commit) is heavier — deferred unless abuse appears.

### 5.7 Conflict model
- **`vault.db` stays whole-file LWW** (unchanged). The winning DB is the source of truth for *which* screenshots should exist.
- **Screenshots follow the DB:** after the DB sync direction is decided, reconcile — upload referenced-but-missing, delete orphaned. On a **download-wins** sync, also download any referenced screenshots the local device lacks and rewrite `Content` to the local cache path (resolve-by-filename).

### 5.8 Restore (second device / fresh install)
1. Sign in → download `vault.db` (existing) → `ReplaceDatabaseFromAsync`.
2. Read screenshot entries → for each, `GET /vault/object/get-url` → download `.enc` → `DecryptBytes` → write the PNG into the local `screenshots\` dir → point `Content` at it.
3. Entries whose screenshot isn't on R2 (older, never synced) resolve to `ScreenshotPath == null` (as today) — no crash.

## 6. Decisions — DECIDED 2026-06-01
1. **Re-encode format: PNG (lossless).** Screenshots are UI/text; JPEG would blur them.
2. **Vault encryption: REQUIRED to use the Online Vault** (chosen over mirror-state). No plaintext-upload path; enabling sync is gated on a vault password; the panel prompts to set one and warns the password is the sole key (lost ⇒ unrecoverable online data). See §4.4.
3. **Sync scope: non-expired screenshots referenced by the DB**, quota-aware.
4. **Over-quota behavior: upload oldest-first until full**, then surface "N not synced — over quota" (do not fail the whole sync).
5. **Key naming:** `screenshots/{originalFilename}.enc` (deterministic from the DB; readable).
6. **Quota enforcement: client pre-check + server prefix-sum backstop** (§5.6).

## 7. Coupling to Phase 4 + T-34
- The web vault (Phase 4) *consumes* §5.2 (KDF meta) + §5.3 (binary decrypt) + §5.5 (`/vault/objects`, `get-url`). Phase 3 is what makes it *possible*; building the viewer is Phase 4 and needs the **T-34** repo-consolidation decision.
- Slice A (KDF meta) is the one piece with hard Phase-4 value that's also **no-regret** to land now.

## 8. Migration & rollout
- No DB schema change (keys derive from existing entries). First sync after upgrade uploads existing screenshots, quota-aware.
- `vault.db.meta` `version` bumps 1 → 2 (adds KDF fields); readers tolerate v1 (no KDF) by falling back (web viewer: 100k legacy).
- Backend changes are additive (new endpoints; the meta-commit quota becomes a prefix-sum) — deploy is backward-compatible with the current client (which simply never calls the new endpoints).

## 9. Testing plan (test-first, RED→GREEN)
**Backend (vitest):** object-presign key validation (reject `..`, reject outside `screenshots/`, accept valid); `/vault/objects` list shape + pagination; multi-object quota = prefix-sum (in-quota 200, over-quota 413+no-meta-advance); delete removes the object + drops `storage_used`.
**Client (xUnit):** `EncryptBytes`/`DecryptBytes` round-trip + tamper→throw; meta carries `salt/iterations/kdf/encrypted`; re-encode BMP→PNG produces a valid decodable image; sync uploads only new screenshots (skip via sync-state); orphan delete; quota pre-check stops at the cap; restore decrypts + writes the image. (FakeR2 in the backend tests already supports `head`/`delete`; extend with `list`.)

## 10. Risks & residuals
- **Vault password is the sole key.** The Online Vault requires it and the server holds only ciphertext, so a lost/forgotten password makes online data unrecoverable — surface an explicit warning + acknowledgement on enable (this is the account-password-≠-vault-password interlock the F-02 critique flagged).
- **Quota race** (§5.6 residual).
- **Device-local paths in `Content`** — resolve-by-filename adds a small reconcile step; the underlying "store full path" debt stays (future cleanup: store just the filename).
- **R2 `list` cost / 1000-key pagination** for large vaults — handle the cursor.
- **Worker CPU** — re-encode/encrypt happen on the **client**; the Worker only presigns + lists (cheap). Good.

## 11. Implementation slices (ordered, each its own commit, test-first)
- **A. Web-unlock meta (client)** — expose KDF params; `VaultMeta` v2; `R2StorageProvider` writes them. *No backend change.* (No-regret; lands independently.)
- **B. Encryption interlock (client)** — refuse to enable/use the Online Vault unless a vault password is set; the panel prompts to set one and shows the "sole key — loss is unrecoverable" warning (acknowledged); `CloudSyncManager`/`R2StorageProvider` refuse when not encrypted. Tests.
- **C. Binary encryption (client)** — `EncryptBytes`/`DecryptBytes` + tamper tests.
- **D. Backend object API** — `/vault/object/{put,get,delete}-url` + `/vault/objects` + key validation + tests.
- **E. Multi-object quota (backend)** — meta-commit prefix-sum; `storage_used` across all objects; tests.
- **F. Screenshot sync pipeline (client)** — ✅ done (`a00ee25`). Client object API (`GetObjectPutUrlAsync`/`GetObjectGetUrlAsync`/`DeleteObjectAsync`/`ListObjectsAsync` + `VaultObject`/`VaultObjectList` + `OnlineApiException.IsPayloadTooLarge`); `SkiaScreenshotImageCodec` (BMP→PNG); `ScreenshotSyncService.SyncUpAsync` (enumerate non-expired → re-encode → `EncryptBytes` → upload; orphan cleanup; quota pre-check oldest-first + meta-recommit/413 trim backstop); wired into `CloudSyncManager`. Tests.
- **G. Restore (client)** — ✅ done (`5cc03e6`). `ScreenshotSyncService.RestoreAsync` (list → download missing → `DecryptBytes` → write to the local screenshots dir by filename); `CloudSyncManager` runs it on download-wins; `CaptureEntry.ScreenshotPath` resolve-by-filename fallback + **all four screenshot read sites repointed to it** (preview, content viewer, Save, annotation editor). Tests.
- **H. UX + docs** — ✅ done. Panel status is delivered via `CloudSyncManager` folding the screenshot result into `LastSyncStatus` (already shown in Settings → `SyncStatusText`): "· N screenshot(s)", "· N not synced — over quota", "· N restored". Managed docs reconciled to v1.15.0. (v1.0.8 deferred to the maintainer pending a fresh live end-to-end smoke.)

### As-built deviations from this design (deliberate, reviewed)
1. **No local `online_sync_state.json` (vs § 5.4.2).** The live remote object list (`GET /vault/objects`) is the source of truth for "already uploaded" — self-correcting across devices/reinstalls, no local cache to drift, and the list is needed anyway for orphan cleanup + quota. Screenshots are immutable (filename = identity), so presence-by-key is a complete signal.
2. **Resolve-by-filename at DISPLAY, not by re-pointing `Content` in the DB (vs § 5.7/5.8 "rewrite/point Content").** Restore leaves the DB untouched and writes the image into `CaptureEntry.ScreenshotDirectory` keyed by filename; `CaptureEntry.ScreenshotPath` falls back to that dir. Mutating `Content` per device would, under whole-DB last-writer-wins, ping-pong device-local paths between devices every sync; the display-fallback achieves the same visible result with **zero** added multi-device churn (and the "store full path" debt of § 10 is untouched). Required pointing every screenshot read (preview/viewer/save/editor) at `ScreenshotPath`.

**Known test gap:** the `CloudSyncManager` push-vs-restore dispatch wiring stays untested (its static `DbPath`/`SyncMetaPath` block unit isolation; same limitation predates Phase 3). The branch logic was verified correct by independent inspection; the pipeline + restore services themselves are fully unit-tested.

## 12. Acceptance criteria
- A new screenshot captured on device 1 appears (decrypted) on device 2 after sync; the server holds only ciphertext.
- Enabling the Online Vault without a vault password is blocked with a clear prompt to set one.
- Exceeding the quota rejects cleanly (413) with a clear in-app message; no partial corruption; `storage_used` accurate.
- `vault.db.meta` carries the KDF params; a 600k vault is decryptable from them (validated end-to-end in Phase 4).
- Backend + client suites green; Release 0/0; `dotnet format` clean; backend `tsc` clean.
