using CatTracker.Data;

namespace CatTracker.Models;

/// <summary>
/// The single cat profile tracked by this app. There is always exactly one
/// of these — it is stored as a single JSON object rather than a list.
///
/// A freshly-defaulted instance (no profile.json saved yet) has an empty
/// name and BirthDate == DateOnly.MinValue. Endpoints/ProfileRepository
/// use "does profile.json exist on disk" (not a field on this class) to
/// decide whether onboarding is still needed — see
/// ProfileRepository.GetWithStatusAsync.
/// </summary>
public class CatProfile
{
    public string Name { get; set; } = "";
    public string Breed { get; set; } = "";
    public string ColorPattern { get; set; } = "";
    public DateOnly BirthDate { get; set; } = DateOnly.MinValue;

    /// <summary>Key into ThemeCatalog.Themes, e.g. "tuxedo".</summary>
    public string Theme { get; set; } = ThemeCatalog.DefaultThemeKey;
}
