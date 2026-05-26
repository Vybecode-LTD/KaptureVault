using Avalonia.Controls;
using Avalonia.Interactivity;
using Kapture.ViewModels;

namespace Kapture.Views.Dialogs;

public partial class ExpiryDialog : Window
{
    public bool WasConfirmed { get; private set; }

    public ExpiryDialog()
    {
        InitializeComponent();
        DataContext = new ExpiryDialogViewModel();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        WasConfirmed = false;
        Close(null as TimeSpan?);
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExpiryDialogViewModel vm && vm.SelectedOption != null)
        {
            WasConfirmed = true;
            Close(vm.SelectedOption.Duration);
        }
    }
}
