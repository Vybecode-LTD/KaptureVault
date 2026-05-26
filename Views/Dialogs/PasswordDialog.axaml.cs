using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kapture.Views.Dialogs;

public partial class PasswordDialog : Window
{
    public enum DialogMode { Unlock, SetNew, Confirm }

    public string? ResultPassword { get; private set; }
    public bool WasConfirmed { get; private set; }

    private readonly DialogMode _mode;

    public PasswordDialog() : this(DialogMode.Unlock) { }

    public PasswordDialog(DialogMode mode)
    {
        InitializeComponent();
        _mode = mode;

        switch (mode)
        {
            case DialogMode.SetNew:
                TitleText.Text = "Set Encryption Password";
                SubtitleText.Text = "Choose a strong password. All entry content will be encrypted with AES-256. If you forget this password, your data cannot be recovered.";
                ConfirmBox.IsVisible = true;
                SubmitButton.Content = "Enable Encryption";
                break;

            case DialogMode.Unlock:
                TitleText.Text = "Unlock Vault";
                SubtitleText.Text = "Enter your encryption password to unlock.";
                ConfirmBox.IsVisible = false;
                SubmitButton.Content = "Unlock";
                break;

            case DialogMode.Confirm:
                TitleText.Text = "Confirm Password";
                SubtitleText.Text = "Enter your current encryption password to disable encryption.";
                ConfirmBox.IsVisible = false;
                SubmitButton.Content = "Disable Encryption";
                break;
        }
    }

    private void Submit_Click(object? sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Password cannot be empty.");
            return;
        }

        if (_mode == DialogMode.SetNew)
        {
            if (password.Length < 4)
            {
                ShowError("Password must be at least 4 characters.");
                return;
            }

            var confirm = ConfirmBox.Text?.Trim() ?? string.Empty;
            if (password != confirm)
            {
                ShowError("Passwords do not match.");
                return;
            }
        }

        ResultPassword = password;
        WasConfirmed = true;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        WasConfirmed = false;
        Close(false);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
