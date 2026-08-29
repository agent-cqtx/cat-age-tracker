namespace CatTracker.Models;

/// <summary>
/// A selectable visual theme, meant to roughly match a cat's coloring.
/// Colors here are the literal hex values used both by the frontend's
/// per-theme CSS blocks and to build the dynamic favicon — keep this in
/// sync with wwwroot/css/styles.css if you add or edit a theme (see the
/// [data-theme="..."] blocks there).
/// </summary>
public class ThemeInfo
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Swatch color shown in the theme picker (matches --mark in CSS).</summary>
    public required string SwatchColor { get; init; }
}
