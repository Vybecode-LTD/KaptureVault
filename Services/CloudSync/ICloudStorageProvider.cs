namespace Kapture.Services.CloudSync;

public interface ICloudStorageProvider
{
    string ProviderName { get; }
    bool IsAuthenticated { get; }

    /// <summary>Start OAuth2 flow — opens browser, listens for callback, stores tokens.</summary>
    Task<bool> AuthenticateAsync(CancellationToken ct = default);

    /// <summary>Sign out and clear stored tokens.</summary>
    void SignOut();

    /// <summary>Upload a local file to cloud storage. Returns the remote file ID.</summary>
    Task<string?> UploadFileAsync(string localPath, string remoteFileName, CancellationToken ct = default);

    /// <summary>Download a file from cloud storage to a local path.</summary>
    Task<bool> DownloadFileAsync(string remoteFileId, string localPath, CancellationToken ct = default);

    /// <summary>Get metadata (modified time) for a remote file. Returns null if not found.</summary>
    Task<DateTime?> GetRemoteModifiedTimeAsync(string remoteFileId, CancellationToken ct = default);

    /// <summary>Find a file by name in the app's sync folder. Returns file ID or null.</summary>
    Task<string?> FindFileAsync(string remoteFileName, CancellationToken ct = default);
}
