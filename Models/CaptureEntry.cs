namespace Kapture.Models;

public class CaptureEntry
{
    public long Id { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int CharCount { get; set; }
    public DateTime CapturedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsPinned { get; set; }
    public string EntryType { get; set; } = "keyboard";
    public string? DetectedLanguage { get; set; }
    public string Tags { get; set; } = string.Empty;

    public bool IsClipboard => EntryType == "clipboard";
    public bool IsScreenshot => EntryType == "screenshot";

    /// <summary>For screenshot entries, Content holds the file path to the image.</summary>
    public string? ScreenshotPath => IsScreenshot && File.Exists(Content) ? Content : null;

    public List<string> TagList =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
