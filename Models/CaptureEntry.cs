using CommunityToolkit.Mvvm.ComponentModel;

namespace Kapture.Models;

public partial class CaptureEntry : ObservableObject
{
    public long Id { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int CharCount { get; set; }
    public DateTime CapturedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string EntryType { get; set; } = "keyboard";
    public string? DetectedLanguage { get; set; }

    // KV-013 / T-09: these change in place after capture (pin toggle, tag edit). Making them
    // observable lets the entry list diff-update (reuse instances by Id, preserving the bound
    // selection) without losing the live repaint a full Clear()+rebuild used to provide.
    [ObservableProperty] private bool _isPinned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagList))]
    private string _tags = string.Empty;

    public bool IsClipboard => EntryType == "clipboard";
    public bool IsScreenshot => EntryType == "screenshot";

    /// <summary>
    /// Directory where screenshot images live on THIS device. Screenshots restored from another device
    /// (Phase 3 slice G) are written here keyed by filename, so a row whose <see cref="Content"/> path
    /// was captured elsewhere still resolves. Defaults to the standard location (matches
    /// <c>ScreenshotService</c> and <c>ScreenshotSyncService</c>); overridable for tests.
    /// </summary>
    public static string ScreenshotDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaptureVault", "screenshots");

    /// <summary>
    /// For a screenshot entry, a locally-readable path to the image, or null. Prefers the stored path
    /// (this device's own capture); falls back to <see cref="ScreenshotDirectory"/> keyed by filename
    /// so a screenshot restored from another device — whose stored path is that device's — resolves
    /// (resolve-by-filename; the DB stores a device-local absolute path, only the filename is portable).
    /// </summary>
    public string? ScreenshotPath
    {
        get
        {
            if (!IsScreenshot) return null;
            if (File.Exists(Content)) return Content;
            var byName = Path.Combine(ScreenshotDirectory, Path.GetFileName(Content));
            return File.Exists(byName) ? byName : null;
        }
    }

    public List<string> TagList =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
