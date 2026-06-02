using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kapture.Services.CloudSync.Online;
using Kapture.ViewModels;
using Kapture.Views.Dialogs;

namespace Kapture.Views;

/// <summary>
/// The pop-open Files manager (F-02 Phase 6 — paid file hosting). The view model is pure logic; this
/// code-behind supplies the UI-only pieces: the file picker (upload/download), the clipboard (share
/// link), and the folder-name prompt. Opened from the main window's Upload button for paid accounts.
/// </summary>
public partial class FilesWindow : Window
{
    public FilesWindow()
    {
        InitializeComponent();
    }

    public FilesWindow(FileHostingViewModel vm) : this()
    {
        DataContext = vm;
    }

    private FileHostingViewModel? Vm => DataContext as FileHostingViewModel;

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Vm is not null) await Vm.RefreshAsync();
    }

    private void Home_Click(object? sender, RoutedEventArgs e) => Vm?.GoHomeCommand.Execute(null);
    private void Up_Click(object? sender, RoutedEventArgs e) => Vm?.GoUpCommand.Execute(null);
    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null) await Vm.RefreshAsync();
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string folder }) Vm?.OpenFolder(folder);
    }

    private async void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        var name = await TextPromptDialog.ShowAsync(this, "New Folder", "Folder name:");
        if (!string.IsNullOrWhiteSpace(name)) Vm?.CreateFolder(name);
    }

    private async void UploadPrivate_Click(object? sender, RoutedEventArgs e) => await PickAndUploadAsync(encrypt: true);
    private async void UploadShareable_Click(object? sender, RoutedEventArgs e) => await PickAndUploadAsync(encrypt: false);

    private async Task PickAndUploadAsync(bool encrypt)
    {
        if (Vm is null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = encrypt ? "Choose file(s) to upload privately" : "Choose file(s) to upload",
            AllowMultiple = true,
        });
        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) await Vm.UploadAsync(path, encrypt);
        }
    }

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { DataContext: HostedFile file }) return;
        var dest = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save file",
            SuggestedFileName = file.Name,
        });
        var path = dest?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path)) await Vm.DownloadAsync(file, path);
    }

    private async void CopyLink_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { DataContext: HostedFile file }) return;
        var url = await Vm.ShareAsync(file);
        if (!string.IsNullOrEmpty(url) && Clipboard is not null) await Clipboard.SetTextAsync(url);
    }

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { DataContext: HostedFile file }) return;
        await Vm.DeleteAsync(file);
    }
}
