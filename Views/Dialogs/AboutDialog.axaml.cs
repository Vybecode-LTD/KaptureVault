using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace Kapture.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Reflect the <Version> tag from the .csproj at runtime
        var ver = typeof(AboutDialog).Assembly.GetName().Version;
        VersionText.Text = ver != null ? ver.ToString(3) : "1.0.0";
    }

    private void WebsiteLink_Tapped(object? sender, TappedEventArgs e)
    {
        OpenUrl("https://kapture.tools");
    }

    private void PublisherLink_Tapped(object? sender, TappedEventArgs e)
    {
        OpenUrl("https://www.vybeco.de");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignore if no browser is registered */ }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
