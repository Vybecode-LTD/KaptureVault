namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Typed client for the KaptureVault Online Vault backend (the Cloudflare Worker). One method per
/// endpoint; throws <see cref="OnlineApiException"/> on a non-success response. Token lifecycle
/// (storing the session, refreshing on 401) is a caller concern — this is the raw HTTP surface,
/// kept deliberately thin so it is trivially mockable in higher-layer tests.
/// </summary>
public interface IKaptureOnlineApiClient
{
    /// <summary><c>GET /health</c> — true if the backend is reachable and healthy.</summary>
    Task<bool> HealthAsync(CancellationToken ct = default);

    /// <summary><c>POST /auth/session</c> — exchange a verified Google ID token for a session.</summary>
    Task<OnlineSession> AuthSessionAsync(string googleIdToken, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /auth/google</c> — the secret-less sign-in path: hand the backend a desktop PKCE
    /// authorization code; it brokers the Google exchange (holding the secret) and returns a session.
    /// </summary>
    Task<OnlineSession> AuthWithCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct = default);

    // ── Email/password auth (Phase 5) ──────────────────────────────────────────
    /// <summary><c>POST /auth/register</c> — create an unverified account; the backend emails a verification link. 409 if already established.</summary>
    Task RegisterAsync(string email, string password, CancellationToken ct = default);

    /// <summary><c>POST /auth/verify</c> — consume an emailed verification token; returns a session (auto-login).</summary>
    Task<OnlineSession> VerifyEmailAsync(string token, CancellationToken ct = default);

    /// <summary><c>POST /auth/login</c> — email/password sign-in. 401 on mismatch; 403 when the email isn't verified.</summary>
    Task<OnlineSession> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary><c>POST /auth/reset-request</c> — request a reset email (always succeeds; no account-existence disclosure).</summary>
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);

    /// <summary><c>POST /auth/reset</c> — set a new password from an emailed reset token; returns a session.</summary>
    Task<OnlineSession> ResetPasswordAsync(string token, string password, CancellationToken ct = default);

    /// <summary><c>POST /auth/refresh</c> — rotate a session token using the refresh token.</summary>
    Task<RefreshedSession> RefreshSessionAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /auth/handoff/create</c> — mint a one-time, short-lived code (P5c) to hand the web vault
    /// so the browser auto-logs-in without repeating Google sign-in. Requires a valid session bearer.
    /// </summary>
    Task<HandoffCode> CreateHandoffCodeAsync(string session, CancellationToken ct = default);

    /// <summary><c>GET /me</c> — profile, subscription status, entitlement, storage used.</summary>
    Task<MeResponse> GetMeAsync(string session, CancellationToken ct = default);

    /// <summary><c>POST /billing/checkout</c> — a Stripe Checkout URL to open in the browser.</summary>
    Task<BillingUrl> CreateCheckoutAsync(string session, CancellationToken ct = default);

    /// <summary><c>POST /billing/portal</c> — a Stripe Customer Portal URL to open in the browser.</summary>
    Task<BillingUrl> CreatePortalAsync(string session, CancellationToken ct = default);

    /// <summary><c>POST /vault/put-url</c> — a presigned URL to PUT the encrypted vault to R2.</summary>
    Task<PresignedUrl> GetVaultPutUrlAsync(string session, CancellationToken ct = default);

    /// <summary><c>POST /vault/get-url</c> — a presigned URL to GET the encrypted vault from R2.</summary>
    Task<PresignedUrl> GetVaultGetUrlAsync(string session, CancellationToken ct = default);

    /// <summary><c>GET /vault/meta</c> — the remote vault's meta for conflict checks, or not-exists.</summary>
    Task<VaultMetaResult> GetVaultMetaAsync(string session, CancellationToken ct = default);

    /// <summary><c>PUT /vault/meta</c> — write the vault meta (mtime/sha/size) after an upload.</summary>
    Task PutVaultMetaAsync(string session, VaultMeta meta, CancellationToken ct = default);

    // ── Vault sub-objects (screenshots) — Phase 3 ──────────────────────────────
    // Each is uploaded/downloaded directly to/from R2 via a presigned URL; the client encrypts the
    // bytes first, so R2 only ever holds ciphertext. The Worker validates keys to `screenshots/<name>`.

    /// <summary><c>POST /vault/object/put-url</c> — presigned PUT URL for a vault sub-object (e.g. <c>screenshots/&lt;name&gt;.enc</c>).</summary>
    Task<PresignedUrl> GetObjectPutUrlAsync(string session, string key, CancellationToken ct = default);

    /// <summary><c>POST /vault/object/get-url</c> — presigned GET URL for a vault sub-object.</summary>
    Task<PresignedUrl> GetObjectGetUrlAsync(string session, string key, CancellationToken ct = default);

    /// <summary><c>POST /vault/object/delete</c> — delete a vault sub-object (orphan cleanup / quota trim).</summary>
    Task DeleteObjectAsync(string session, string key, CancellationToken ct = default);

    /// <summary><c>GET /vault/objects</c> — list every object under the user's vault prefix (relative key + size).</summary>
    Task<VaultObjectList> ListObjectsAsync(string session, CancellationToken ct = default);

    // ── Hosted files (Phase 6 — paid; 402 if the account isn't subscribed) ─────
    /// <summary><c>POST /files/put-url</c> — register a file (+ encrypted flag + virtual folder) and a presigned PUT (enforces 250 MB + quota).</summary>
    Task<FileUploadTicket> CreateFilePutUrlAsync(string session, string name, long size, string? contentType, bool encrypted, string? folder, CancellationToken ct = default);

    /// <summary><c>POST /files/{id}/commit</c> — finish an upload; the server HEADs the real size + banks usage.</summary>
    Task<FileCommitResult> CommitFileAsync(string session, string id, string? sha256, CancellationToken ct = default);

    /// <summary><c>GET /files</c> — the caller's hosted files (newest first).</summary>
    Task<HostedFileList> ListFilesAsync(string session, CancellationToken ct = default);

    /// <summary><c>GET /files/{id}/get-url</c> — a presigned GET URL for the caller's own file.</summary>
    Task<PresignedUrl> GetFileGetUrlAsync(string session, string id, CancellationToken ct = default);

    /// <summary><c>DELETE /files/{id}</c> — delete the file (R2 object + row + cascade its shares).</summary>
    Task DeleteFileAsync(string session, string id, CancellationToken ct = default);

    /// <summary><c>POST /files/{id}/share</c> — create a public share link (optional ISO <paramref name="expiresAt"/>).</summary>
    Task<ShareLink> CreateShareAsync(string session, string id, string? expiresAt, CancellationToken ct = default);

    /// <summary><c>DELETE /shares/{token}</c> — revoke a share link.</summary>
    Task RevokeShareAsync(string session, string token, CancellationToken ct = default);
}
