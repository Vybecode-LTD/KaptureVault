using Avalonia.Media;

namespace Kapture.Themes;

public static class Colors
{
    // Backgrounds
    public const string BgDark = "#0D1117";
    public const string BgSecondary = "#161B22";
    public const string BgTertiary = "#21262D";

    // Borders
    public const string Border = "#30363D";

    // Text
    public const string TextPrimary = "#E6EDF3";
    public const string TextSecondary = "#8B949E";

    // Accent (amber)
    public const string Accent = "#F0A500";
    public const string AccentHover = "#D4940A";
    public const string AccentDim = "#7A5500";

    // Status
    public const string Success = "#3FB950";
    public const string Warning = "#D29922";
    public const string Error = "#F85149";
    public const string Info = "#58A6FF";

    // Brush helpers
    public static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));

    public static readonly SolidColorBrush BgDarkBrush = Brush(BgDark);
    public static readonly SolidColorBrush BgSecondaryBrush = Brush(BgSecondary);
    public static readonly SolidColorBrush BgTertiaryBrush = Brush(BgTertiary);
    public static readonly SolidColorBrush BorderBrush = Brush(Border);
    public static readonly SolidColorBrush TextPrimaryBrush = Brush(TextPrimary);
    public static readonly SolidColorBrush TextSecondaryBrush = Brush(TextSecondary);
    public static readonly SolidColorBrush AccentBrush = Brush(Accent);
    public static readonly SolidColorBrush AccentHoverBrush = Brush(AccentHover);
    public static readonly SolidColorBrush AccentDimBrush = Brush(AccentDim);
    public static readonly SolidColorBrush SuccessBrush = Brush(Success);
    public static readonly SolidColorBrush WarningBrush = Brush(Warning);
    public static readonly SolidColorBrush ErrorBrush = Brush(Error);
    public static readonly SolidColorBrush InfoBrush = Brush(Info);
}
