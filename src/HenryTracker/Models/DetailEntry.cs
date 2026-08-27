namespace HenryTracker.Models;

/// <summary>
/// A freeform piece of information about the cat — e.g. vet contact info,
/// microchip number, food preferences, a weight-log entry, etc. Category is
/// a free-text label rather than an enum so the single user can invent their
/// own organization scheme without a code change.
/// </summary>
public class DetailEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = "General";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateOnly? Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
