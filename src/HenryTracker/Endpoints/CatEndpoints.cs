using HenryTracker.Data;
using HenryTracker.Models;
using HenryTracker.Services;

namespace HenryTracker.Endpoints;

public static class CatEndpoints
{
    private record CatStatusResponse(
        string Name,
        string Breed,
        string ColorPattern,
        DateOnly BirthDate,
        double CatYears,
        string FormattedAge,
        double HumanEquivalentYears,
        string LifeStageName,
        string LifeStageRangeLabel,
        string LifeStageSummary,
        string LifeStageDescription);

    private record AgeAtResponse(
        double CatYears,
        DateOnly Date,
        string FormattedAge,
        double HumanEquivalentYears,
        string LifeStageName,
        string LifeStageRangeLabel,
        string LifeStageSummary,
        string LifeStageDescription,
        bool IsPast,
        bool IsFuture,
        bool IsToday,
        string RelativeLabel);

    private record ProfileUpdateRequest(string Name, string Breed, string ColorPattern, DateOnly BirthDate);

    public static void MapCatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cat");

        group.MapGet("/", async (ProfileRepository repo) =>
        {
            var profile = await repo.GetAsync();
            var today = DateOnly.FromDateTime(DateTime.Now);
            var years = CatAgeMath.CatYearsBetween(profile.BirthDate, today);
            var stage = CatAgeMath.GetLifeStage(years, LifeStageCatalog.Stages);

            return Results.Ok(new CatStatusResponse(
                profile.Name,
                profile.Breed,
                profile.ColorPattern,
                profile.BirthDate,
                Math.Round(years, 3),
                CatAgeMath.FormatAge(years),
                Math.Round(CatAgeMath.HumanEquivalentYears(years), 1),
                stage.Name,
                stage.RangeLabel,
                stage.Summary,
                stage.Description));
        });

        group.MapGet("/age-at", async (double years, ProfileRepository repo) =>
        {
            years = Math.Clamp(years, 0, 30);

            var profile = await repo.GetAsync();
            var targetDate = profile.BirthDate.AddDays((int)Math.Round(years * 365.25));
            var today = DateOnly.FromDateTime(DateTime.Now);
            var stage = CatAgeMath.GetLifeStage(years, LifeStageCatalog.Stages);

            var dayDiff = targetDate.DayNumber - today.DayNumber;

            return Results.Ok(new AgeAtResponse(
                Math.Round(years, 3),
                targetDate,
                CatAgeMath.FormatAge(years),
                Math.Round(CatAgeMath.HumanEquivalentYears(years), 1),
                stage.Name,
                stage.RangeLabel,
                stage.Summary,
                stage.Description,
                IsPast: dayDiff < 0,
                IsFuture: dayDiff > 0,
                IsToday: dayDiff == 0,
                RelativeLabel: CatAgeMath.FormatRelative(dayDiff)));
        });

        group.MapGet("/lifestages", () => Results.Ok(LifeStageCatalog.Stages));

        group.MapPut("/", async (ProfileUpdateRequest req, ProfileRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest(new { error = "Name is required." });
            }

            var updated = new CatProfile
            {
                Name = req.Name.Trim(),
                Breed = req.Breed?.Trim() ?? "",
                ColorPattern = req.ColorPattern?.Trim() ?? "",
                BirthDate = req.BirthDate
            };

            await repo.SaveAsync(updated);
            return Results.Ok(updated);
        }).RequireAuthorization();
    }
}
