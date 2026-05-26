namespace Kapture.Themes;

public static class ThemeRegistry
{
    public static readonly Dictionary<string, ThemeDefinition> Themes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dark"] = new ThemeDefinition(
            Name: "Dark",
            BaseVariant: "Dark",
            BgPrimary: "#0D1117",
            BgSecondary: "#161B22",
            BgTertiary: "#21262D",
            Border: "#30363D",
            TextPrimary: "#E6EDF3",
            TextSecondary: "#8B949E",
            Accent: "#F0A500",
            AccentHover: "#D4940A",
            AccentDim: "#7A5500"
        ),
        ["Light"] = new ThemeDefinition(
            Name: "Light",
            BaseVariant: "Light",
            BgPrimary: "#FFFFFF",
            BgSecondary: "#F6F8FA",
            BgTertiary: "#DDE2E8",
            Border: "#B0B8C1",
            TextPrimary: "#1F2328",
            TextSecondary: "#656D76",
            Accent: "#D4940A",
            AccentHover: "#B87D08",
            AccentDim: "#8A6006"
        ),
        ["Sunset"] = new ThemeDefinition(
            Name: "Sunset",
            BaseVariant: "Dark",
            BgPrimary: "#1A0E08",
            BgSecondary: "#2A1810",
            BgTertiary: "#3A2820",
            Border: "#5A3820",
            TextPrimary: "#F5E6D3",
            TextSecondary: "#B89878",
            Accent: "#FF6B35",
            AccentHover: "#E05A28",
            AccentDim: "#8A3A18"
        ),
        ["Dawn"] = new ThemeDefinition(
            Name: "Dawn",
            BaseVariant: "Dark",
            BgPrimary: "#1A0F1A",
            BgSecondary: "#241828",
            BgTertiary: "#2E2232",
            Border: "#483050",
            TextPrimary: "#F0E0F5",
            TextSecondary: "#A888B8",
            Accent: "#C77DBA",
            AccentHover: "#A868A0",
            AccentDim: "#6E4868"
        ),
        ["Oceanic"] = new ThemeDefinition(
            Name: "Oceanic",
            BaseVariant: "Dark",
            BgPrimary: "#0A1628",
            BgSecondary: "#0E1E30",
            BgTertiary: "#142838",
            Border: "#1E3A50",
            TextPrimary: "#D0E8F8",
            TextSecondary: "#7AA8C8",
            Accent: "#00B4D8",
            AccentHover: "#0098B8",
            AccentDim: "#006880"
        ),
        ["Rose"] = new ThemeDefinition(
            Name: "Rose",
            BaseVariant: "Dark",
            BgPrimary: "#1A0A10",
            BgSecondary: "#241018",
            BgTertiary: "#2E1820",
            Border: "#4A2030",
            TextPrimary: "#F5D8E0",
            TextSecondary: "#B88898",
            Accent: "#E84080",
            AccentHover: "#C83068",
            AccentDim: "#882048"
        ),
    };

    public static IReadOnlyList<string> ThemeNames { get; } = [.. Themes.Keys];
}
