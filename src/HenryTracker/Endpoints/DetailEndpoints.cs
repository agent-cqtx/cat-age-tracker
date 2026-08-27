using HenryTracker.Models;
using HenryTracker.Services;

namespace HenryTracker.Endpoints;

public static class DetailEndpoints
{
    private record DetailRequest(string Category, string Title, string Content, DateOnly? Date);

    public static void MapDetailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/details");

        group.MapGet("/", async (DetailRepository repo) => Results.Ok(await repo.GetAllAsync()));

        group.MapPost("/", async (DetailRequest req, DetailRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
            {
                return Results.BadRequest(new { error = "Title is required." });
            }

            var entry = new DetailEntry
            {
                Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category.Trim(),
                Title = req.Title.Trim(),
                Content = req.Content?.Trim() ?? "",
                Date = req.Date
            };

            var created = await repo.AddAsync(entry);
            return Results.Created($"/api/details/{created.Id}", created);
        }).RequireAuthorization();

        group.MapPut("/{id:guid}", async (Guid id, DetailRequest req, DetailRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
            {
                return Results.BadRequest(new { error = "Title is required." });
            }

            var updated = await repo.UpdateAsync(id, e =>
            {
                e.Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category.Trim();
                e.Title = req.Title.Trim();
                e.Content = req.Content?.Trim() ?? "";
                e.Date = req.Date;
            });

            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).RequireAuthorization();

        group.MapDelete("/{id:guid}", async (Guid id, DetailRepository repo) =>
            await repo.DeleteAsync(id) ? Results.NoContent() : Results.NotFound()
        ).RequireAuthorization();
    }
}
