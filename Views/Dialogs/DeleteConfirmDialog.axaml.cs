using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kapture.Views.Dialogs;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
