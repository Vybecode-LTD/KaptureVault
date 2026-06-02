using System.Text.Json.Serialization;

namespace Kapture.Models;

public class AppSettings
{
    // Theme
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Dark";

    // Auto-Cleanup
    [JsonPropertyName("autoCleanupEnabled")]
    public bool AutoCleanupEnabled { get; set; } = true;

    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; } = 30;

    [JsonPropertyName("excludePinnedFromCleanup")]
    public bool ExcludePinnedFromCleanup { get; set; } = true;

    // Quick Paste Hotkey
    [JsonPropertyName("quickPasteHotkey")]
    public string QuickPasteHotkey { get; set; } = "Ctrl+Shift+V";

    [JsonPropertyName("quickPasteEnabled")]
    public bool QuickPasteEnabled { get; set; } = true;

    // Cloud Sync.
    // P5 decouple: Google Drive backup and the Online Vault are INDEPENDENT.
    //  • DriveBackupEnabled governs the optional whole-DB Google Drive backup.
    //  • The Online Vault has no flag here — it syncs whenever the user is signed in (gated on the
    //    account + an active vault password), so it's seamless once connected.
    //  • CloudSyncIntervalMinutes + SyncOnClose are shared by both.
    [JsonPropertyName("driveBackupEnabled")]
    public bool DriveBackupEnabled { get; set; } = false;

    [JsonPropertyName("cloudSyncIntervalMinutes")]
    public int CloudSyncIntervalMinutes { get; set; } = 15;

    [JsonPropertyName("syncOnClose")]
    public bool SyncOnClose { get; set; } = true;

    // ── Legacy (pre-P5) — read only, for one-time migration in SettingsService.Load. ──
    // Old single "active provider" model: CloudSyncEnabled was the master switch and CloudSyncProvider
    // named the one selected provider ("Google Drive" | "Online Vault"). Superseded by DriveBackupEnabled
    // (Drive) + sign-in (Online Vault). Kept so existing settings.json deserializes and migrates.
    [JsonPropertyName("cloudSyncProvider")]
    public string? CloudSyncProvider { get; set; }

    [JsonPropertyName("cloudSyncEnabled")]
    public bool CloudSyncEnabled { get; set; } = false;

    // General
    [JsonPropertyName("maxBufferChars")]
    public int MaxBufferChars { get; set; } = 5000;

    [JsonPropertyName("idleFlushSeconds")]
    public int IdleFlushSeconds { get; set; } = 20;

    // Advanced
    /// <summary>
    /// When true, KaptureVault restarts with administrator privileges so that
    /// its low-level keyboard hook can receive input from elevated processes
    /// (Task Manager, Registry Editor, etc.). Requires a UAC prompt on startup.
    /// </summary>
    [JsonPropertyName("captureAdminApps")]
    public bool CaptureAdminApps { get; set; } = false;
}
