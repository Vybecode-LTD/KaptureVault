using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Kapture.Views.Dialogs;

/// <summary>
/// The "Upload" dialog (P5). File hosting is the paid differentiator (Phase 6), so this adapts to the
/// account tier set on its <see cref="Kapture.ViewModels.OnlineAccountViewModel"/> DataContext: a free
/// user sees the upgrade pitch (→ Stripe checkout via SubscribeCommand), a paid user sees the
/// "coming soon" notice. Pure view — all state + commands live on the shared account view model.
/// </summary>
public partial class UploadDialog : Window
{
    public UploadDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
