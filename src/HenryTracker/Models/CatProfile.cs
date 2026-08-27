namespace HenryTracker.Models;

/// <summary>
/// The single cat profile tracked by this app. There is always exactly one
/// of these — it is stored as a single JSON object rather than a list.
/// </summary>
public class CatProfile
{
    public string Name { get; set; } = "Henry";
    public string Breed { get; set; } = "American Shorthair";
    public string ColorPattern { get; set; } = "Black and white tuxedo";
    public DateOnly BirthDate { get; set; } = new DateOnly(2020, 6, 26);
}
