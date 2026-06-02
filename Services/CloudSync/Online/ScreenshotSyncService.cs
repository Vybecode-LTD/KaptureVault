using System.Net.Http.Headers;
using Kapture.Models;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Default <see cref="IScreenshotSyncService"/>. Uploads encrypted, re-encoded screenshots to the
/// user's vault namespace on R2 (<c>screenshots/&lt;filename&gt;.enc</c>) via the backend object API, using
/// the live remote object list as the source of truth for what is already uploaded (robust across
/// devices and reinstalls — no local sync-state file to drift). Quota is enforced client-side as a
/// pre-check (sum of all vault objects vs the tier quota) and server-side as a backstop at the
/// vault-meta commit (prefix-sum → 413), on which this trims the newest uploads and retries.
/// </summary>
public sealed class ScreenshotSyncService : IScreenshotSyncService
{
    private const string ScreenshotKeyPrefix = "screenshots/";
    private const string EncryptedSuffix = ".enc";

    private readonly IOnlineAccountService _account;
    private readonly IKaptureOnlineApiClient _api;
    private readonly HttpClient _r2Http;
    private readonly IEncryptionService _encryption;
    private readonly IScreenshotImageCodec _codec;
    private readonly IDatabaseService _db;
    private readonly Func<DateTime> _utcNow;

    public ScreenshotSyncService(
        IOnlineAccountService account,
        IKaptureOnlineApiClient api,
        HttpClient r2Http,
        IEncryptionService encryption,
        IScreenshotImageCodec codec,
        IDatabaseService db,
        Func<DateTime>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(r2Http);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(db);
        _account = account;
        _api = api;
        _r2Http = r2Http;
        _encryption = encryption;
        _codec = codec;
        _db = db;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<ScreenshotSyncResult> SyncUpAsync(CancellationToken ct = default)
    {
        // The Online Vault is end-to-end encrypted — never sync screenshots without a vault password.
        if (!_account.IsSignedIn || !_encryption.IsActive)
            return ScreenshotSyncResult.NotRun;

        // Fresh quota for the client-side pre-check (the server prefix-sum at meta-commit is the backstop).
        await _account.RefreshAccountAsync(ct);
        var quota = _account.QuotaBytes;

        return await _account.ExecuteAuthedAsync((session, c) => SyncUpCoreAsync(session, quota, c), ct);
    }

    private async Task<ScreenshotSyncResult> SyncUpCoreAsync(string session, long quota, CancellationToken ct)
    {
        var desired = EnumerateDesiredScreenshots(); // oldest-first, deduped by filename

        // The remote object list is authoritative for "what is already uploaded" + current usage.
        var remote = await _api.ListObjectsAsync(session, ct);
        var remoteScreenshots = remote.Objects
            .Where(o => o.Key.StartsWith(ScreenshotKeyPrefix, StringComparison.Ordinal))
            .ToDictionary(o => o.Key, o => o.Size, StringComparer.Ordinal);
        var used = remote.Objects.Sum(o => o.Size);

        var desiredKeys = desired
            .Select(s => ScreenshotKey(s.Filename))
            .ToHashSet(StringComparer.Ordinal);

        var uploaded = 0;
        var orphans = 0;
        var notSynced = 0;
        var changed = false;
        var uploadedThisRun = new List<UploadedObject>();

        // 1. Orphan cleanup: R2 screenshots the DB no longer references (deleted / expired) → delete.
        foreach (var (key, size) in remoteScreenshots.Where(kv => !desiredKeys.Contains(kv.Key)).ToList())
        {
            await _api.DeleteObjectAsync(session, key, ct);
            remoteScreenshots.Remove(key);
            used -= size;
            orphans++;
            changed = true;
        }

        // 2. Upload the screenshots not already on R2, oldest-first, stopping at the quota.
        var candidates = desired.Where(s => !remoteScreenshots.ContainsKey(ScreenshotKey(s.Filename))).ToList();
        for (var i = 0; i < candidates.Count; i++)
        {
            var shot = candidates[i];
            byte[] blob;
            try
            {
                var png = _codec.ReEncodeToPng(await File.ReadAllBytesAsync(shot.LocalPath, ct));
                blob = _encryption.EncryptBytes(png);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                // Corrupt / unreadable screenshot — skip it; never let one bad file fail the whole sync.
                continue;
            }

            // Client-side quota pre-check: keep the oldest, stop once the next one will not fit.
            if (quota > 0 && used + blob.Length > quota)
            {
                notSynced += candidates.Count - i;
                break;
            }

            var key = ScreenshotKey(shot.Filename);
            var put = await _api.GetObjectPutUrlAsync(session, key, ct);
            await PutBytesAsync(put.Url, blob, ct);
            used += blob.Length;
            uploadedThisRun.Add(new UploadedObject(key, blob.Length));
            uploaded++;
            changed = true;
        }

        // 3. Commit: re-PUT the vault meta so the server re-sums all objects and banks storage_used.
        //    On a 413 (a concurrent device pushed us over quota) trim the newest uploads and retry.
        if (changed)
        {
            var trimmed = await CommitMetaWithTrimAsync(session, uploadedThisRun, ct);
            uploaded -= trimmed;
            notSynced += trimmed;
        }

        return new ScreenshotSyncResult(true, uploaded, orphans, notSynced);
    }

    private List<Shot> EnumerateDesiredScreenshots()
    {
        var now = _utcNow();
        return _db.GetAll()
            .Where(e => e.IsScreenshot && (e.ExpiresAt == null || e.ExpiresAt > now))
            .Select(e => new Shot(Path.GetFileName(e.Content), e.Content, e.CapturedAt))
            .Where(s => !string.IsNullOrEmpty(s.Filename) && File.Exists(s.LocalPath))
            .GroupBy(s => s.Filename, StringComparer.Ordinal)        // one object per filename
            .Select(g => g.OrderByDescending(s => s.CapturedAt).First())
            .OrderBy(s => s.CapturedAt)                              // oldest-first (upload + trim policy)
            .ToList();
    }

    /// <summary>
    /// Re-PUT the current remote meta to trigger the server's quota prefix-sum + storage_used bank.
    /// Returns the number of screenshots trimmed (deleted) to get under quota on a 413.
    /// </summary>
    private async Task<int> CommitMetaWithTrimAsync(string session, List<UploadedObject> uploadedThisRun, CancellationToken ct)
    {
        var metaResult = await _api.GetVaultMetaAsync(session, ct);
        if (!metaResult.Exists || metaResult.Meta is null)
            return 0; // no vault.db committed yet — nothing to commit against

        var trimmed = 0;
        while (true)
        {
            try
            {
                await _api.PutVaultMetaAsync(session, metaResult.Meta, ct);
                return trimmed;
            }
            catch (OnlineApiException ex) when (ex.IsPayloadTooLarge && uploadedThisRun.Count > 0)
            {
                // Drop the newest screenshot we just uploaded (keep the oldest), then retry the commit.
                var newest = uploadedThisRun[^1];
                uploadedThisRun.RemoveAt(uploadedThisRun.Count - 1);
                await _api.DeleteObjectAsync(session, newest.Key, ct);
                trimmed++;
            }
        }
    }

    private async Task PutBytesAsync(string url, byte[] bytes, CancellationToken ct)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var resp = await _r2Http.PutAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Online Vault screenshot upload failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
    }

    /// <summary>The R2 (relative) key for a screenshot filename: <c>screenshots/&lt;filename&gt;.enc</c>.</summary>
    private static string ScreenshotKey(string filename) => ScreenshotKeyPrefix + filename + EncryptedSuffix;

    private readonly record struct Shot(string Filename, string LocalPath, DateTime CapturedAt);

    private readonly record struct UploadedObject(string Key, long Size);
}
