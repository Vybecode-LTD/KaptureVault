namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Client-side orchestration for paid file hosting (Phase 6): upload a local file (presigned PUT →
/// commit), list / delete hosted files, and create / revoke public share links. Wraps
/// <see cref="IKaptureOnlineApiClient"/> behind <see cref="IOnlineAccountService.ExecuteAuthedAsync"/>
/// (transparent session refresh). When the account isn't subscribed the backend returns 402, which
/// surfaces as an <see cref="OnlineApiException"/> with <c>IsPaymentRequired</c> for the UI to handle.
/// </summary>
public interface IFileHostingService
{
    /// <summary>Per-file ceiling (250 MB), enforced client-side before the upload starts.</summary>
    long MaxFileBytes { get; }

    /// <summary>Upload a local file and return its hosted record. Throws if it exceeds <see cref="MaxFileBytes"/>.</summary>
    Task<HostedFile> UploadAsync(string localPath, CancellationToken ct = default);

    /// <summary>List the account's hosted files (newest first).</summary>
    Task<IReadOnlyList<HostedFile>> ListAsync(CancellationToken ct = default);

    /// <summary>Delete a hosted file (and, on the server, its share links).</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Create a public share link for a file; returns the URL to copy.</summary>
    Task<string> CreateShareLinkAsync(string id, CancellationToken ct = default);

    /// <summary>Revoke a share link by its token.</summary>
    Task RevokeShareAsync(string token, CancellationToken ct = default);
}
