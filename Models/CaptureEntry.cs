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

    /// <summary>For screenshot entries, Content holds the file path to the image.</summary>
    public string? ScreenshotPath => IsScreenshot && File.Exists(Content) ? Content : null;

    public List<string> TagList =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
