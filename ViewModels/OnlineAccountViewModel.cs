using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kapture.Services;
using Kapture.Services.CloudSync.Online;

namespace Kapture.ViewModels;

/// <summary>
/// View model for the Settings "Online Vault (KaptureVault Account)" panel (F-02 Phase 2): sign in
/// / out, subscription status, Subscribe / Manage-billing (open the Stripe URLs in the browser), and
/// the entitlement flags the panel binds to. All logic delegates to the tested
/// <see cref="IOnlineAccountService"/>; on a paid sign-in it persists the sync-provider choice so the
/// Online Vault becomes the sync target. (Introducing a bindable VM here is a step toward T-22.)
/// </summary>
public partial class OnlineAccountViewModel : ObservableObject
{
    private const string ProviderName = "Online Vault";

    private readonly IOnlineAccountService _account;
    private readonly IUrlOpener _urlOpener;
    private readonly OnlineVaultConfig _config;
    private readonly ISettingsService _settings;
    private readonly IEncryptionService _encryption;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";

    public OnlineAccountViewModel(
        IOnlineAccountService account, IUrlOpener urlOpener, OnlineVaultConfig config, ISettingsService settings,
        IEncryptionService encryption)
    {
        _account = account;
        _urlOpener = urlOpener;
        _config = config;
        _settings = settings;
        _encryption = encryption;
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

    /// <summary>Signed in but no active vault password — the Online Vault is end-to-end encrypted and
    /// cannot be enabled until one is set. The panel shows the "set a password / sole key" guidance.</summary>
    public bool VaultPasswordRequired => _account.IsSignedIn && !_encryption.IsActive;

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
            // encrypted, so a vault password is REQUIRED before we make it the sync target (Phase 3 slice B).
            if (!_encryption.IsActive)
            {
                StatusMessage = "Signed in. Set a vault password in Settings → Encryption to enable the Online " +
                    "Vault — that password is the only key, so if you lose it your online vault can't be recovered.";
                return;
            }
            _settings.Settings.CloudSyncProvider = ProviderName;
            _settings.Settings.CloudSyncEnabled = true;
            _settings.Save();
            StatusMessage = "Signed in. The Online Vault is now your sync target.";
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
        if (_settings.Settings.CloudSyncProvider == ProviderName)
        {
            _settings.Settings.CloudSyncProvider = null;
            _settings.Save();
        }
        StatusMessage = "Signed out.";
    }

    /// <summary>Open the web vault in the browser (shown once signed in).</summary>
    [RelayCommand]
    private void OpenVault() => _urlOpener.Open(_config.WebVaultUrl);

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
        OnPropertyChanged(nameof(HasStorageInfo));
        OnPropertyChanged(nameof(StorageSummary));
        OnPropertyChanged(nameof(VaultPasswordRequired));
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
