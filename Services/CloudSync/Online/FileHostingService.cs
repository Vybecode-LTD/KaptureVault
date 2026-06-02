using System.Net.Http.Headers;
using System.Security.Cryptography;
using Kapture.Services;

namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Default <see cref="IFileHostingService"/>. Uploads go straight to R2 over a presigned PUT (the
/// Worker never sees the bytes); the commit then has the server HEAD the real object and bank usage.
/// A private file is encrypted client-side with the vault key (<see cref="IEncryptionService.EncryptBytes"/>)
/// before upload and decrypted on download; a shareable file is stored as-is so its public link works.
/// </summary>
public sealed class FileHostingService : IFileHostingService
{
    /// <summary>Mirrors the Worker's MAX_FILE_BYTES — reject early, before requesting a put-url.</summary>
    public const long MaxFileBytesConst = 250L * 1024 * 1024; // 250 MB

    private readonly IOnlineAccountService _account;
    private readonly IKaptureOnlineApiClient _api;
    private readonly IEncryptionService _encryption;
    private readonly HttpClient _r2Http;

    public FileHostingService(
        IOnlineAccountService account, IKaptureOnlineApiClient api, IEncryptionService encryption, HttpClient r2Http)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(r2Http);
        _account = account;
        _api = api;
        _encryption = encryption;
        _r2Http = r2Http;
    }

    public long MaxFileBytes => MaxFileBytesConst;

    public async Task<HostedFile> UploadAsync(string localPath, bool encrypt, string? folder, CancellationToken ct = default)
    {
        var info = new FileInfo(localPath);
        if (!info.Exists) throw new FileNotFoundException("File not found.", localPath);
        if (info.Length > MaxFileBytesConst)
            throw new InvalidOperationException(
                $"\"{info.Name}\" is larger than the {MaxFileBytesConst / (1024 * 1024)} MB per-file limit.");

        // A private file is encrypted up front (the ciphertext is what we store + quota against).
        byte[]? cipher = null;
        long uploadSize = info.Length;
        if (encrypt)
        {
            if (!_encryption.IsActive)
                throw new InvalidOperationException(
                    "Set a vault password (Settings → Encryption) to upload a private, encrypted file.");
            var plain = await File.ReadAllBytesAsync(localPath, ct);
            cipher = _encryption.EncryptBytes(plain);
            uploadSize = cipher.LongLength;
        }

        // 1) Register the file + get a presigned PUT (server checks the claimed size vs cap + quota).
        var ticket = await _account.ExecuteAuthedAsync(
            (session, c) => _api.CreateFilePutUrlAsync(session, info.Name, uploadSize, contentType: null, encrypt, folder, c), ct);

        // 2) PUT the bytes straight to R2 (the presigned URL carries its own auth).
        if (encrypt)
            await PutAsync(ticket.Url, new ByteArrayContent(cipher!), ct);
        else
            await PutFileAsync(ticket.Url, localPath, ct);

        // 3) Commit — the server HEADs the real object, enforces the cap/quota, banks usage.
        var sha = encrypt ? Sha256Hex(cipher!) : await Sha256HexAsync(localPath, ct);
        var result = await _account.ExecuteAuthedAsync(
            (session, c) => _api.CommitFileAsync(session, ticket.Id, sha, c), ct);

        return new HostedFile(ticket.Id, info.Name, result.Size, null, encrypt, folder, DateTime.UtcNow.ToString("o"));
    }

    public async Task DownloadAsync(HostedFile file, string localPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var presigned = await _account.ExecuteAuthedAsync((session, c) => _api.GetFileGetUrlAsync(session, file.Id, c), ct);
        var bytes = await _r2Http.GetByteArrayAsync(presigned.Url, ct);
        if (file.Encrypted)
        {
            if (!_encryption.IsActive)
                throw new InvalidOperationException("Unlock your vault to open this encrypted file.");
            bytes = _encryption.DecryptBytes(bytes);
        }
        await File.WriteAllBytesAsync(localPath, bytes, ct);
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

    private Task PutFileAsync(string url, string localPath, CancellationToken ct)
    {
        var stream = File.OpenRead(localPath);
        var content = new StreamContent(stream); // disposed by PutAsync
        return PutAsync(url, content, ct);
    }

    private async Task PutAsync(string url, HttpContent content, CancellationToken ct)
    {
        using (content)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var resp = await _r2Http.PutAsync(url, content, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Upload failed: {(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<string> Sha256HexAsync(string localPath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(localPath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
