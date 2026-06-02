using Kapture.Services.CloudSync.Online;
using Timer = System.Timers.Timer;

namespace Kapture.Services.CloudSync;

/// <summary>
/// Coordinates the two INDEPENDENT cloud features (P5 decouple — they used to share one selectable
/// "active provider", which conflated them):
///   • <b>Google Drive backup</b> — an optional whole-DB dump to the user's Drive (a convenience).
///   • <b>Online Vault</b> — the user's KaptureVault account: the encrypted vault + screenshots on
///     R2, readable in the web vault. Syncs automatically whenever signed in.
/// Each has its own timer and on-demand trigger; both funnel through the shared last-writer-wins
/// <see cref="SyncAsync(ICloudStorageProvider, CancellationToken)"/> (serialized by one guard so the
/// local vault.db is never replaced concurrently). The Online Vault path additionally syncs screenshots.
/// </summary>
public class CloudSyncManager : IDisposable
{
    private const string SyncFileName = "vault.db";
    public const string DriveProviderName = "Google Drive";
    public const string OnlineVaultProviderName = "Online Vault";

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaptureVault", "vault.db");

    private readonly IDatabaseService _db;
    private readonly IScreenshotSyncService? _screenshotSync;
    private readonly Dictionary<string, ICloudStorageProvider> _providers = new();
    private Timer? _driveTimer;
    private Timer? _onlineTimer;
    private int _syncing; // 0 = idle, 1 = syncing (atomic guard; serializes Drive + Online so DB replace can't race)

    public event Action<string>? OnSyncStatusChanged;
    public string LastSyncStatus { get; private set; } = "Not synced";
    public bool IsSyncing => _syncing != 0;

    /// <summary>Outcome of the most recent screenshot sync pass (Online Vault only), for the UI. Null until one runs.</summary>
    public ScreenshotSyncResult? LastScreenshotSync { get; private set; }

    /// <summary>Outcome of the most recent screenshot restore pass (Online Vault only), for the UI. Null until one runs.</summary>
    public ScreenshotRestoreResult? LastScreenshotRestore { get; private set; }

    public CloudSyncManager(
        IDatabaseService db,
        IEnumerable<ICloudStorageProvider> providers,
        IScreenshotSyncService? screenshotSync = null)
    {
        _db = db;
        // The Online Vault path also syncs screenshot images (re-encoded + encrypted). Optional so
        // existing construction/tests are unaffected; only used for the R2 (Online Vault) provider.
        _screenshotSync = screenshotSync;
        foreach (var provider in providers)
            _providers[provider.ProviderName] = provider;
    }

    public IReadOnlyDictionary<string, ICloudStorageProvider> Providers => _providers;

    private ICloudStorageProvider? Provider(string name) =>
        _providers.TryGetValue(name, out var p) ? p : null;

    // ── Google Drive backup (independent) ───────────────────────────────────────
    public void StartDriveBackup(int intervalMinutes)
    {
        StopDriveBackup();
        _driveTimer = new Timer(TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds);
        _driveTimer.Elapsed += async (_, _) => await SyncDriveNowAsync();
        _driveTimer.Start();
    }

    public void StopDriveBackup()
    {
        _driveTimer?.Stop();
        _driveTimer?.Dispose();
        _driveTimer = null;
    }

    /// <summary>Back up the vault to Google Drive now (no-op if Drive isn't connected).</summary>
    public Task<bool> SyncDriveNowAsync(CancellationToken ct = default)
    {
        var p = Provider(DriveProviderName);
        return p is { IsAuthenticated: true } ? SyncAsync(p, ct) : Task.FromResult(false);
    }

    // ── Online Vault sync (independent) ─────────────────────────────────────────
    public void StartOnlineVaultSync(int intervalMinutes)
    {
        StopOnlineVaultSync();
        _onlineTimer = new Timer(TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds);
        _onlineTimer.Elapsed += async (_, _) => await SyncOnlineVaultNowAsync();
        _onlineTimer.Start();
    }

    public void StopOnlineVaultSync()
    {
        _onlineTimer?.Stop();
        _onlineTimer?.Dispose();
        _onlineTimer = null;
    }

    /// <summary>Sync the Online Vault now (no-op unless signed in). The vault + screenshots go to R2.</summary>
    public Task<bool> SyncOnlineVaultNowAsync(CancellationToken ct = default)
    {
        var p = Provider(OnlineVaultProviderName);
        return p is { IsAuthenticated: true } ? SyncAsync(p, ct) : Task.FromResult(false);
    }

    /// <summary>
    /// Last-writer-wins sync of vault.db to/from a SPECIFIC provider, plus screenshots for the Online
    /// Vault. Serialized by <see cref="_syncing"/> so Drive and Online passes never replace the local
    /// DB concurrently.
    /// </summary>
    public async Task<bool> SyncAsync(ICloudStorageProvider provider, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (Interlocked.CompareExchange(ref _syncing, 1, 0) != 0)
            return false;

        if (!provider.IsAuthenticated)
        {
            Interlocked.Exchange(ref _syncing, 0);
            return false;
        }

        UpdateStatus("Syncing...");

        try
        {
            if (!File.Exists(DbPath))
            {
                UpdateStatus("No local database to sync");
                return false;
            }

            var localModified = File.GetLastWriteTimeUtc(DbPath);
            var remoteFileId = await provider.FindFileAsync(SyncFileName, ct);

            bool result;
            var pushScreenshots = false;
            var restoreScreenshots = false;

            if (remoteFileId == null)
            {
                var uploaded = await UploadSafeCopy(provider, ct);
                UpdateStatus(uploaded ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}" : "Upload failed");
                result = uploaded;
                pushScreenshots = uploaded;
            }
            else
            {
                var remoteModified = await provider.GetRemoteModifiedTimeAsync(remoteFileId, ct);

                if (remoteModified == null)
                {
                    var uploaded = await UploadSafeCopy(provider, ct);
                    UpdateStatus(uploaded ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}" : "Upload failed");
                    result = uploaded;
                    pushScreenshots = uploaded;
                }
                else if (localModified > remoteModified.Value.AddSeconds(5))
                {
                    var uploaded = await UploadSafeCopy(provider, ct);
                    UpdateStatus(uploaded ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}" : "Upload failed");
                    result = uploaded;
                    pushScreenshots = uploaded;
                }
                else if (remoteModified.Value > localModified.AddSeconds(5))
                {
                    var tempPath = DbPath + ".sync_temp";
                    var success = await provider.DownloadFileAsync(remoteFileId, tempPath, ct);
                    if (success && File.Exists(tempPath))
                    {
                        await _db.ReplaceDatabaseFromAsync(tempPath, ct);
                        UpdateStatus($"Downloaded from {provider.ProviderName} at {DateTime.Now:HH:mm}");
                        result = true;
                        restoreScreenshots = true;
                    }
                    else
                    {
                        UpdateStatus("Download failed");
                        result = false;
                    }
                }
                else
                {
                    UpdateStatus($"In sync with {provider.ProviderName}");
                    result = true;
                    pushScreenshots = true;
                }
            }

            if (pushScreenshots)
                await SyncScreenshotsUpAsync(provider, ct);
            else if (restoreScreenshots)
                await RestoreScreenshotsDownAsync(provider, ct);

            return result;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Sync error: {ex.Message}");
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _syncing, 0);
        }
    }

    /// <summary>
    /// Push local screenshots to the Online Vault after the capture DB synced (Phase 3 slice F).
    /// No-op for any other provider. Best-effort: a failure is surfaced but never fails the vault sync.
    /// </summary>
    private async Task SyncScreenshotsUpAsync(ICloudStorageProvider provider, CancellationToken ct)
    {
        if (_screenshotSync is null || provider is not R2StorageProvider)
            return;

        try
        {
            var r = await _screenshotSync.SyncUpAsync(ct);
            LastScreenshotSync = r;
            if (r.Ran && (r.Uploaded > 0 || r.OrphansDeleted > 0 || r.NotSyncedOverQuota > 0))
            {
                var overQuota = r.NotSyncedOverQuota > 0
                    ? $" ({r.NotSyncedOverQuota} not synced — over quota)"
                    : string.Empty;
                UpdateStatus($"{LastSyncStatus} · {r.Uploaded} screenshot(s){overQuota}");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"{LastSyncStatus} · screenshot sync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Restore screenshots referenced by the just-downloaded vault that this device lacks (Phase 3
    /// slice G). No-op for any other provider. Best-effort.
    /// </summary>
    private async Task RestoreScreenshotsDownAsync(ICloudStorageProvider provider, CancellationToken ct)
    {
        if (_screenshotSync is null || provider is not R2StorageProvider)
            return;

        try
        {
            var r = await _screenshotSync.RestoreAsync(ct);
            LastScreenshotRestore = r;
            if (r.Ran && r.Restored > 0)
                UpdateStatus($"{LastSyncStatus} · {r.Restored} screenshot(s) restored");
        }
        catch (Exception ex)
        {
            UpdateStatus($"{LastSyncStatus} · screenshot restore error: {ex.Message}");
        }
    }

    private async Task<bool> UploadSafeCopy(ICloudStorageProvider provider, CancellationToken ct)
    {
        var tempPath = DbPath + ".upload_temp";
        try
        {
            _db.CreateBackupCopy(tempPath);
            var id = await provider.UploadFileAsync(tempPath, SyncFileName, ct);
            return id != null;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private void UpdateStatus(string status)
    {
        LastSyncStatus = status;
        OnSyncStatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        StopDriveBackup();
        StopOnlineVaultSync();
        GC.SuppressFinalize(this);
    }
}
