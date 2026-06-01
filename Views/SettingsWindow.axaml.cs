using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kapture.Services;
using Kapture.Services.CloudSync;
using Kapture.ViewModels;
using Kapture.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;

namespace Kapture.Views;

public partial class SettingsWindow : Window
{
    public bool WasSaved { get; private set; }
    private CloudSyncManager? _syncManager;
    private bool _originalCaptureAdminApps;

    public SettingsWindow()
    {
        InitializeComponent();

        // The ScrollViewer measures its content at unbounded width here, so long wrapping
        // paragraphs never wrap and spill past the cards. Bound the content to the viewer's
        // actual visible width (Bounds minus its horizontal padding) on every size change —
        // a stable, window-driven value that forces wrapping. This is the reliable fix.
        SettingsScroll.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty)
                SettingsContent.MaxWidth = System.Math.Max(0,
                    SettingsScroll.Bounds.Width - SettingsScroll.Padding.Left - SettingsScroll.Padding.Right);
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateEncryptionStatus();

        _syncManager = App.Services.GetService<CloudSyncManager>();
        UpdateCloudStatus();

        // F-02: the Online Vault account panel binds to its own view model (resolved from DI),
        // separate from this window's SettingsViewModel DataContext.
        OnlineVaultPanel.DataContext = App.Services.GetService<OnlineAccountViewModel>();

        // The General card's "Run on startup" button binds to the main view model (the startup
        // command lives there); Export DB is handled below so its picker parents to this window.
        GeneralPanel.DataContext = App.Services.GetService<MainWindowViewModel>();

        // Snapshot the current value so Save_Click can detect a change.
        if (DataContext is SettingsViewModel vm)
            _originalCaptureAdminApps = vm.CaptureAdminApps;
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

    private void UpdateCloudStatus(bool preserveErrorText = false)
    {
        if (_syncManager == null) return;

        var google = _syncManager.Providers["Google Drive"];
        if (google.IsAuthenticated)
            GoogleStatusText.Text = "✓ Connected";
        else if (!preserveErrorText)
            GoogleStatusText.Text = "Not connected";

        GoogleConnectBtn.IsVisible = !google.IsAuthenticated;
        GoogleDisconnectBtn.IsVisible = google.IsAuthenticated;

        SyncStatusText.Text = _syncManager.LastSyncStatus;
    }

    private async void GoogleConnect_Click(object? sender, RoutedEventArgs e)
    {
        if (_syncManager == null) return;
        GoogleConnectBtn.IsEnabled = false;
        GoogleStatusText.Text = "Connecting... (check browser)";

        try
        {
            var provider = _syncManager.Providers["Google Drive"];
            var success = await provider.AuthenticateAsync();

            if (success)
            {
                _syncManager.SetActiveProvider("Google Drive");
                var settings = App.Services.GetRequiredService<ISettingsService>();
                settings.Settings.CloudSyncProvider = "Google Drive";
                settings.Save();
            }
            else
            {
                var reason = provider.LastAuthError ?? "Authorization cancelled or timed out";
                GoogleStatusText.Text = $"⚠ {reason}";
            }
        }
        catch (Exception ex)
        {
            GoogleStatusText.Text = $"⚠ Error: {ex.Message}";
        }
        finally
        {
            GoogleConnectBtn.IsEnabled = true;
            UpdateCloudStatus(preserveErrorText: true);
        }
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

    // ── General (Export DB; Run-on-startup binds to MainWindowViewModel.ToggleStartupCommand) ──

    private async void ExportDb_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Vault Database",
                DefaultExtension = "db",
                SuggestedFileName = $"KaptureVault-backup-{DateTime.Now:yyyyMMdd_HHmmss}.db",
                FileTypeChoices = [new FilePickerFileType("SQLite Database") { Patterns = ["*.db"] }]
            });
            if (file is null) return;

            var path = file.Path.LocalPath;
            // VACUUM INTO requires the destination not to pre-exist; the save dialog already
            // confirmed any overwrite with the user, so deleting first is intended.
            if (File.Exists(path)) File.Delete(path);
            var db = App.Services.GetRequiredService<IDatabaseService>();
            await Task.Run(() => db.CreateBackupCopy(path));
            GeneralStatusText.Text = "Vault database exported.";
        }
        catch (Exception ex)
        {
            GeneralStatusText.Text = $"Export failed: {ex.Message}";
        }
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

        // ── Capture Admin Apps — handle restart if the setting changed ────────
        if (DataContext is SettingsViewModel vm && vm.CaptureAdminApps != _originalCaptureAdminApps)
        {
            if (vm.CaptureAdminApps)
                RestartElevated(vm);
            else
                RestartNormal();
            return; // either path handles the close / shutdown
        }

        Close();
    }

    // Re-launch the process with administrator privileges (UAC elevation).
    // If the user cancels the UAC prompt, we revert the setting and close normally.
    private void RestartElevated(SettingsViewModel vm)
    {
        // Release the mutex NOW so the incoming elevated instance can acquire it.
        Program.PrepareForRestart();

        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas"
            });

            // Elevated instance is starting up — shut this one down.
            (Application.Current as App)?.ShutdownForRestart();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // UAC cancelled
        {
            // Revert the toggle and re-persist so the reverted value is saved.
            vm.CaptureAdminApps = false;
            var svc = App.Services.GetRequiredService<ISettingsService>();
            svc.Settings.CaptureAdminApps = false;
            svc.Save();

            // We already released the mutex above — the app can keep running but
            // single-instance protection is gone for this session. Safest: shutdown.
            (Application.Current as App)?.ShutdownForRestart();
        }
        catch
        {
            // Unknown error — fall back to a plain close.
            Close();
        }
    }

    // Turning off admin capture: settings already saved with CaptureAdminApps=false.
    // We can't programmatically de-elevate, so we shut down and relaunch via the
    // Windows shell (inherits Explorer's medium-integrity token).
    private void RestartNormal()
    {
        Program.PrepareForRestart();

        try
        {
            // explorer.exe starts the target at Explorer's (medium) integrity level,
            // effectively stripping the elevated token.
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Environment.ProcessPath}\"")
            {
                UseShellExecute = false
            });
        }
        catch { /* if the relaunch fails the user can start it manually */ }

        (Application.Current as App)?.ShutdownForRestart();
    }
}
