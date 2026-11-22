namespace CatTracker.Models;

/// <summary>
/// Metadata for an uploaded file (vet invoice, vaccine certificate, etc.).
/// The bytes themselves live on disk under {dataDir}/attachments named by
/// <see cref="Id"/> in "N" format (no extension) — this record only carries
/// what's needed to list, render, and serve the file.
///
/// <see cref="AppointmentId"/> == null means the attachment is a "general"
/// document: not tied to any appointment, always visible in the Documents
/// panel.
/// </summary>
public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AppointmentId { get; set; }
    /// <summary>Sanitized display name only — never used to build a file path.</summary>
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
