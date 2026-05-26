namespace Kapture.Themes;

public record ThemeDefinition(
    string Name,
    string BaseVariant,  // "Dark" or "Light" — sets FluentTheme base
    string BgPrimary,
    string BgSecondary,
    string BgTertiary,
    string Border,
    string TextPrimary,
    string TextSecondary,
    string Accent,
    string AccentHover,
    string AccentDim
);
