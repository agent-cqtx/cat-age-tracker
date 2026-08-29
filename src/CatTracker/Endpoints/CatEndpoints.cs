using CatTracker.Data;
using CatTracker.Models;
using CatTracker.Services;

namespace CatTracker.Endpoints;

public static class CatEndpoints
{
    private record CatStatusResponse(
        bool NeedsOnboarding,
        string Name,
        string Breed,
        string ColorPattern,
        DateOnly BirthDate,
        string Theme,
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

    private record ProfileUpdateRequest(string Name, string? Breed, string? ColorPattern, DateOnly BirthDate, string Theme);

    public static void MapCatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cat");

        group.MapGet("/", async (ProfileRepository repo) =>
        {
            var (profile, isOnboarded) = await repo.GetWithStatusAsync();

            if (!isOnboarded)
            {
                // No real data yet -- the frontend shows the onboarding form
                // instead of the dashboard, so these computed fields are
                // just harmless placeholders.
                return Results.Ok(new CatStatusResponse(
                    NeedsOnboarding: true,
                    profile.Name, profile.Breed, profile.ColorPattern, profile.BirthDate, profile.Theme,
                    CatYears: 0, FormattedAge: "", HumanEquivalentYears: 0,
                    LifeStageName: "", LifeStageRangeLabel: "", LifeStageSummary: "", LifeStageDescription: ""));
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var years = CatAgeMath.CatYearsBetween(profile.BirthDate, today);
            var stage = CatAgeMath.GetLifeStage(years, LifeStageCatalog.Stages);

            return Results.Ok(new CatStatusResponse(
                NeedsOnboarding: false,
                profile.Name,
                profile.Breed,
                profile.ColorPattern,
                profile.BirthDate,
                profile.Theme,
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
            var (profile, isOnboarded) = await repo.GetWithStatusAsync();
            if (!isOnboarded)
            {
                return Results.Conflict(new { error = "Cat profile hasn't been set up yet." });
            }

            years = Math.Clamp(years, 0, 30);

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

        group.MapGet("/themes", () => Results.Ok(ThemeCatalog.Themes));

        group.MapPut("/", async (ProfileUpdateRequest req, ProfileRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest(new { error = "Name is required." });
            }

            if (req.BirthDate == default || req.BirthDate > DateOnly.FromDateTime(DateTime.Now))
            {
                return Results.BadRequest(new { error = "A valid birth date is required." });
            }

            if (string.IsNullOrWhiteSpace(req.Theme) || !ThemeCatalog.IsValidKey(req.Theme))
            {
                return Results.BadRequest(new { error = "Unrecognized theme." });
            }

            var updated = new CatProfile
            {
                Name = req.Name.Trim(),
                Breed = req.Breed?.Trim() ?? "",
                ColorPattern = req.ColorPattern?.Trim() ?? "",
                BirthDate = req.BirthDate,
                Theme = req.Theme,
            };

            await repo.SaveAsync(updated);
            return Results.Ok(updated);
        }).RequireAuthorization();
    }
}
