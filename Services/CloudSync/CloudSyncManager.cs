using Kapture.Services.CloudSync.Online;
using Timer = System.Timers.Timer;

namespace Kapture.Services.CloudSync;

public class CloudSyncManager : IDisposable
{
    private const string SyncFileName = "vault.db";

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaptureVault", "vault.db");

    private static readonly string SyncMetaPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaptureVault", "sync_meta.json");

    private readonly IDatabaseService _db;
    private readonly IScreenshotSyncService? _screenshotSync;
    private readonly Dictionary<string, ICloudStorageProvider> _providers = new();
    private Timer? _syncTimer;
    private string? _activeProvider;
    private int _syncing; // 0 = idle, 1 = syncing (atomic guard)

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
        // Phase 3 (slice F): the Online Vault also syncs screenshot images (DB rows reference plaintext
        // .bmp files that must be re-encoded + encrypted before upload). Optional so existing
        // construction/tests are unaffected; only used when the active provider is the Online Vault.
        _screenshotSync = screenshotSync;
        // Providers are injected (Google Drive + Online Vault) and keyed by ProviderName so the
        // active one can be selected from settings. (Was: new GoogleDriveProvider() inline.)
        foreach (var provider in providers)
            _providers[provider.ProviderName] = provider;
    }

    public IReadOnlyDictionary<string, ICloudStorageProvider> Providers => _providers;

    public void SetActiveProvider(string? providerName)
    {
        _activeProvider = providerName;
    }

    public ICloudStorageProvider? GetActiveProvider()
    {
        if (_activeProvider != null && _providers.TryGetValue(_activeProvider, out var provider))
            return provider;
        return null;
    }

    public void StartPeriodicSync(int intervalMinutes)
    {
        StopPeriodicSync();
        _syncTimer = new Timer(TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds);
        _syncTimer.Elapsed += async (_, _) => await SyncAsync();
        _syncTimer.Start();
    }

    public void StopPeriodicSync()
    {
        _syncTimer?.Stop();
        _syncTimer?.Dispose();
        _syncTimer = null;
    }

    public async Task<bool> SyncAsync(CancellationToken ct = default)
    {
        // Atomic reentrancy guard — prevents overlapping timer callbacks
        if (Interlocked.CompareExchange(ref _syncing, 1, 0) != 0)
            return false;

        var provider = GetActiveProvider();
        if (provider == null || !provider.IsAuthenticated)
        {
            Interlocked.Exchange(ref _syncing, 0);
            return false;
        }

        UpdateStatus("Syncing...");

        try
        {
            // Check if DB file exists locally
            if (!File.Exists(DbPath))
            {
                UpdateStatus("No local database to sync");
                return false;
            }

            var localModified = File.GetLastWriteTimeUtc(DbPath);

            // Find or upload
            var remoteFileId = await provider.FindFileAsync(SyncFileName, ct);

            bool result;
            // True only after a vault upload or an already-in-sync state — when the local screenshots
            // are the source of truth and should be pushed up (Phase 3 slice F). A download-wins sync
            // instead restores screenshots from the server (slice G) via restoreScreenshots.
            var pushScreenshots = false;
            var restoreScreenshots = false;

            if (remoteFileId == null)
            {
                // First sync — upload
                var uploaded = await UploadSafeCopy(provider, ct);
                UpdateStatus(uploaded
                    ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}"
                    : "Upload failed");
                result = uploaded;
                pushScreenshots = uploaded;
            }
            else
            {
                // Compare timestamps
                var remoteModified = await provider.GetRemoteModifiedTimeAsync(remoteFileId, ct);

                if (remoteModified == null)
                {
                    // Can't get remote time — upload local
                    var uploaded = await UploadSafeCopy(provider, ct);
                    UpdateStatus(uploaded
                        ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}"
                        : "Upload failed");
                    result = uploaded;
                    pushScreenshots = uploaded;
                }
                else if (localModified > remoteModified.Value.AddSeconds(5))
                {
                    // Local is newer — upload
                    var uploaded = await UploadSafeCopy(provider, ct);
                    UpdateStatus(uploaded
                        ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}"
                        : "Upload failed");
                    result = uploaded;
                    pushScreenshots = uploaded;
                }
                else if (remoteModified.Value > localModified.AddSeconds(5))
                {
                    // Remote is newer — download and safely replace
                    var tempPath = DbPath + ".sync_temp";
                    var success = await provider.DownloadFileAsync(remoteFileId, tempPath, ct);
                    if (success && File.Exists(tempPath))
                    {
                        await _db.ReplaceDatabaseFromAsync(tempPath, ct);
                        UpdateStatus($"Downloaded from {provider.ProviderName} at {DateTime.Now:HH:mm}");
                        // Restore the screenshots the downloaded vault references but this device lacks (slice G).
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
    /// Push local screenshots to the Online Vault after the capture DB itself has synced (Phase 3 slice
    /// F). No-op for any other provider. Best-effort: a screenshot-sync failure is surfaced in the
    /// status but never fails the vault sync (which already succeeded).
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
    /// Restore screenshots referenced by the just-downloaded vault that this device is missing (Phase 3
    /// slice G). No-op for any other provider. Best-effort: a restore failure is surfaced in the status
    /// but never fails the vault sync (the DB already downloaded successfully).
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
        StopPeriodicSync();
        GC.SuppressFinalize(this);
    }
}
