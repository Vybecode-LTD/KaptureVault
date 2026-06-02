using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kapture.Services.CloudSync.Online;

namespace Kapture.ViewModels;

/// <summary>
/// View model for the Files manager window (F-02 Phase 6 — paid file hosting). Holds the account's
/// hosted files, groups them into virtual folders, and drives upload (private/encrypted or shareable),
/// download, share-link, and delete via <see cref="IFileHostingService"/>. The window's code-behind
/// supplies the file picker + clipboard (UI concerns); this view model is pure, testable logic.
/// </summary>
public partial class FileHostingViewModel : ObservableObject
{
    private readonly IFileHostingService _files;
    private readonly List<HostedFile> _all = [];
    // Folders the user created this session that have no file yet (virtual folders only persist once a
    // file lands in them) — keep them visible until reload so "New Folder" feels real.
    private readonly HashSet<string> _sessionFolders = new(StringComparer.Ordinal);

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _currentFolder = "";

    /// <summary>Sub-folders directly under the current folder (click to open).</summary>
    public ObservableCollection<string> Subfolders { get; } = [];

    /// <summary>Files directly in the current folder (newest first).</summary>
    public ObservableCollection<HostedFile> Files { get; } = [];

    public bool AtRoot => CurrentFolder.Length == 0;
    public bool HasFiles => Files.Count > 0;
    public bool HasSubfolders => Subfolders.Count > 0;
    public string CurrentFolderDisplay => CurrentFolder.Length == 0 ? "Home" : "Home / " + CurrentFolder.Replace("/", " / ");
    public long MaxFileBytes => _files.MaxFileBytes;

    /// <summary>"🔒" for an encrypted (private) file, "🔗" for a shareable one.</summary>
    public static readonly IValueConverter LockIconConverter =
        new FuncValueConverter<bool, string>(enc => enc ? "🔒" : "🔗");

    /// <summary>Friendly byte size, e.g. "1.2 MB".</summary>
    public static readonly IValueConverter SizeConverter =
        new FuncValueConverter<long, string>(FormatSize);

    public FileHostingViewModel(IFileHostingService files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
        Recompute();
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Loading…";
        try
        {
            var list = await _files.ListAsync(ct);
            _all.Clear();
            _all.AddRange(list);
            Recompute();
            StatusMessage = _all.Count == 0 ? "No files yet — upload one to get started." : $"{_all.Count} file(s).";
        }
        catch (Exception ex) { StatusMessage = Describe(ex); }
        finally { IsBusy = false; }
    }

    public async Task UploadAsync(string localPath, bool encrypt, CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = $"Uploading {Path.GetFileName(localPath)}…";
        try
        {
            var file = await _files.UploadAsync(localPath, encrypt, CurrentFolder.Length == 0 ? null : CurrentFolder, ct);
            _all.Add(file);
            Recompute();
            StatusMessage = $"Uploaded {file.Name}{(encrypt ? " (private)" : "")}.";
        }
        catch (Exception ex) { StatusMessage = $"Upload failed: {Describe(ex)}"; }
        finally { IsBusy = false; }
    }

    public async Task DeleteAsync(HostedFile file, CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = $"Deleting {file.Name}…";
        try
        {
            await _files.DeleteAsync(file.Id, ct);
            _all.RemoveAll(f => f.Id == file.Id);
            Recompute();
            StatusMessage = $"Deleted {file.Name}.";
        }
        catch (Exception ex) { StatusMessage = $"Delete failed: {Describe(ex)}"; }
        finally { IsBusy = false; }
    }

    /// <summary>Create a public link for a (non-encrypted) file; returns the URL (the window copies it). Null if not allowed/failed.</summary>
    public async Task<string?> ShareAsync(HostedFile file, CancellationToken ct = default)
    {
        if (file.Encrypted)
        {
            StatusMessage = "Private files can't be shared — only you can open them.";
            return null;
        }
        IsBusy = true;
        StatusMessage = $"Creating a link for {file.Name}…";
        try
        {
            var url = await _files.CreateShareLinkAsync(file.Id, ct);
            StatusMessage = "Public link copied to the clipboard.";
            return url;
        }
        catch (Exception ex) { StatusMessage = $"Couldn't create a link: {Describe(ex)}"; return null; }
        finally { IsBusy = false; }
    }

    public async Task<bool> DownloadAsync(HostedFile file, string savePath, CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = $"Downloading {file.Name}…";
        try
        {
            await _files.DownloadAsync(file, savePath, ct);
            StatusMessage = $"Saved {file.Name}.";
            return true;
        }
        catch (Exception ex) { StatusMessage = $"Download failed: {Describe(ex)}"; return false; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void GoHome() => CurrentFolder = "";
    [RelayCommand] private void GoUp() { if (!AtRoot) CurrentFolder = Parent(CurrentFolder); }

    /// <summary>Descend into a sub-folder of the current folder.</summary>
    public void OpenFolder(string folder)
    {
        if (!string.IsNullOrEmpty(folder))
            CurrentFolder = CurrentFolder.Length == 0 ? folder : CurrentFolder + "/" + folder;
    }

    /// <summary>Create (and navigate into) a new sub-folder. It persists once a file is uploaded there.</summary>
    public void CreateFolder(string name)
    {
        var clean = SanitizeSegment(name);
        if (clean.Length == 0) { StatusMessage = "Enter a folder name."; return; }
        var path = CurrentFolder.Length == 0 ? clean : CurrentFolder + "/" + clean;
        _sessionFolders.Add(path);
        CurrentFolder = path;
    }

    partial void OnCurrentFolderChanged(string value) => Recompute();

    private void Recompute()
    {
        Files.Clear();
        foreach (var f in _all.Where(f => (f.Folder ?? "") == CurrentFolder)
                               .OrderByDescending(f => f.CreatedAt, StringComparer.Ordinal))
            Files.Add(f);

        var prefix = CurrentFolder.Length == 0 ? "" : CurrentFolder + "/";
        var subs = _all.Select(f => f.Folder ?? "")
            .Concat(_sessionFolders)
            .Where(folder => folder.Length > prefix.Length && folder.StartsWith(prefix, StringComparison.Ordinal))
            .Select(folder => folder[prefix.Length..].Split('/', 2)[0])
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        Subfolders.Clear();
        foreach (var s in subs) Subfolders.Add(s);

        OnPropertyChanged(nameof(AtRoot));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasSubfolders));
        OnPropertyChanged(nameof(CurrentFolderDisplay));
    }

    private static string Parent(string folder)
    {
        var i = folder.LastIndexOf('/');
        return i < 0 ? "" : folder[..i];
    }

    private static string SanitizeSegment(string? name)
    {
        var kept = new string((name ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '_' or '-' or '.').ToArray()).Trim();
        return kept.Length > 60 ? kept[..60] : kept;
    }

    private static string FormatSize(long bytes)
    {
        const double KB = 1024, MB = KB * 1024, GB = MB * 1024;
        if (bytes >= GB) return (bytes / GB).ToString("0.#") + " GB";
        if (bytes >= MB) return (bytes / MB).ToString("0.#") + " MB";
        if (bytes >= KB) return (bytes / KB).ToString("0.#") + " KB";
        return bytes + " B";
    }

    private static string Describe(Exception ex) =>
        ex is OnlineApiException { IsPaymentRequired: true }
            ? "An active subscription is required for file hosting."
            : ex.Message;
}
