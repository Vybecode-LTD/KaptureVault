using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kapture.Views.Dialogs;

/// <summary>A minimal single-line text prompt (OK / Cancel). Returns the entered text, or null on cancel.</summary>
public partial class TextPromptDialog : Window
{
    public string? Result { get; private set; }

    public TextPromptDialog()
    {
        InitializeComponent();
    }

    public TextPromptDialog(string title, string prompt) : this()
    {
        Title = title;
        PromptText.Text = prompt;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Input.Focus();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Result = Input.Text;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    public static async Task<string?> ShowAsync(Window owner, string title, string prompt)
    {
        var dialog = new TextPromptDialog(title, prompt);
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }
}
