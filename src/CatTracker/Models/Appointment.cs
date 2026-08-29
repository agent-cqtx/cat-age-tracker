namespace CatTracker.Models;

public enum AppointmentCategory
{
    Vet,
    Grooming,
    Medication,
    Other
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime When { get; set; }
    public AppointmentCategory Category { get; set; } = AppointmentCategory.Other;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
