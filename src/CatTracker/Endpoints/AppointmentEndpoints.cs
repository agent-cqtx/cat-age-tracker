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

        group.MapDelete("/{id:guid}", async (Guid id, AppointmentRepository repo) =>
            await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound()
        ).RequireAuthorization();
    }
}
