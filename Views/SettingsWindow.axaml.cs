using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kapture.Services;
using Kapture.Services.CloudSync;
using Kapture.ViewModels;
using Kapture.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace Kapture.Views;

public partial class SettingsWindow : Window
{
    public bool WasSaved { get; private set; }
    private CloudSyncManager? _syncManager;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateEncryptionStatus();

        _syncManager = App.Services.GetService<CloudSyncManager>();
        UpdateCloudStatus();
    }

    // ── Encryption ──

    private void UpdateEncryptionStatus()
    {
        var enc = App.Services.GetRequiredService<IEncryptionService>();
        if (enc.IsConfigured && enc.IsActive)
        {
            EncryptionStatusText.Text = "✓ Encryption is enabled and active.";
            EnableEncryptionBtn.IsEnabled = false;
            DisableEncryptionBtn.IsEnabled = true;
        }
        else if (enc.IsConfigured)
        {
            EncryptionStatusText.Text = "Encryption is configured but locked.";
            EnableEncryptionBtn.IsEnabled = false;
            DisableEncryptionBtn.IsEnabled = true;
        }
        else
        {
            EncryptionStatusText.Text = "Encryption is not enabled.";
            EnableEncryptionBtn.IsEnabled = true;
            DisableEncryptionBtn.IsEnabled = false;
        }
    }

    private async void EnableEncryption_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new PasswordDialog(PasswordDialog.DialogMode.SetNew);
        await dialog.ShowDialog(this);
        if (!dialog.WasConfirmed || string.IsNullOrEmpty(dialog.ResultPassword)) return;

        var enc = App.Services.GetRequiredService<IEncryptionService>();
        enc.Configure(dialog.ResultPassword);

        var db = App.Services.GetRequiredService<IDatabaseService>();
        var count = db.EncryptAllEntries();

        UpdateEncryptionStatus();
        EncryptionStatusText.Text = $"✓ Encryption enabled. {count} entries encrypted.";
    }

    private async void DisableEncryption_Click(object? sender, RoutedEventArgs e)
    {
        var enc = App.Services.GetRequiredService<IEncryptionService>();
        if (!enc.IsActive)
        {
            var unlockDialog = new PasswordDialog(PasswordDialog.DialogMode.Confirm);
            await unlockDialog.ShowDialog(this);
            if (!unlockDialog.WasConfirmed || !enc.Unlock(unlockDialog.ResultPassword!))
            {
                EncryptionStatusText.Text = "Wrong password. Encryption was not disabled.";
                return;
            }
        }

        var db = App.Services.GetRequiredService<IDatabaseService>();
        var count = db.DecryptAllEntries();
        enc.Disable();

        UpdateEncryptionStatus();
        EncryptionStatusText.Text = $"Encryption disabled. {count} entries decrypted.";
    }

    // ── Cloud Sync ──

    private void UpdateCloudStatus()
    {
        if (_syncManager == null) return;

        var google = _syncManager.Providers["Google Drive"];
        GoogleStatusText.Text = google.IsAuthenticated ? "✓ Connected" : "Not connected";
        GoogleConnectBtn.IsVisible = !google.IsAuthenticated;
        GoogleDisconnectBtn.IsVisible = google.IsAuthenticated;

        SyncStatusText.Text = _syncManager.LastSyncStatus;
    }

    private async void GoogleConnect_Click(object? sender, RoutedEventArgs e)
    {
        if (_syncManager == null) return;
        GoogleConnectBtn.IsEnabled = false;
        GoogleStatusText.Text = "Connecting... (check browser)";

        var provider = _syncManager.Providers["Google Drive"];
        var success = await provider.AuthenticateAsync();

        if (success)
        {
            _syncManager.SetActiveProvider("Google Drive");
            var settings = App.Services.GetRequiredService<ISettingsService>();
            settings.Settings.CloudSyncProvider = "Google Drive";
            settings.Save();
        }

        GoogleConnectBtn.IsEnabled = true;
        UpdateCloudStatus();
    }

    private void GoogleDisconnect_Click(object? sender, RoutedEventArgs e)
    {
        _syncManager?.Providers["Google Drive"].SignOut();
        if (_syncManager?.GetActiveProvider()?.ProviderName == "Google Drive")
            _syncManager.SetActiveProvider(null);
        UpdateCloudStatus();
    }

    private async void SyncNow_Click(object? sender, RoutedEventArgs e)
    {
        if (_syncManager == null) return;

        // If no active provider, pick the first connected one
        if (_syncManager.GetActiveProvider() == null)
        {
            foreach (var (name, provider) in _syncManager.Providers)
            {
                if (provider.IsAuthenticated)
                {
                    _syncManager.SetActiveProvider(name);
                    break;
                }
            }
        }

        SyncNowBtn.IsEnabled = false;
        SyncStatusText.Text = "Syncing...";
        await _syncManager.SyncAsync();
        SyncStatusText.Text = _syncManager.LastSyncStatus;
        SyncNowBtn.IsEnabled = true;
    }

    // ── Theme ──

    private void ApplyTheme_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && Application.Current is App app)
            app.ApplyTheme(vm.SelectedTheme);
    }

    // ── Window ──

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        WasSaved = false;
        Close();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        WasSaved = true;
        Close();
    }
}
