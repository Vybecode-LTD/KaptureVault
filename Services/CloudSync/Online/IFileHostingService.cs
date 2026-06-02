namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Client-side orchestration for paid file hosting (Phase 6): upload a local file (optionally
/// client-encrypted), download it back (decrypting if needed), list / delete hosted files, and
/// create / revoke public share links. Wraps <see cref="IKaptureOnlineApiClient"/> behind
/// <see cref="IOnlineAccountService.ExecuteAuthedAsync"/> (transparent session refresh). A 402
/// surfaces as an <see cref="OnlineApiException"/> with <c>IsPaymentRequired</c> when the account
/// isn't subscribed.
/// <para>
/// Per-file privacy (Phase 6D): a file is EITHER private (client-encrypted with the vault key — only
/// the owner can open it, no public link) OR shareable (stored as-is, public link). The two are
/// mutually exclusive; the backend also refuses a share for an encrypted file.
/// </para>
/// </summary>
public interface IFileHostingService
{
    /// <summary>Per-file ceiling (250 MB), enforced client-side before the upload starts.</summary>
    long MaxFileBytes { get; }

    /// <summary>
    /// Upload a local file into <paramref name="folder"/> (null = root). When <paramref name="encrypt"/>
    /// is true the bytes are encrypted with the vault key first (requires an active vault password).
    /// </summary>
    Task<HostedFile> UploadAsync(string localPath, bool encrypt, string? folder, CancellationToken ct = default);

    /// <summary>Download a hosted file to <paramref name="localPath"/>, decrypting it if it was stored encrypted.</summary>
    Task DownloadAsync(HostedFile file, string localPath, CancellationToken ct = default);

    /// <summary>List the account's hosted files (newest first).</summary>
    Task<IReadOnlyList<HostedFile>> ListAsync(CancellationToken ct = default);

    /// <summary>Delete a hosted file (and, on the server, its share links).</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Create a public share link for a (non-encrypted) file; returns the URL to copy.</summary>
    Task<string> CreateShareLinkAsync(string id, CancellationToken ct = default);

    /// <summary>Revoke a share link by its token.</summary>
    Task RevokeShareAsync(string token, CancellationToken ct = default);
}
