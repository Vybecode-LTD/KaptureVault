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
