using System.Globalization;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Default <see cref="IOnlineAccountService"/>: ties together <see cref="IGoogleSignIn"/> (interactive
/// code), <see cref="IKaptureOnlineApiClient"/> (backend), and <see cref="IOnlineTokenStore"/> (DPAPI
/// persistence). Holds the session in memory + on disk, refreshes it transparently when it nears
/// expiry or is rejected mid-flight, and caches the subscription entitlement read from <c>/me</c>.
/// </summary>
public sealed class OnlineAccountService : IOnlineAccountService
{
    /// <summary>Treat the session as expired this many seconds early, to avoid edge-of-expiry 401s.</summary>
    private const int ExpirySkewSeconds = 60;

    private readonly IKaptureOnlineApiClient _api;
    private readonly IGoogleSignIn _signIn;
    private readonly IOnlineTokenStore _store;
    private readonly Func<DateTime> _utcNow;

    private OnlineTokens? _tokens;

    public OnlineAccountService(
        IKaptureOnlineApiClient api,
        IGoogleSignIn signIn,
        IOnlineTokenStore store,
        Func<DateTime>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentNullException.ThrowIfNull(store);
        _api = api;
        _signIn = signIn;
        _store = store;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _tokens = _store.Load();
    }

    public bool IsSignedIn => _tokens is not null;
    public string? Uid => _tokens?.Uid;
    public string? Email { get; private set; }
    public bool IsPaid { get; private set; }
    public string SubscriptionStatus { get; private set; } = "none";
    public DateTime? CurrentPeriodEndUtc { get; private set; }
    public long QuotaBytes { get; private set; }
    public long UsedBytes { get; private set; }
    public string? LastError { get; private set; }

    public event Action? StateChanged;

    public async Task<bool> SignInAsync(CancellationToken ct = default)
    {
        LastError = null;

        GoogleAuthCode? code;
        try
        {
            code = await _signIn.SignInAsync(ct);
        }
        catch (Exception ex)
        {
            LastError = $"Sign-in failed: {ex.Message}";
            return false;
        }

        if (code is null)
        {
            LastError = "Sign-in was cancelled.";
            return false;
        }

        try
        {
            var session = await _api.AuthWithCodeAsync(code.Code, code.CodeVerifier, code.RedirectUri, ct);
            Store(session.Session, session.Refresh, session.Uid, session.ExpiresIn);
            RaiseStateChanged();
            return true;
        }
        catch (OnlineApiException ex)
        {
            LastError = $"Sign-in failed: {ex.Message}";
            return false;
        }
    }

    public void SignOut()
    {
        _store.Clear();
        _tokens = null;
        IsPaid = false;
        Email = null;
        SubscriptionStatus = "none";
        CurrentPeriodEndUtc = null;
        QuotaBytes = 0;
        UsedBytes = 0;
        RaiseStateChanged();
    }

    public async Task<MeResponse?> RefreshAccountAsync(CancellationToken ct = default)
    {
        if (!IsSignedIn) return null;
        try
        {
            var me = await ExecuteAuthedAsync((s, c) => _api.GetMeAsync(s, c), ct);
            IsPaid = me.Entitled;
            Email = me.Email;
            SubscriptionStatus = me.Subscription.Status;
            CurrentPeriodEndUtc = ParseIso(me.Subscription.CurrentPeriodEnd);
            QuotaBytes = me.Quota;
            UsedBytes = me.Used;
            RaiseStateChanged();
            return me;
        }
        catch (OnlineApiException ex) when (ex.IsUnauthorized)
        {
            // Session and refresh both rejected — the account is no longer usable; sign out cleanly.
            SignOut();
            return null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public async Task<string?> GetCheckoutUrlAsync(CancellationToken ct = default)
    {
        if (!IsSignedIn) return null;
        try
        {
            var result = await ExecuteAuthedAsync((s, c) => _api.CreateCheckoutAsync(s, c), ct);
            return result.Url;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public async Task<string?> GetBillingPortalUrlAsync(CancellationToken ct = default)
    {
        if (!IsSignedIn) return null;
        try
        {
            var result = await ExecuteAuthedAsync((s, c) => _api.CreatePortalAsync(s, c), ct);
            return result.Url;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public async Task<T> ExecuteAuthedAsync<T>(Func<string, CancellationToken, Task<T>> call, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        var session = await GetValidSessionAsync(ct);
        try
        {
            return await call(session, ct);
        }
        catch (OnlineApiException ex) when (ex.IsUnauthorized)
        {
            // The session was rejected mid-flight — force one refresh and retry.
            session = await ForceRefreshAsync(ct);
            return await call(session, ct);
        }
    }

    private async Task<string> GetValidSessionAsync(CancellationToken ct)
    {
        var tokens = _tokens ?? throw new InvalidOperationException("Not signed in to the Online Vault.");
        if (_utcNow() < tokens.SessionExpiresAtUtc)
            return tokens.Session;
        return await ForceRefreshAsync(ct);
    }

    private async Task<string> ForceRefreshAsync(CancellationToken ct)
    {
        var tokens = _tokens ?? throw new InvalidOperationException("Not signed in to the Online Vault.");
        var refreshed = await _api.RefreshSessionAsync(tokens.Refresh, ct);
        Store(refreshed.Session, tokens.Refresh, tokens.Uid, refreshed.ExpiresIn);
        return refreshed.Session;
    }

    private void Store(string session, string refresh, string uid, int expiresIn)
    {
        var expiry = _utcNow().AddSeconds(Math.Max(0, expiresIn - ExpirySkewSeconds));
        _tokens = new OnlineTokens(session, refresh, expiry, uid);
        _store.Save(_tokens);
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();

    private static DateTime? ParseIso(string? iso) =>
        DateTime.TryParse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
}
