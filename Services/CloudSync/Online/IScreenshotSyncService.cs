namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// The Online Vault screenshot pipeline (F-02 Phase 3). The capture <c>vault.db</c> itself is synced by
/// <see cref="R2StorageProvider"/>; this service syncs the screenshot images that the DB references,
/// which live as plaintext <c>.bmp</c> files on disk and so must be re-encoded + encrypted before they
/// ever reach the server. Screenshot identity is the <em>filename</em> (the DB stores a device-local
/// absolute path, which is not portable), so the R2 key is derived from the filename.
/// </summary>
public interface IScreenshotSyncService
{
    /// <summary>
    /// Push local screenshots up to the Online Vault. Enumerates the non-expired screenshots referenced
    /// by the (winning) DB whose file still exists, re-encodes each new one to PNG, encrypts it, and
    /// uploads it (oldest-first, stopping at the storage quota); deletes R2 screenshots the DB no longer
    /// references (orphan cleanup); then re-commits the vault meta so the server banks usage, trimming
    /// the newest uploads and retrying if the server reports the vault is over quota (413).
    ///
    /// A no-op (returns <see cref="ScreenshotSyncResult.NotRun"/>) unless the account is signed in and a
    /// vault password is active — there is no plaintext-upload path.
    /// </summary>
    Task<ScreenshotSyncResult> SyncUpAsync(CancellationToken ct = default);
}
