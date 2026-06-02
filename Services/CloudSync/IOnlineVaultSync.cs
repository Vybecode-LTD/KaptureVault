namespace Kapture.Services.CloudSync;

/// <summary>
/// The minimal seam the Online Vault UI uses to trigger an on-demand sync (the main-window "Sync"
/// button) and reflect progress, without depending on all of <see cref="CloudSyncManager"/>. The
/// manager implements it; the timers + Google Drive backup stay internal. (Replaces the retired
/// provider-switching seam from before the P5 decouple — this one only triggers, it doesn't select.)
/// </summary>
public interface IOnlineVaultSync
{
    /// <summary>Sync the Online Vault now (no-op + false unless signed in). Vault + screenshots to R2.</summary>
    Task<bool> SyncOnlineVaultNowAsync(CancellationToken ct = default);

    /// <summary>True while any sync (Drive or Online Vault) is in progress.</summary>
    bool IsSyncing { get; }
}
