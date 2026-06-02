using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Default <see cref="IFileHostingService"/>. Uploads go straight to R2 over a presigned PUT (the
/// Worker never sees the bytes); the commit then has the server HEAD the real object and bank usage.
/// Files are stored as-is (not encrypted) so share links can serve them — this is the paid hosting
/// tier, distinct from the end-to-end-encrypted Online Vault.
/// </summary>
public sealed class FileHostingService : IFileHostingService
{
    /// <summary>Mirrors the Worker's MAX_FILE_BYTES — reject early, before requesting a put-url.</summary>
    public const long MaxFileBytesConst = 250L * 1024 * 1024; // 250 MB

    private readonly IOnlineAccountService _account;
    private readonly IKaptureOnlineApiClient _api;
    private readonly HttpClient _r2Http;

    public FileHostingService(IOnlineAccountService account, IKaptureOnlineApiClient api, HttpClient r2Http)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(r2Http);
        _account = account;
        _api = api;
        _r2Http = r2Http;
    }

    public long MaxFileBytes => MaxFileBytesConst;

    public async Task<HostedFile> UploadAsync(string localPath, CancellationToken ct = default)
    {
        var info = new FileInfo(localPath);
        if (!info.Exists) throw new FileNotFoundException("File not found.", localPath);
        if (info.Length > MaxFileBytesConst)
            throw new InvalidOperationException(
                $"\"{info.Name}\" is larger than the {MaxFileBytesConst / (1024 * 1024)} MB per-file limit.");

        // 1) Register the file + get a presigned PUT (server checks the claimed size vs cap + quota).
        var ticket = await _account.ExecuteAuthedAsync(
            (session, c) => _api.CreateFilePutUrlAsync(session, info.Name, info.Length, contentType: null, c), ct);

        // 2) PUT the bytes straight to R2 (the presigned URL carries its own auth).
        await PutBytesAsync(ticket.Url, localPath, ct);

        // 3) Commit — the server HEADs the real object, enforces the cap/quota, banks usage.
        var sha = await Sha256HexAsync(localPath, ct);
        var result = await _account.ExecuteAuthedAsync(
            (session, c) => _api.CommitFileAsync(session, ticket.Id, sha, c), ct);

        return new HostedFile(ticket.Id, info.Name, result.Size, null, DateTime.UtcNow.ToString("o"));
    }

    public Task<IReadOnlyList<HostedFile>> ListAsync(CancellationToken ct = default) =>
        _account.ExecuteAuthedAsync(async (session, c) => (await _api.ListFilesAsync(session, c)).Files, ct);

    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        _account.ExecuteAuthedAsync(async (session, c) =>
        {
            await _api.DeleteFileAsync(session, id, c);
            return true;
        }, ct);

    public Task<string> CreateShareLinkAsync(string id, CancellationToken ct = default) =>
        _account.ExecuteAuthedAsync(async (session, c) =>
            (await _api.CreateShareAsync(session, id, expiresAt: null, c)).Url, ct);

    public Task RevokeShareAsync(string token, CancellationToken ct = default) =>
        _account.ExecuteAuthedAsync(async (session, c) =>
        {
            await _api.RevokeShareAsync(session, token, c);
            return true;
        }, ct);

    private async Task PutBytesAsync(string url, string localPath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(localPath);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var resp = await _r2Http.PutAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Upload failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
    }

    private static async Task<string> Sha256HexAsync(string localPath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(localPath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
