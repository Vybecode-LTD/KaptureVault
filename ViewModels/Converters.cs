using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Kapture.ViewModels;

public partial class MainWindowViewModel
{
    public static readonly IValueConverter PreviewConverter = new PreviewTextConverter();
    public static readonly IValueConverter PinLabelConverter = new PinLabelTextConverter();
    public static readonly IValueConverter RecordingColorConverter = new RecordingColorValueConverter();
    public static readonly IValueConverter EntryTypeIconConverter = new EntryTypeIconValueConverter();
    public static readonly IValueConverter EntryTypeColorConverter = new EntryTypeColorValueConverter();
    public static readonly IValueConverter TypeFilterActiveConverter = new TypeFilterActiveValueConverter();
    public static readonly IValueConverter LanguageDisplayConverter = new LanguageDisplayValueConverter();
    public static readonly IValueConverter BufferFillColorConverter = new BufferFillColorValueConverter();
    public static readonly IValueConverter BufferFillWidthConverter = new BufferFillWidthValueConverter();
    public static readonly IValueConverter ScreenshotThumbnailConverter = new ScreenshotThumbnailValueConverter();
    public static readonly IValueConverter ScreenshotPreviewConverter = new ScreenshotPreviewValueConverter();

    // Section switcher converters
    public static readonly IValueConverter ActiveSectionFontWeight =
        new FuncValueConverter<bool, FontWeight>(active => active ? FontWeight.SemiBold : FontWeight.Normal);
    public static readonly IValueConverter ActiveSectionForeground =
        new FuncValueConverter<bool, IBrush>(active => active
            ? new SolidColorBrush(Color.Parse("#F0A500"))
            : new SolidColorBrush(Color.Parse("#8B949E")));
    public static readonly IValueConverter SearchWatermarkConverter =
        new FuncValueConverter<bool, string>(isVault => isVault ? "Search entries..." : "Search...");

    private class PreviewTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                var clean = text.ReplaceLineEndings(" ").Trim();
                return clean.Length > 80 ? clean[..80] + "..." : clean;
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class PinLabelTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? "Unpin" : "Pin";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class RecordingColorValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Color.Parse("#3FB950") : Color.Parse("#8B949E");

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class EntryTypeIconValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value switch { "clipboard" => "CB", "screenshot" => "SC", _ => "KB" };

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class TypeFilterActiveValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string selected && parameter is string button)
                return selected == button ? 1.0 : 0.4;
            return 0.4;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class EntryTypeColorValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value switch
            {
                "clipboard"  => new SolidColorBrush(Color.Parse("#D2A8FF")),  // purple
                "screenshot" => new SolidColorBrush(Color.Parse("#58A6FF")),  // blue
                _            => new SolidColorBrush(Color.Parse("#3FB950")),  // green
            };

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class LanguageDisplayValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string langId ? Services.LanguageDetector.GetDisplayName(langId) : string.Empty;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class BufferFillColorValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int charCount)
            {
                double ratio = Math.Min(charCount / 5000.0, 1.0);
                if (ratio > 0.8) return new SolidColorBrush(Color.Parse("#F85149")); // red
                if (ratio > 0.5) return new SolidColorBrush(Color.Parse("#D29922")); // yellow
                return new SolidColorBrush(Color.Parse("#3FB950")); // green
            }
            return new SolidColorBrush(Color.Parse("#3FB950"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class BufferFillWidthValueConverter : IValueConverter
    {
        private const double MaxBarWidth = 200.0; // max pixel width of the bar

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int charCount)
            {
                double ratio = Math.Min(charCount / 5000.0, 1.0);
                return Math.Max(ratio * MaxBarWidth, 2.0); // minimum 2px so it's always visible
            }
            return 2.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class ScreenshotThumbnailValueConverter : IValueConverter
    {
        // LRU bitmap cache to avoid re-reading files on every scroll
        private const int MaxCacheSize = 40;
        private static readonly LinkedList<(string Path, DateTime LastWrite, Bitmap Bmp)> _cache = new();
        private static readonly object _cacheLock = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || !File.Exists(path))
                return null;

            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(path);

                lock (_cacheLock)
                {
                    // Check cache for existing entry
                    var node = _cache.First;
                    while (node != null)
                    {
                        if (node.Value.Path == path)
                        {
                            if (node.Value.LastWrite == lastWrite)
                            {
                                // Cache hit — move to front (MRU)
                                _cache.Remove(node);
                                _cache.AddFirst(node);
                                return node.Value.Bmp;
                            }
                            // Stale — dispose and remove
                            node.Value.Bmp.Dispose();
                            _cache.Remove(node);
                            break;
                        }
                        node = node.Next;
                    }

                    // Cache miss — load and add to front
                    var bmp = new Bitmap(path);
                    _cache.AddFirst((path, lastWrite, bmp));

                    // Evict LRU entries past limit
                    while (_cache.Count > MaxCacheSize)
                    {
                        var last = _cache.Last!;
                        last.Value.Bmp.Dispose();
                        _cache.RemoveLast();
                    }

                    return bmp;
                }
            }
            catch { return null; }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class ScreenshotPreviewValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // For screenshot entries, show "Screenshot - {filesize}" instead of the file path
            if (value is string content && File.Exists(content))
            {
                var size = new FileInfo(content).Length;
                return size > 1024 * 1024
                    ? $"Screenshot ({size / (1024 * 1024.0):F1} MB)"
                    : $"Screenshot ({size / 1024.0:F0} KB)";
            }
            // Fall through to normal preview for non-screenshot entries
            if (value is string text)
            {
                var clean = text.ReplaceLineEndings(" ").Trim();
                return clean.Length > 80 ? clean[..80] + "..." : clean;
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
