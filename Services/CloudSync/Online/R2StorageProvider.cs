using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// <see cref="ICloudStorageProvider"/> backed by the paid Online Vault (Cloudflare R2 via the
/// backend Worker). Slots beside <c>GoogleDriveProvider</c> so <c>CloudSyncManager</c>'s existing
/// last-writer-wins flow (and the retained <c>.pre_sync_backup</c> safety) works unchanged. There
/// is exactly one vault object per user, so the <c>remoteFileName</c>/<c>remoteFileId</c> arguments
/// are ignored — <see cref="RemoteVaultId"/> is the logical handle.
///
/// Bytes flow directly between the client and R2 over a short-lived presigned URL obtained from the
/// Worker; the encrypted vault is opaque to the server. Auth + session refresh + entitlement are
/// delegated to <see cref="IOnlineAccountService"/>; the server re-checks the subscription before
/// signing any URL (a non-subscribed call surfaces as an <see cref="OnlineApiException"/>).
/// </summary>
public sealed class R2StorageProvider : ICloudStorageProvider
{
    /// <summary>Logical id for the single per-user vault object (the API addresses it by session, not id).</summary>
    public const string RemoteVaultId = "vault";

    private readonly IOnlineAccountService _account;
    private readonly IKaptureOnlineApiClient _api;
    private readonly HttpClient _r2Http;
    private readonly IEncryptionService _encryption;

    public R2StorageProvider(IOnlineAccountService account, IKaptureOnlineApiClient api, HttpClient r2Http, IEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(r2Http);
        ArgumentNullException.ThrowIfNull(encryption);
        _account = account;
        _api = api;
        _r2Http = r2Http;
        _encryption = encryption;
    }

    public string ProviderName => "Online Vault";
    public bool IsAuthenticated => _account.IsSignedIn;
    public string? LastAuthError => _account.LastError;

    public async Task<bool> AuthenticateAsync(CancellationToken ct = default)
    {
        var ok = await _account.SignInAsync(ct);
        if (ok)
            await _account.RefreshAccountAsync(ct); // populate cached entitlement for the UI gate
        return ok;
    }

    public void SignOut() => _account.SignOut();

    public async Task<string?> UploadFileAsync(string localPath, string remoteFileName, CancellationToken ct = default)
    {
        await _account.ExecuteAuthedAsync(async (session, c) =>
        {
            var put = await _api.GetVaultPutUrlAsync(session, c);
            await UploadBytesAsync(put.Url, localPath, c);

            var (sha, size) = await HashAndSizeAsync(localPath, c);
            // Carry the public KDF params so the web vault / a second device can derive the key
            // from the user's password (Phase 3 slice A). Absent when the vault has no password.
            var kdf = _encryption.GetKdfInfo();
            var meta = new VaultMeta(
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), sha, size,
                Version: kdf is null ? 1 : 2,
                Kdf: kdf?.Kdf, Iterations: kdf?.Iterations ?? 0, Salt: kdf?.SaltBase64);
            await _api.PutVaultMetaAsync(session, meta, c);
            return true;
        }, ct);

        return RemoteVaultId;
    }

    public Task<bool> DownloadFileAsync(string remoteFileId, string localPath, CancellationToken ct = default) =>
        _account.ExecuteAuthedAsync(async (session, c) =>
        {
            var get = await _api.GetVaultGetUrlAsync(session, c);
            await DownloadBytesAsync(get.Url, localPath, c);
            return true;
        }, ct);

    public async Task<DateTime?> GetRemoteModifiedTimeAsync(string remoteFileId, CancellationToken ct = default)
    {
        var meta = await _account.ExecuteAuthedAsync((session, c) => _api.GetVaultMetaAsync(session, c), ct);
        if (!meta.Exists || meta.Meta is null) return null;
        return DateTime.TryParse(meta.Meta.Mtime, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }

    public async Task<string?> FindFileAsync(string remoteFileName, CancellationToken ct = default)
    {
        var meta = await _account.ExecuteAuthedAsync((session, c) => _api.GetVaultMetaAsync(session, c), ct);
        return meta.Exists ? RemoteVaultId : null;
    }

    private async Task UploadBytesAsync(string url, string localPath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(localPath);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var resp = await _r2Http.PutAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Online Vault upload failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
    }

    private async Task DownloadBytesAsync(string url, string localPath, CancellationToken ct)
    {
        using var resp = await _r2Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Online Vault download failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
        await using var fs = File.Create(localPath);
        await resp.Content.CopyToAsync(fs, ct);
    }

    private static async Task<(string Sha256, long Size)> HashAndSizeAsync(string localPath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(localPath);
        var hash = await SHA256.HashDataAsync(fs, ct);
        return (Convert.ToHexStringLower(hash), fs.Length);
    }
}
