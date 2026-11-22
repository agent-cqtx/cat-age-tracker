using CatTracker.Models;
using CatTracker.Services;

namespace CatTracker.Endpoints;

public static class AppointmentEndpoints
{
    private record AppointmentRequest(string Title, string? Notes, DateTime When, AppointmentCategory Category);

    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointments");

        group.MapGet("/", async (AppointmentRepository repo) => Results.Ok(await repo.GetAllAsync()));

        group.MapPost("/", async (AppointmentRequest req, AppointmentRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
            {
                return Results.BadRequest(new { error = "Title is required." });
            }

            var appointment = new Appointment
            {
                Title = req.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
                When = req.When,
                Category = req.Category
            };

            var created = await repo.AddAsync(appointment);
            return Results.Created($"/api/appointments/{created.Id}", created);
        }).RequireAuthorization();

        group.MapPut("/{id:guid}", async (Guid id, AppointmentRequest req, AppointmentRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
            {
                return Results.BadRequest(new { error = "Title is required." });
            }

            var updated = await repo.UpdateAsync(id, a =>
            {
                a.Title = req.Title.Trim();
                a.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
                a.When = req.When;
                a.Category = req.Category;
            });

            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequireAuthorization();

        group.MapDelete("/{id:guid}", async (Guid id, AppointmentRepository repo, AttachmentRepository attachments) =>
        {
            if (await repo.GetByIdAsync(id) is null) return Results.NotFound();

            // Cascade: an appointment's attachments die with it. Attachment
            // rows go FIRST, then the appointment row: with the opposite
            // order, a concurrent upload that validated the appointment
            // before the delete could attach a row+blob to a dead
            // appointment id, unreachable through every list endpoint
            // forever. Blobs go last — the rows are the only handle to them.
            var removed = await attachments.DeleteForAppointmentAsync(id);
            await repo.DeleteAsync(id);
            foreach (var attachment in removed)
            {
                attachments.TryDeleteBlob(attachment.Id);
            }

            return Results.NoContent();
        }).RequireAuthorization();
    }
}
