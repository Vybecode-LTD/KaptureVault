namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Owns the Online Vault account/session lifecycle: interactive sign-in (secret-less PKCE via the
/// backend broker), DPAPI-persisted session + refresh tokens with transparent refresh, sign-out,
/// and the cached subscription entitlement that gates paid UI. The single source of truth for
/// "am I signed in / am I paid" across the app — also serves as the entitlement service (KV-007/F-02).
/// </summary>
public interface IOnlineAccountService
{
    bool IsSignedIn { get; }
    string? Uid { get; }

    /// <summary>The signed-in user's email (from <c>/me</c>); null until refreshed or when signed out.</summary>
    string? Email { get; }

    /// <summary>Cached from the last <see cref="RefreshAccountAsync"/>; UI-only — the server re-checks.</summary>
    bool IsPaid { get; }
    string SubscriptionStatus { get; }
    DateTime? CurrentPeriodEndUtc { get; }

    /// <summary>Storage quota (bytes) for the account's tier, cached from the last <c>/me</c>; 0 if unknown.</summary>
    long QuotaBytes { get; }

    /// <summary>Storage used (bytes), cached from the last <c>/me</c>.</summary>
    long UsedBytes { get; }

    string? LastError { get; }

    /// <summary>Raised when sign-in state or entitlement changes (for UI binding).</summary>
    event Action? StateChanged;

    /// <summary>Interactive Google sign-in → backend broker → stored session. True on success.</summary>
    Task<bool> SignInAsync(CancellationToken ct = default);

    // ── Email/password auth (Phase 5) ──────────────────────────────────────────
    /// <summary>Register an email/password account; the backend emails a verification link. True = request accepted (check email). Sets <see cref="LastError"/> on failure (e.g. 409 already-registered).</summary>
    Task<bool> RegisterAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Consume an emailed verification token → stored session (signs in). True on success.</summary>
    Task<bool> VerifyEmailAsync(string token, CancellationToken ct = default);

    /// <summary>Email/password sign-in → stored session. True on success; on failure <see cref="LastError"/> is set and <see cref="NeedsVerification"/> reflects a 403 (unverified email).</summary>
    Task<bool> SignInWithPasswordAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Set after <see cref="SignInWithPasswordAsync"/> when the failure was an unverified email (403).</summary>
    bool NeedsVerification { get; }

    /// <summary>Request a password-reset email. Always reports true unless the call itself errored (no account-existence disclosure).</summary>
    Task<bool> RequestPasswordResetAsync(string email, CancellationToken ct = default);

    /// <summary>Complete a password reset with an emailed token → stored session (signs in). True on success.</summary>
    Task<bool> ResetPasswordAsync(string token, string password, CancellationToken ct = default);

    /// <summary>Clear the stored session and reset cached entitlement.</summary>
    void SignOut();

    /// <summary>Re-read <c>/me</c> to refresh cached entitlement; null if not signed in or it failed.</summary>
    Task<MeResponse?> RefreshAccountAsync(CancellationToken ct = default);

    /// <summary>Create a Stripe Checkout session; returns the URL to open in the browser (null on failure).</summary>
    Task<string?> GetCheckoutUrlAsync(CancellationToken ct = default);

    /// <summary>Create a Stripe Customer Portal session; returns the URL (null if no billing account / failure).</summary>
    Task<string?> GetBillingPortalUrlAsync(CancellationToken ct = default);

    /// <summary>
    /// Mint a one-time web-vault handoff code (P5c) so the browser can auto-log-in. Returns the code,
    /// or null if not signed in or the call fails (the caller then opens the web vault for a manual
    /// sign-in instead). The code conveys only the account session, never the vault encryption key.
    /// </summary>
    Task<string?> CreateWebVaultHandoffCodeAsync(CancellationToken ct = default);

    /// <summary>
    /// Run an authenticated backend call with a valid session, refreshing the session once and
    /// retrying if it is rejected with 401. Throws <see cref="InvalidOperationException"/> if not signed in.
    /// </summary>
    Task<T> ExecuteAuthedAsync<T>(Func<string, CancellationToken, Task<T>> call, CancellationToken ct = default);
}
