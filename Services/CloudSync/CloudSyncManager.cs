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
    private readonly Dictionary<string, ICloudStorageProvider> _providers = new();
    private Timer? _syncTimer;
    private string? _activeProvider;
    private int _syncing; // 0 = idle, 1 = syncing (atomic guard)

    public event Action<string>? OnSyncStatusChanged;
    public string LastSyncStatus { get; private set; } = "Not synced";
    public bool IsSyncing => _syncing != 0;

    public CloudSyncManager(IDatabaseService db)
    {
        _db = db;
        _providers["Google Drive"] = new GoogleDriveProvider();
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

            if (remoteFileId == null)
            {
                // First sync — upload
                var uploaded = await UploadSafeCopy(provider, ct);
                UpdateStatus(uploaded
                    ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}"
                    : "Upload failed");
                return uploaded;
            }

            // Compare timestamps
            var remoteModified = await provider.GetRemoteModifiedTimeAsync(remoteFileId, ct);

            if (remoteModified == null)
            {
                // Can't get remote time — upload local
                var uploaded = await UploadSafeCopy(provider, ct);
                UpdateStatus(uploaded
                    ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}"
                    : "Upload failed");
                return uploaded;
            }

            if (localModified > remoteModified.Value.AddSeconds(5))
            {
                // Local is newer — upload
                var uploaded = await UploadSafeCopy(provider, ct);
                UpdateStatus(uploaded
                    ? $"Uploaded to {provider.ProviderName} at {DateTime.Now:HH:mm}"
                    : "Upload failed");
                return uploaded;
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
                    return true;
                }
                UpdateStatus("Download failed");
                return false;
            }
            else
            {
                UpdateStatus($"In sync with {provider.ProviderName}");
                return true;
            }
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
