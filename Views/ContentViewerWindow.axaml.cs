using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Kapture.Models;
using Kapture.Services;
using TextMateSharp.Grammars;

namespace Kapture.Views;

public partial class ContentViewerWindow : Window
{
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;
    private TextEditor? _editor;
    private Bitmap? _screenshotBitmap;

    private static readonly Dictionary<string, string> LanguageExtensions = new()
    {
        ["Plain Text"] = "",
        ["C#"] = ".cs",
        ["JavaScript"] = ".js",
        ["TypeScript"] = ".ts",
        ["Python"] = ".py",
        ["Java"] = ".java",
        ["Go"] = ".go",
        ["Rust"] = ".rs",
        ["C++"] = ".cpp",
        ["C"] = ".c",
        ["HTML"] = ".html",
        ["CSS"] = ".css",
        ["JSON"] = ".json",
        ["XML"] = ".xml",
        ["YAML"] = ".yaml",
        ["SQL"] = ".sql",
        ["Markdown"] = ".md",
        ["PHP"] = ".php",
        ["Ruby"] = ".rb",
        ["Shell"] = ".sh",
        ["PowerShell"] = ".ps1",
    };

    private static readonly Dictionary<string, string> DetectorToDisplay = new()
    {
        ["csharp"] = "C#",
        ["javascript"] = "JavaScript",
        ["typescript"] = "TypeScript",
        ["python"] = "Python",
        ["java"] = "Java",
        ["go"] = "Go",
        ["rust"] = "Rust",
        ["cpp"] = "C++",
        ["c"] = "C",
        ["html"] = "HTML",
        ["css"] = "CSS",
        ["json"] = "JSON",
        ["xml"] = "XML",
        ["yaml"] = "YAML",
        ["sql"] = "SQL",
        ["markdown"] = "Markdown",
        ["php"] = "PHP",
        ["ruby"] = "Ruby",
        ["shellscript"] = "Shell",
        ["powershell"] = "PowerShell",
    };

    public ContentViewerWindow()
    {
        InitializeComponent();
    }

    public ContentViewerWindow(CaptureEntry entry) : this()
    {
        Title = $"Kapture - {entry.AppName}";

        var appNameText = this.FindControl<TextBlock>("AppNameText")!;
        var languageText = this.FindControl<TextBlock>("LanguageText")!;
        var metaText = this.FindControl<TextBlock>("MetaText")!;
        _editor = this.FindControl<TextEditor>("ContentEditor")!;
        var langSelector = this.FindControl<ComboBox>("LanguageSelector")!;
        var langSelectorPanel = this.FindControl<StackPanel>("LanguageSelectorPanel")!;
        var screenshotViewer = this.FindControl<ScrollViewer>("ScreenshotViewer")!;
        var screenshotImage = this.FindControl<Image>("ScreenshotImage")!;

        appNameText.Text = entry.AppName;

        if (entry.IsScreenshot)
        {
            // Screenshot mode
            metaText.Text = $"{entry.WindowTitle}  |  {entry.CapturedAt:yyyy-MM-dd HH:mm:ss}  |  screenshot";

            // Hide text editor and language selector, show image viewer
            _editor.IsVisible = false;
            langSelectorPanel.IsVisible = false;
            screenshotViewer.IsVisible = true;
            languageText.Text = "[Screenshot]";

            // Load image
            if (File.Exists(entry.Content))
            {
                try
                {
                    _screenshotBitmap = new Bitmap(entry.Content);
                    screenshotImage.Source = _screenshotBitmap;
                    var fileSize = new FileInfo(entry.Content).Length;
                    var sizeText = fileSize > 1024 * 1024
                        ? $"{fileSize / (1024 * 1024.0):F1} MB"
                        : $"{fileSize / 1024.0:F0} KB";
                    metaText.Text = $"{entry.WindowTitle}  |  {sizeText}  |  {entry.CapturedAt:yyyy-MM-dd HH:mm:ss}";
                }
                catch
                {
                    languageText.Text = "[Image load failed]";
                }
            }
            else
            {
                languageText.Text = "[Image file not found]";
            }
        }
        else
        {
            // Text mode
            metaText.Text = $"{entry.WindowTitle}  |  {entry.CharCount} chars  |  {entry.CapturedAt:yyyy-MM-dd HH:mm:ss}  |  {entry.EntryType}";

            screenshotViewer.IsVisible = false;
            langSelector.ItemsSource = LanguageExtensions.Keys.ToList();

            // Step 1: Install TextMate FIRST
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            _textMateInstallation = _editor.InstallTextMate(_registryOptions);

            // Step 2: Set grammar SECOND
            string displayName = "Plain Text";
            if (!string.IsNullOrEmpty(entry.DetectedLanguage) &&
                DetectorToDisplay.TryGetValue(entry.DetectedLanguage, out var detected))
            {
                displayName = detected;
            }
            ApplyGrammar(displayName);
            languageText.Text = displayName != "Plain Text" ? $"[{displayName}]" : "";

            // Step 3: Set text LAST
            _editor.Document.Text = entry.Content;

            langSelector.SelectedItem = displayName;
        }
    }

    private void ApplyGrammar(string langName)
    {
        if (_textMateInstallation == null || _registryOptions == null) return;

        if (langName == "Plain Text" || !LanguageExtensions.TryGetValue(langName, out var ext) || string.IsNullOrEmpty(ext))
        {
            _textMateInstallation.SetGrammar(null);
            return;
        }

        var language = _registryOptions.GetLanguageByExtension(ext);
        var scope = _registryOptions.GetScopeByLanguageId(language.Id);
        _textMateInstallation.SetGrammar(scope);
    }

    protected override void OnClosed(EventArgs e)
    {
        _screenshotBitmap?.Dispose();
        _screenshotBitmap = null;
        _textMateInstallation?.Dispose();
        _textMateInstallation = null;
        base.OnClosed(e);
    }

    private void LanguageSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not string langName) return;

        ApplyGrammar(langName);

        if (_editor != null)
        {
            var text = _editor.Document.Text;
            _editor.Document.Text = text;
        }

        var languageText = this.FindControl<TextBlock>("LanguageText");
        if (languageText != null)
            languageText.Text = langName != "Plain Text" ? $"[{langName}]" : "";
    }
}
