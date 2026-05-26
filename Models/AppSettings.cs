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

    // Cloud Sync
    [JsonPropertyName("cloudSyncProvider")]
    public string? CloudSyncProvider { get; set; }

    [JsonPropertyName("cloudSyncIntervalMinutes")]
    public int CloudSyncIntervalMinutes { get; set; } = 15;

    [JsonPropertyName("cloudSyncEnabled")]
    public bool CloudSyncEnabled { get; set; } = false;

    [JsonPropertyName("syncOnClose")]
    public bool SyncOnClose { get; set; } = true;

    // General
    [JsonPropertyName("maxBufferChars")]
    public int MaxBufferChars { get; set; } = 5000;

    [JsonPropertyName("idleFlushSeconds")]
    public int IdleFlushSeconds { get; set; } = 20;
}
