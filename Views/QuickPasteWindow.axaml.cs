using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Kapture.Models;
using Kapture.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kapture.Views;

// Small converter class referenced from AXAML — avoids needing the main VM
public static class QuickPasteConverters
{
    public static readonly IValueConverter TypeColorConverter = new TypeColorValueConverter();
    public static readonly IValueConverter TypeIconConverter = new TypeIconValueConverter();
    public static readonly IValueConverter PreviewConverter = new PreviewValueConverter();

    private class TypeColorValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value switch
            {
                "clipboard"  => new SolidColorBrush(Color.Parse("#D2A8FF")),
                "screenshot" => new SolidColorBrush(Color.Parse("#58A6FF")),
                _            => new SolidColorBrush(Color.Parse("#3FB950")),
            };
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private class TypeIconValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value switch { "clipboard" => "CB", "screenshot" => "SC", _ => "KB" };
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private class PreviewValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                if (File.Exists(text)) return $"📷 Screenshot ({new FileInfo(text).Length / 1024} KB)";
                var clean = text.ReplaceLineEndings(" ").Trim();
                return clean.Length > 100 ? clean[..100] + "..." : clean;
            }
            return string.Empty;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

public partial class QuickPasteWindow : Window
{
    private readonly IDatabaseService _db;
    private readonly ObservableCollection<CaptureEntry> _results = [];
    private bool _fullyOpened;

    /// <summary>The content that should be pasted after this window closes.</summary>
    public string? ContentToPaste { get; private set; }

    public QuickPasteWindow()
    {
        InitializeComponent();

        _db = App.Services.GetRequiredService<IDatabaseService>();
        ResultsList.ItemsSource = _results;

        // Load recent entries immediately
        LoadResults(string.Empty);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        SearchBox.Focus();
        // Delay setting the flag so the initial activation cycle completes
        await Task.Delay(300);
        _fullyOpened = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_fullyOpened && change.Property == IsActiveProperty && change.GetNewValue<bool>() == false)
        {
            ContentToPaste = null;
            Close();
        }
    }

    // Handle Escape at window level (not just SearchBox)
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ContentToPaste = null;
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void LoadResults(string query)
    {
        _results.Clear();

        List<CaptureEntry> entries;
        if (string.IsNullOrWhiteSpace(query))
            entries = _db.GetAll();
        else
            entries = _db.Search(query);

        // Exclude screenshots from quick paste (can't paste images as text)
        var textEntries = entries.Where(e => e.EntryType != "screenshot").Take(50);

        foreach (var entry in textEntries)
            _results.Add(entry);

        if (_results.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        LoadResults(SearchBox.Text ?? string.Empty);
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                ContentToPaste = null;
                Close();
                e.Handled = true;
                break;

            case Key.Enter:
                PasteSelected();
                e.Handled = true;
                break;

            case Key.Down:
                if (ResultsList.SelectedIndex < _results.Count - 1)
                    ResultsList.SelectedIndex++;
                e.Handled = true;
                break;

            case Key.Up:
                if (ResultsList.SelectedIndex > 0)
                    ResultsList.SelectedIndex--;
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        PasteSelected();
    }

    private void PasteSelected()
    {
        if (ResultsList.SelectedItem is CaptureEntry entry)
        {
            ContentToPaste = entry.Content;
            Close();
        }
    }
}
