using CatTracker.Models;
using CatTracker.Services;

namespace CatTracker.Endpoints;

/// <summary>
/// File attachments, two flavors:
///  - per-appointment: /api/appointments/{id}/attachments
///  - general ("Documents"): /api/attachments (AppointmentId == null)
/// Uploads are multipart with a single "file" field. Reads (list + file
/// bytes) are open like every other GET in this app; mutations require auth.
/// </summary>
public static class AttachmentEndpoints
{
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    };

    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- General attachments ------------------------------------------
        var group = app.MapGroup("/api/attachments");

        group.MapGet("/", async (AttachmentRepository attachments) =>
            Results.Ok(await attachments.GetGeneralAsync()));

        group.MapPost("/", async (HttpContext ctx, AttachmentRepository attachments, AppointmentRepository appointments) =>
            await HandleUpload(ctx, null, attachments, appointments)
        ).RequireAuthorization();

        // Serves the bytes. Open (no auth) so <img src> tags work, same
        // read-open policy as the other GETs.
        group.MapGet("/{id:guid}/file", async (Guid id, HttpContext ctx, AttachmentRepository attachments) =>
        {
            var attachment = await attachments.GetByIdAsync(id);
            if (attachment is null || !attachments.BlobExists(id)) return Results.NotFound();

            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var stream = attachments.OpenBlobRead(id);
            return Results.File(stream, attachment.ContentType, fileDownloadName: attachment.FileName);
        });

        // Handles BOTH kinds — metadata row + blob together.
        group.MapDelete("/{id:guid}", async (Guid id, AttachmentRepository attachments) =>
        {
            var removed = await attachments.DeleteAsync(id);
            if (removed is null) return Results.NotFound();
            attachments.TryDeleteBlob(id);
            return Results.NoContent();
        }).RequireAuthorization();

        // ---- Per-appointment attachments -----------------------------------
        var apptGroup = app.MapGroup("/api/appointments");

        apptGroup.MapGet("/{appointmentId:guid}/attachments", async (Guid appointmentId, AppointmentRepository appointments, AttachmentRepository attachments) =>
        {
            if (await appointments.GetByIdAsync(appointmentId) is null) return Results.NotFound();
            return Results.Ok(await attachments.GetForAppointmentAsync(appointmentId));
        });

        apptGroup.MapPost("/{appointmentId:guid}/attachments", async (Guid appointmentId, HttpContext ctx, AppointmentRepository appointments, AttachmentRepository attachments) =>
        {
            if (await appointments.GetByIdAsync(appointmentId) is null) return Results.NotFound();
            return await HandleUpload(ctx, appointmentId, attachments, appointments);
        }).RequireAuthorization();
    }

    private static async Task<IResult> HandleUpload(
        HttpContext ctx,
        Guid? appointmentId,
        AttachmentRepository attachments,
        AppointmentRepository appointments)
    {
        if (!ctx.Request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Expected a multipart form upload with a 'file' field." });
        }

        var form = await ctx.Request.ReadFormAsync();
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "No file provided." });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return Results.Json(new { error = $"Unsupported file type: {file.ContentType}" },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (file.Length > MaxSizeBytes)
        {
            return Results.Json(new { error = "File exceeds the 10 MB limit." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var attachment = new Attachment
        {
            AppointmentId = appointmentId,
            FileName = SanitizeFileName(file.FileName),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
        };

        // Blob first: if the metadata write then fails, no API path can
        // reference the blob, whereas metadata pointing at a missing blob
        // would show a broken item forever.
        await using (var upload = file.OpenReadStream())
        {
            await attachments.SaveBlobAsync(attachment.Id, upload);
        }

        var created = await attachments.AddAsync(attachment);
        return Results.Created($"/api/attachments/{created.Id}", created);
    }

    /// <summary>
    /// Display-name only: drop any directory part (browsers may send paths,
    /// hostile clients certainly will) and all control characters. The result
    /// is only ever rendered via textContent and never used for file paths —
    /// blob paths are built exclusively from Guids.
    /// </summary>
    private static string SanitizeFileName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "attachment";

        var name = rawName.Split('/', '\\')[^1];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (name.Length == 0) name = "attachment";
        if (name.Length > 200) name = name[..200];
        return name;
    }
}
