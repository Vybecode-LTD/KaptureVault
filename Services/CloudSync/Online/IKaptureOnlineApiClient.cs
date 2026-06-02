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

    /// <summary><c>POST /auth/refresh</c> — rotate a session token using the refresh token.</summary>
    Task<RefreshedSession> RefreshSessionAsync(string refreshToken, CancellationToken ct = default);

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
}
