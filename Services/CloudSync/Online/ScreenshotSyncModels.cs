namespace Kapture.Services.CloudSync.Online;

/// <summary>
/// Outcome of one <see cref="IScreenshotSyncService.SyncUpAsync"/> pass, for the Settings panel status
/// line (slice H) and for tests. <see cref="Ran"/> is false when the sync was skipped entirely
/// (not signed in, or no vault password — the Online Vault is end-to-end encrypted).
/// </summary>
public sealed record ScreenshotSyncResult(
    bool Ran,
    int Uploaded,
    int OrphansDeleted,
    int NotSyncedOverQuota)
{
    /// <summary>The sync did not run (not signed in or the vault has no password).</summary>
    public static ScreenshotSyncResult NotRun { get; } = new(false, 0, 0, 0);
}

/// <summary>
/// Outcome of one <see cref="IScreenshotSyncService.RestoreAsync"/> pass (download-wins sync / fresh
/// device). <see cref="MissingRemote"/> counts referenced screenshots that aren't on the server
/// (older captures from before screenshot sync) — expected, not an error.
/// </summary>
public sealed record ScreenshotRestoreResult(
    bool Ran,
    int Restored,
    int MissingRemote)
{
    /// <summary>The restore did not run (not signed in or the vault has no password).</summary>
    public static ScreenshotRestoreResult NotRun { get; } = new(false, 0, 0);
}
