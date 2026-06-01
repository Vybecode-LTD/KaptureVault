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

    /// <summary>Cached from the last <see cref="RefreshAccountAsync"/>; UI-only — the server re-checks.</summary>
    bool IsPaid { get; }
    string SubscriptionStatus { get; }
    DateTime? CurrentPeriodEndUtc { get; }
    string? LastError { get; }

    /// <summary>Raised when sign-in state or entitlement changes (for UI binding).</summary>
    event Action? StateChanged;

    /// <summary>Interactive Google sign-in → backend broker → stored session. True on success.</summary>
    Task<bool> SignInAsync(CancellationToken ct = default);

    /// <summary>Clear the stored session and reset cached entitlement.</summary>
    void SignOut();

    /// <summary>Re-read <c>/me</c> to refresh cached entitlement; null if not signed in or it failed.</summary>
    Task<MeResponse?> RefreshAccountAsync(CancellationToken ct = default);

    /// <summary>Create a Stripe Checkout session; returns the URL to open in the browser (null on failure).</summary>
    Task<string?> GetCheckoutUrlAsync(CancellationToken ct = default);

    /// <summary>Create a Stripe Customer Portal session; returns the URL (null if no billing account / failure).</summary>
    Task<string?> GetBillingPortalUrlAsync(CancellationToken ct = default);

    /// <summary>
    /// Run an authenticated backend call with a valid session, refreshing the session once and
    /// retrying if it is rejected with 401. Throws <see cref="InvalidOperationException"/> if not signed in.
    /// </summary>
    Task<T> ExecuteAuthedAsync<T>(Func<string, CancellationToken, Task<T>> call, CancellationToken ct = default);
}
