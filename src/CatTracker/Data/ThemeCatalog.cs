using CatTracker.Models;

namespace CatTracker.Data;

/// <summary>
/// The set of selectable visual themes. Solid-color palettes only for now —
/// no patterned options (tabby stripes, calico patches) yet. See each
/// [data-theme="..."] block in wwwroot/css/styles.css for the full palette
/// each key maps to (backgrounds, text, hairlines) — this catalog only
/// carries what the backend/API need: the picker copy and swatch color.
/// </summary>
public static class ThemeCatalog
{
    public const string DefaultThemeKey = "tuxedo";

    public static readonly IReadOnlyList<ThemeInfo> Themes = new List<ThemeInfo>
    {
        new()
        {
            Key = "tuxedo",
            Name = "Tuxedo",
            Description = "Black & white",
            SwatchColor = "#111111",
        },
        new()
        {
            Key = "black",
            Name = "Black",
            Description = "Solid black",
            SwatchColor = "#0D0D0D",
        },
        new()
        {
            Key = "white",
            Name = "White",
            Description = "White or cream",
            SwatchColor = "#D8D8D4",
        },
        new()
        {
            Key = "gray",
            Name = "Gray",
            Description = "Gray or blue",
            SwatchColor = "#6B6E73",
        },
        new()
        {
            Key = "ginger",
            Name = "Ginger",
            Description = "Orange tabby",
            SwatchColor = "#C1662C",
        },
    };

    public static bool IsValidKey(string key) => Themes.Any(t => t.Key == key);
}
