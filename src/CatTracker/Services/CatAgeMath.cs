using CatTracker.Models;

namespace CatTracker.Services;

/// <summary>
/// Cat-to-human age conversion and life-stage lookup. The conversion is the
/// commonly cited veterinary "15-9-4" model: a cat reaches the equivalent of
/// 15 human years in its first year, 24 by its second, and gains roughly
/// four human years per year after that. It is not perfectly linear inside
/// the first year, so we interpolate through a 6-month checkpoint too. See
/// docs/cat-age-research.md for sources — this is an approximation used for
/// general reference, not a precise or medical calculation.
/// </summary>
public static class CatAgeMath
{
    public static double CatYearsBetween(DateOnly birth, DateOnly on)
    {
        var days = on.DayNumber - birth.DayNumber;
        return days / 365.25;
    }

    public static double HumanEquivalentYears(double catYears)
    {
        if (catYears <= 0) return 0;
        if (catYears <= 0.5) return Lerp(catYears, 0, 0.5, 0, 10);
        if (catYears <= 1) return Lerp(catYears, 0.5, 1, 10, 15);
        if (catYears <= 2) return Lerp(catYears, 1, 2, 15, 24);
        return 24 + (catYears - 2) * 4;
    }

    private static double Lerp(double x, double x0, double x1, double y0, double y1)
        => y0 + (y1 - y0) * (x - x0) / (x1 - x0);

    public static LifeStageInfo GetLifeStage(double catYears, IReadOnlyList<LifeStageInfo> catalog)
    {
        foreach (var stage in catalog)
        {
            if (catYears >= stage.MinYears && (stage.MaxYears is null || catYears < stage.MaxYears))
            {
                return stage;
            }
        }

        return catalog[^1];
    }

    /// <summary>Formats a cat age in years as a friendly "X years, Y months" string.</summary>
    public static string FormatAge(double catYears)
    {
        if (catYears < 0) catYears = 0;

        var totalMonths = (int)Math.Round(catYears * 12);
        if (totalMonths == 0 && catYears > 0)
        {
            // Round-to-nearest can hit exactly 0 for a kitten under ~15
            // days old (e.g. 0.04yr * 12 = 0.48 -> rounds to 0), which
            // would otherwise print as "0 months". FormatRelative already
            // guards this same case with Math.Max(1, ...); mirror it here.
            totalMonths = 1;
        }

        var years = totalMonths / 12;
        var months = totalMonths % 12;

        if (years <= 0)
        {
            return months == 1 ? "1 month" : $"{months} months";
        }

        if (months == 0)
        {
            return years == 1 ? "1 year" : $"{years} years";
        }

        return $"{years} yr{(years == 1 ? "" : "s")}, {months} mo{(months == 1 ? "" : "s")}";
    }

    /// <summary>Formats a day offset as a friendly "3 years ago" / "in 8 months" string.</summary>
    public static string FormatRelative(int dayDiff)
    {
        if (dayDiff == 0) return "Today";

        var absDays = Math.Abs(dayDiff);
        var years = absDays / 365.25;

        string label;
        if (years >= 1)
        {
            var roundedYears = Math.Round(years, 1);
            label = $"{roundedYears} year{(roundedYears == 1 ? "" : "s")}";
        }
        else
        {
            var months = Math.Max(1, (int)Math.Round(absDays / 30.44));
            label = $"{months} month{(months == 1 ? "" : "s")}";
        }

        return dayDiff < 0 ? $"{label} ago" : $"in {label}";
    }
}
