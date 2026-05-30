using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kapture.Models;
using Kapture.Services;
using SkiaSharp;

namespace Kapture.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDatabaseService _db;
    private readonly ICaptureService _capture;
    private readonly IClipboardMonitorService _clipboardMonitor;
    private readonly IScreenshotService? _screenshotService;
    private readonly IStartupService _startup;
    private readonly ISettingsService? _settings;

    // Prevents OnSelected*FilterChanged from calling RefreshEntries() while
    // RefreshAppList / RefreshTagList are rebuilding their collections.
    // Without this, AppList.Clear() causes Avalonia's ListBox to push null back
    // into SelectedAppFilter, firing RefreshEntries() with no filter applied.
    private bool _suppressFilterRefresh;

    // KV-013: cap how many entries the list materializes at once (most-recent first).
    // The ListBox virtualizes, but an unbounded result set still decrypts + allocates
    // for every row on each refresh. Search is unaffected (it scans the full vault).
    private const int MaxDisplayedEntries = 1000;

    // (single-section app — vault is always the active view)

    // Stats
    [ObservableProperty] private int _totalEntries;
    [ObservableProperty] private long _totalChars;
    [ObservableProperty] private int _distinctApps;
    [ObservableProperty] private int _clipboardEntries;
    [ObservableProperty] private int _screenshotEntries;

    // Search & filter
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isSearchOpen;
    [ObservableProperty] private string? _selectedAppFilter;
    [ObservableProperty] private string _selectedTypeFilter = "All";

    // Recording state
    [ObservableProperty] private bool _isRecording = true;
    [ObservableProperty] private string _recordingStatus = "Recording";

    // Startup state
    [ObservableProperty] private bool _isStartupEnabled;
    [ObservableProperty] private string _startupButtonText = "+ Add to Startup";

    // Tag filter
    [ObservableProperty] private string? _selectedTagFilter;
    [ObservableProperty] private string _newTagText = string.Empty;

    // Sub-ViewModels
    public ObservableCollection<string> AppList { get; } = [];
    public ObservableCollection<string> TagList { get; } = [];
    public ObservableCollection<string> EntryTags { get; } = [];
    public ObservableCollection<CaptureEntry> Entries { get; } = [];

    // Selected entry
    [ObservableProperty] private CaptureEntry? _selectedEntry;

    // Toast
    [ObservableProperty] private string? _toastMessage;
    [ObservableProperty] private bool _isToastVisible;

    public MainWindowViewModel()
    {
        // Design-time only
        _db = null!;
        _capture = null!;
        _clipboardMonitor = null!;
        _startup = null!;
    }

    public MainWindowViewModel(IDatabaseService db, ICaptureService capture, IClipboardMonitorService clipboardMonitor, IStartupService startup, ISettingsService? settings = null, IScreenshotService? screenshotService = null)
    {
        _db = db;
        _capture = capture;
        _clipboardMonitor = clipboardMonitor;
        _screenshotService = screenshotService;
        _startup = startup;
        _settings = settings;

        _capture.OnEntryFlushed += () => Dispatcher.UIThread.Post(Refresh);
        _clipboardMonitor.OnEntryFlushed += () => Dispatcher.UIThread.Post(Refresh);
        if (_screenshotService != null)
            _screenshotService.OnEntryFlushed += () => Dispatcher.UIThread.Post(Refresh);

        IsStartupEnabled = _startup.IsRegistered;
        UpdateStartupButtonText();
        Refresh();
    }

    public void Refresh()
    {
        RefreshStats();
        RefreshAppList();
        RefreshTagList();
        RefreshEntries();
    }

    private void RefreshStats()
    {
        var (total, chars, apps, clips, screenshots) = _db.GetStats();
        TotalEntries = total;
        TotalChars = chars;
        DistinctApps = apps;
        ClipboardEntries = clips;
        ScreenshotEntries = screenshots;
    }

    private void RefreshAppList()
    {
        var apps = _db.GetDistinctApps();
        var currentSelection = SelectedAppFilter;

        // Diff-update instead of Clear()+rebuild.
        // Clear() makes Avalonia's ListBox post a DEFERRED SelectedItem=null binding
        // callback that fires AFTER _suppressFilterRefresh returns to false, wiping
        // the user's selection. By never removing the currently-selected item (unless
        // it truly left the DB), the ListBox never loses its selection and nothing
        // needs to be deferred.
        _suppressFilterRefresh = true;
        try
        {
            var desired = new List<string>(apps.Count + 1) { "All Apps" };
            desired.AddRange(apps);

            // Remove items no longer valid (reverse so indices stay stable)
            for (int i = AppList.Count - 1; i >= 0; i--)
                if (!desired.Contains(AppList[i])) AppList.RemoveAt(i);

            // Add missing items (preserves relative order)
            foreach (var item in desired)
                if (!AppList.Contains(item)) AppList.Add(item);

            SelectedAppFilter = currentSelection != null && AppList.Contains(currentSelection)
                ? currentSelection
                : "All Apps";
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
    }

    private void RefreshEntries()
    {
        var appFilter = SelectedAppFilter == "All Apps" ? null : SelectedAppFilter;

        List<CaptureEntry> results;
        if (!string.IsNullOrWhiteSpace(SearchText))
            results = _db.Search(SearchText, appFilter);
        else if (appFilter != null)
            results = _db.GetByApp(appFilter, MaxDisplayedEntries);
        else
            results = _db.GetAll(MaxDisplayedEntries);

        // Apply entry type filter
        if (SelectedTypeFilter == "Keyboard")
            results = results.Where(e => e.EntryType == "keyboard").ToList();
        else if (SelectedTypeFilter == "Clipboard")
            results = results.Where(e => e.EntryType == "clipboard").ToList();
        else if (SelectedTypeFilter == "Screenshot")
            results = results.Where(e => e.EntryType == "screenshot").ToList();

        // Apply tag filter
        if (SelectedTagFilter != null && SelectedTagFilter != "All Tags")
            results = results.Where(e => e.TagList.Contains(SelectedTagFilter, StringComparer.OrdinalIgnoreCase)).ToList();

        Entries.Clear();
        foreach (var entry in results)
            Entries.Add(entry);

        // Re-select if still available
        if (SelectedEntry != null)
        {
            var match = Entries.FirstOrDefault(e => e.Id == SelectedEntry.Id);
            SelectedEntry = match;
        }
    }

    partial void OnSearchTextChanged(string value) => RefreshEntries();
    partial void OnSelectedTypeFilterChanged(string value) => RefreshEntries();
    // App and tag filters are suppressed during list rebuilds to prevent
    // the mid-rebuild null push from Avalonia's ListBox clearing selection.
    partial void OnSelectedAppFilterChanged(string? value) { if (!_suppressFilterRefresh) RefreshEntries(); }
    partial void OnSelectedTagFilterChanged(string? value) { if (!_suppressFilterRefresh) RefreshEntries(); }

    partial void OnSelectedEntryChanged(CaptureEntry? value)
    {
        EntryTags.Clear();
        if (value != null)
        {
            foreach (var tag in value.TagList)
                EntryTags.Add(tag);
        }
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (IsRecording)
        {
            _capture.Pause();
            _clipboardMonitor.Pause();
            _screenshotService?.Pause();
            IsRecording = false;
            RecordingStatus = "Paused";
        }
        else
        {
            _capture.Resume();
            _clipboardMonitor.Resume();
            _screenshotService?.Resume();
            IsRecording = true;
            RecordingStatus = "Recording";
        }
    }

    [RelayCommand]
    private void ClearExpired()
    {
        _db.PruneExpired();
        Refresh();
        ShowToast("Expired entries cleared");
    }

    [RelayCommand]
    private void ToggleStartup()
    {
        if (IsStartupEnabled)
            _startup.Unregister();
        else
            _startup.Register();

        IsStartupEnabled = _startup.IsRegistered;
        UpdateStartupButtonText();
    }

    private void UpdateStartupButtonText()
    {
        StartupButtonText = IsStartupEnabled ? "Runs on Startup" : "+ Add to Startup";
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchOpen = !IsSearchOpen;
    }

    [RelayCommand]
    private void CloseSearch()
    {
        IsSearchOpen = false;
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void SetTypeFilter(string filter)
    {
        SelectedTypeFilter = filter;
    }

    [RelayCommand]
    private void AddTag()
    {
        if (SelectedEntry == null || string.IsNullOrWhiteSpace(NewTagText)) return;
        var tag = NewTagText.Trim().ToLowerInvariant();
        var tags = SelectedEntry.TagList;
        if (tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) return;
        tags.Add(tag);
        var newTags = string.Join(",", tags);
        _db.UpdateTags(SelectedEntry.Id, newTags);
        SelectedEntry.Tags = newTags;
        EntryTags.Clear();
        foreach (var t in tags) EntryTags.Add(t);
        NewTagText = string.Empty;
        RefreshTagList();
        ShowToast($"Tag '{tag}' added");
    }

    [RelayCommand]
    private void RemoveTag(string tag)
    {
        if (SelectedEntry == null) return;
        var tags = SelectedEntry.TagList;
        tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
        var newTags = string.Join(",", tags);
        _db.UpdateTags(SelectedEntry.Id, newTags);
        SelectedEntry.Tags = newTags;
        EntryTags.Clear();
        foreach (var t in tags) EntryTags.Add(t);
        RefreshTagList();
        ShowToast($"Tag '{tag}' removed");
    }

    private void RefreshTagList()
    {
        var tags = _db.GetDistinctTags();
        var currentSelection = SelectedTagFilter;

        _suppressFilterRefresh = true;
        try
        {
            var desired = new List<string>(tags.Count + 1) { "All Tags" };
            desired.AddRange(tags);

            for (int i = TagList.Count - 1; i >= 0; i--)
                if (!desired.Contains(TagList[i])) TagList.RemoveAt(i);

            foreach (var item in desired)
                if (!TagList.Contains(item)) TagList.Add(item);

            SelectedTagFilter = currentSelection != null && TagList.Contains(currentSelection)
                ? currentSelection
                : "All Tags";
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
    }

    [RelayCommand]
    private async Task CopyEntry()
    {
        if (SelectedEntry == null) return;
        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(SelectedEntry.Content);
            ShowToast("Copied to clipboard");
        }
    }

    [RelayCommand]
    private async Task SaveEntryToFile()
    {
        if (SelectedEntry == null) return;
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        if (SelectedEntry.IsScreenshot)
        {
            var sourcePath = SelectedEntry.Content;
            if (!File.Exists(sourcePath))
            {
                ShowToast("Screenshot file not found");
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Screenshot",
                DefaultExtension = "png",
                SuggestedFileName = $"kapture_{SelectedEntry.AppName}_{SelectedEntry.CapturedAt:yyyyMMdd_HHmmss}",
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG Image") { Patterns = ["*.png"] },
                    new FilePickerFileType("JPEG Image") { Patterns = ["*.jpg", "*.jpeg"] },
                    new FilePickerFileType("BMP Image") { Patterns = ["*.bmp"] }
                ]
            });

            if (file == null) return;

            var fileName = file.Name.ToLowerInvariant();
            SKEncodedImageFormat format;
            int quality = 92;
            if (fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg"))
            {
                format = SKEncodedImageFormat.Jpeg;
                quality = 90;
            }
            else if (fileName.EndsWith(".bmp"))
            {
                format = SKEncodedImageFormat.Bmp;
            }
            else
            {
                format = SKEncodedImageFormat.Png;
            }

            using var bitmap = SKBitmap.Decode(sourcePath);
            if (bitmap == null)
            {
                ShowToast("Failed to decode screenshot");
                return;
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, quality);
            await using var stream = await file.OpenWriteAsync();
            data.SaveTo(stream);
            ShowToast("Screenshot saved");
        }
        else
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Entry as Text",
                DefaultExtension = "txt",
                SuggestedFileName = $"kapture_{SelectedEntry.AppName}_{SelectedEntry.CapturedAt:yyyyMMdd_HHmmss}.txt",
                FileTypeChoices =
                [
                    new FilePickerFileType("Text Files") { Patterns = ["*.txt"] }
                ]
            });

            if (file != null)
            {
                var header = $"""
                    Kapture Export
                    App: {SelectedEntry.AppName}
                    Window: {SelectedEntry.WindowTitle}
                    Captured: {SelectedEntry.CapturedAt:yyyy-MM-dd HH:mm:ss}
                    Characters: {SelectedEntry.CharCount}
                    ───────────────────────────────

                    """;
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(header + SelectedEntry.Content);
                ShowToast("Saved to file");
            }
        }
    }

    [RelayCommand]
    private void TogglePin()
    {
        if (SelectedEntry == null) return;
        var newState = !SelectedEntry.IsPinned;
        _db.UpdatePin(SelectedEntry.Id, newState);
        SelectedEntry.IsPinned = newState;
        Refresh();
        ShowToast(newState ? "Entry pinned" : "Entry unpinned");
    }

    public void ConfirmDeleteEntry()
    {
        if (SelectedEntry == null) return;
        _db.Delete(SelectedEntry.Id);
        SelectedEntry = null;
        Refresh();
        ShowToast("Entry deleted");
    }

    public void SetExpiry(TimeSpan? duration)
    {
        if (SelectedEntry == null) return;
        var expiresAt = duration.HasValue ? DateTime.UtcNow + duration.Value : (DateTime?)null;
        _db.UpdateExpiry(SelectedEntry.Id, expiresAt);
        SelectedEntry.ExpiresAt = expiresAt;
        Refresh();
        ShowToast(duration.HasValue ? "Expiry set" : "Expiry removed");
    }

    private void ShowToast(string message)
    {
        ToastMessage = message;
        IsToastVisible = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            IsToastVisible = false;
            timer.Stop();
        };
        timer.Start();
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private static Avalonia.Input.Platform.IClipboard? GetClipboard()
    {
        var topLevel = GetTopLevel();
        return topLevel?.Clipboard;
    }
}
