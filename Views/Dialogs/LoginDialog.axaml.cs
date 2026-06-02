using Avalonia.Controls;
using Avalonia.Interactivity;
using Kapture.ViewModels;

namespace Kapture.Views.Dialogs;

/// <summary>
/// The main-window "Login" dialog (Phase 5): email/password sign-in, register, password-reset
/// request, or Continue-with-Google. Closes itself when the bound <see cref="LoginDialogViewModel"/>
/// reports a successful sign-in.
/// </summary>
public partial class LoginDialog : Window
{
    public LoginDialog()
    {
        InitializeComponent();
    }

    public LoginDialog(LoginDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.SignedIn += OnSignedIn;
        Closed += (_, _) => vm.SignedIn -= OnSignedIn;
    }

    private void OnSignedIn() => Close();

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
