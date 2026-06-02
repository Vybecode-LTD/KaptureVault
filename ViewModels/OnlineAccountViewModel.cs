using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kapture.Services;
using Kapture.Services.CloudSync;
using Kapture.Services.CloudSync.Online;

namespace Kapture.ViewModels;

/// <summary>
/// View model for the Settings "Online Vault (KaptureVault Account)" panel (F-02): sign in / out,
/// subscription status, Subscribe / Manage-billing (open the Stripe URLs in the browser), Open Vault,
/// and the entitlement flags the panel binds to. All logic delegates to the tested
/// <see cref="IOnlineAccountService"/>.
/// <para>
/// P5 decouple: the Online Vault is INDEPENDENT of Google Drive backup and is NOT a selectable sync
/// "provider". Once you're signed in with an active vault password, the encrypted vault + screenshots
/// sync automatically (<see cref="Kapture.Services.CloudSync.CloudSyncManager"/>'s own Online-Vault
/// timer) — there is nothing to enable here. This panel is account state only.
/// </para>
/// </summary>
public partial class OnlineAccountViewModel : ObservableObject
{
    private readonly IOnlineAccountService _account;
    private readonly IUrlOpener _urlOpener;
    private readonly OnlineVaultConfig _config;
    private readonly IEncryptionService _encryption;
    private readonly IOnlineVaultSync _sync;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";

    public OnlineAccountViewModel(
        IOnlineAccountService account, IUrlOpener urlOpener, OnlineVaultConfig config,
        IEncryptionService encryption, IOnlineVaultSync sync)
    {
        _account = account;
        _urlOpener = urlOpener;
        _config = config;
        _encryption = encryption;
        _sync = sync;
        _account.StateChanged += OnAccountStateChanged;
    }

    /// <summary>False until the deploy-time Worker URL + sign-in client id are filled in.</summary>
    public bool IsConfigured => _config.IsConfigured;
    public bool IsSignedIn => _account.IsSignedIn;
    public bool IsPaid => _account.IsPaid;
    public string SubscriptionStatus => _account.SubscriptionStatus;
    public string AccountSummary => _account.IsSignedIn
        ? (string.IsNullOrWhiteSpace(_account.Email) ? "Signed in" : $"Signed in as {_account.Email}")
        : "Not signed in";

    // Panel visibility helpers.
    public bool CanSubscribe => _account.IsSignedIn && !_account.IsPaid;
    public bool CanManageBilling => _account.IsSignedIn && _account.IsPaid;

    /// <summary>Main-window "Login" button: shown when the Online Vault is configured and nobody is signed in.</summary>
    public bool ShowLogin => _config.IsConfigured && !_account.IsSignedIn;

    /// <summary>Signed in but no active vault password — the Online Vault is end-to-end encrypted and
    /// cannot sync until one is set. The panel shows the "set a password / sole key" guidance.</summary>
    public bool VaultPasswordRequired => _account.IsSignedIn && !_encryption.IsActive;

    /// <summary>Signed in with a vault password set — the Online Vault syncs automatically.</summary>
    public bool IsSyncingAutomatically => _account.IsSignedIn && _encryption.IsActive;

    /// <summary>True once a signed-in account has a known storage quota (from <c>/me</c>).</summary>
    public bool HasStorageInfo => _account.IsSignedIn && _account.QuotaBytes > 0;

    /// <summary>e.g. "5 MB of 250 MB used" — the account's vault storage against its tier quota.</summary>
    public string StorageSummary => HasStorageInfo
        ? $"{FormatBytes(_account.UsedBytes)} of {FormatBytes(_account.QuotaBytes)} used"
        : "";

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (!_config.IsConfigured)
        {
            StatusMessage = "Online Vault isn't configured in this build yet.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Opening your browser to sign in…";
        try
        {
            if (!await _account.SignInAsync())
            {
                StatusMessage = _account.LastError ?? "Sign-in was cancelled.";
                return;
            }

            await _account.RefreshAccountAsync();
            // Vault sync is FREE (Phase 2) — no subscription needed — but the Online Vault is end-to-end
            // encrypted, so a vault password is REQUIRED before anything can sync (Phase 3 slice B).
            StatusMessage = _encryption.IsActive
                ? "Signed in. Your encrypted vault now syncs to the Online Vault automatically."
                : "Signed in. Set a vault password in Settings → Encryption to start syncing — that " +
                  "password is the only key, so if you lose it your online vault can't be recovered.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sign-in failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        _account.SignOut();
        StatusMessage = "Signed out.";
    }

    /// <summary>
    /// Open the web vault in the browser (shown once signed in). P5c true handoff: first try to mint a
    /// one-time code so the browser auto-logs-in (passed in the URL fragment, never a query — it stays
    /// out of server logs/referrers); if that fails, fall back to the plain URL for a manual sign-in.
    /// </summary>
    [RelayCommand]
    private async Task OpenVaultAsync()
    {
        var code = await _account.CreateWebVaultHandoffCodeAsync();
        var url = string.IsNullOrEmpty(code)
            ? _config.WebVaultUrl
            : $"{_config.WebVaultUrl}#handoff={Uri.EscapeDataString(code)}";
        _urlOpener.Open(url);
    }

    /// <summary>
    /// Manually sync the Online Vault now (the main-window "Sync" button). Gated the same way auto-sync
    /// is: signed in + an active vault password (the Online Vault is end-to-end encrypted).
    /// </summary>
    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (!_account.IsSignedIn) { StatusMessage = "Sign in to the Online Vault first."; return; }
        if (!_encryption.IsActive)
        {
            StatusMessage = "Set a vault password in Settings → Encryption first — the Online Vault is end-to-end encrypted.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Syncing to the Online Vault…";
        try
        {
            var ok = await _sync.SyncOnlineVaultNowAsync();
            StatusMessage = ok ? "Synced to the Online Vault." : "Sync didn't complete — check Settings → Online Vault.";
            await _account.RefreshAccountAsync(); // refresh quota / used after an upload
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubscribeAsync()
    {
        IsBusy = true;
        try
        {
            var url = await _account.GetCheckoutUrlAsync();
            if (url is null)
            {
                StatusMessage = _account.LastError ?? "Couldn't start checkout.";
                return;
            }
            _urlOpener.Open(url);
            StatusMessage = "Complete your subscription in the browser, then choose Refresh.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ManageBillingAsync()
    {
        IsBusy = true;
        try
        {
            var url = await _account.GetBillingPortalUrlAsync();
            if (url is null)
            {
                StatusMessage = _account.LastError ?? "Couldn't open the billing portal.";
                return;
            }
            _urlOpener.Open(url);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _account.RefreshAccountAsync();
            StatusMessage = _account.IsPaid
                ? "Subscription active."
                : $"Subscription: {_account.SubscriptionStatus}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnAccountStateChanged()
    {
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(IsPaid));
        OnPropertyChanged(nameof(SubscriptionStatus));
        OnPropertyChanged(nameof(AccountSummary));
        OnPropertyChanged(nameof(CanSubscribe));
        OnPropertyChanged(nameof(CanManageBilling));
        OnPropertyChanged(nameof(ShowLogin));
        OnPropertyChanged(nameof(HasStorageInfo));
        OnPropertyChanged(nameof(StorageSummary));
        OnPropertyChanged(nameof(VaultPasswordRequired));
        OnPropertyChanged(nameof(IsSyncingAutomatically));
    }

    /// <summary>Friendly byte size (invariant, so display + tests are culture-stable).</summary>
    private static string FormatBytes(long bytes)
    {
        const double KB = 1024, MB = KB * 1024, GB = MB * 1024;
        if (bytes >= GB) return (bytes / GB).ToString("0.#", CultureInfo.InvariantCulture) + " GB";
        if (bytes >= MB) return (bytes / MB).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        if (bytes >= KB) return (bytes / KB).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
        return bytes + " B";
    }
}
