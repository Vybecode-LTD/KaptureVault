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
}
