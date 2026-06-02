using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Kapture.Services;
using Kapture.ViewModels;
using Kapture.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace Kapture.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is MainWindowViewModel mainVm)
                mainVm.PropertyChanged += OnMainVmPropertyChanged;
        };
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel mainVm) return;

        if (e.PropertyName == nameof(MainWindowViewModel.IsSearchOpen) && mainVm.IsSearchOpen)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var searchBox = this.FindControl<TextBox>("SearchOverlayBox");
                searchBox?.Focus();
                searchBox?.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Input);
        }
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var settingsService = App.Services.GetRequiredService<ISettingsService>();
        var settingsVm = new SettingsViewModel(settingsService);
        var settingsWindow = new SettingsWindow { DataContext = settingsVm };
        await settingsWindow.ShowDialog(this);
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        await new AboutDialog().ShowDialog(this);
    }

    // The Upload button. File hosting is the paid differentiator (Phase 6): a paid account opens the
    // Files manager window; a free account gets the upgrade pitch (→ Stripe checkout).
    private async void Upload_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.Account is null) return;
        if (vm.Account.IsPaid)
        {
            var filesVm = App.Services.GetRequiredService<FileHostingViewModel>();
            await new FilesWindow(filesVm).ShowDialog(this);
        }
        else
        {
            await new UploadDialog { DataContext = vm.Account }.ShowDialog(this);
        }
    }

    private async void ViewContent_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedEntry != null)
        {
            var viewer = new ContentViewerWindow(vm.SelectedEntry);
            await viewer.ShowDialog(this);
        }
    }

    private void EditScreenshot_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedEntry == null) return;
        new ScreenshotEditorWindow(vm.SelectedEntry).Show();
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm)
            vm.ClearSearchCommand.Execute(null);
    }

    private void SearchOverlay_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm)
        {
            vm.CloseSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control) && DataContext is MainWindowViewModel vm)
        {
            vm.ToggleSearchCommand.Execute(null);
            if (vm.IsSearchOpen)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var searchBox = this.FindControl<TextBox>("SearchOverlayBox");
                    searchBox?.Focus();
                }, Avalonia.Threading.DispatcherPriority.Input);
            }
            e.Handled = true;
        }
    }

    private void TagInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
            vm.AddTagCommand.Execute(null);
    }

    private async void ExpiryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedEntry == null)
            return;

        var dialog = new ExpiryDialog();
        var result = await dialog.ShowDialog<TimeSpan?>(this);

        if (dialog.WasConfirmed)
            vm.SetExpiry(result);
    }

    private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedEntry == null)
            return;

        var dialog = new DeleteConfirmDialog();
        var result = await dialog.ShowDialog<bool>(this);
        if (result)
            vm.ConfirmDeleteEntry();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
