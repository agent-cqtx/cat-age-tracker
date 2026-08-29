using CatTracker.Models;

namespace CatTracker.Data;

/// <summary>
/// Reference data for feline life stages. Boundaries are adapted from the
/// widely-used six-stage veterinary breakdown (kitten/junior/prime/mature/
/// senior/geriatric) and smoothed slightly so the bands are contiguous with
/// no gaps or overlaps. See docs/cat-age-research.md for sources and the
/// human-year conversion math this pairs with. This is general reference
/// information, not veterinary advice for any individual cat.
/// </summary>
public static class LifeStageCatalog
{
    public static readonly IReadOnlyList<LifeStageInfo> Stages = new List<LifeStageInfo>
    {
        new()
        {
            Name = "Kitten",
            MinYears = 0,
            MaxYears = 0.5,
            RangeLabel = "0 – 6 months",
            Summary = "Rapid growth and first discoveries",
            Description = "Eyes open around two weeks old, baby teeth come in by three to four weeks, and " +
                          "weaning is usually complete by around eight weeks. This stage is a sprint: play is " +
                          "how kittens learn to hunt, socialize, and coordinate their bodies, and it's when " +
                          "first vaccines, deworming, and an early spay/neuter conversation typically happen."
        },
        new()
        {
            Name = "Junior",
            MinYears = 0.5,
            MaxYears = 2,
            RangeLabel = "6 months – 2 years",
            Summary = "The \"teenage\" stretch",
            Description = "By six months most cats have their full set of adult teeth and are reaching sexual " +
                          "maturity — cats grow up far faster than people do. Growth plates finish closing and " +
                          "adult size is usually reached by around a year, but the energy and boundary-testing " +
                          "of adolescence often carries on into year two."
        },
        new()
        {
            Name = "Prime",
            MinYears = 2,
            MaxYears = 6,
            RangeLabel = "2 – 6 years",
            Summary = "Peak condition, settled routines",
            Description = "This is a cat's physical prime: full-grown, athletic, and usually settled into a " +
                          "steady personality and daily rhythm. The main job here is maintenance — balanced " +
                          "nutrition, weight management, and keeping up with annual wellness visits before " +
                          "anything needs fixing."
        },
        new()
        {
            Name = "Mature",
            MinYears = 6,
            MaxYears = 10,
            RangeLabel = "6 – 10 years",
            Summary = "Cat middle-age",
            Description = "Roughly the feline equivalent of middle age. Metabolism and activity often start to " +
                          "shift, weight can creep up, and dental wear becomes more noticeable, so this is a " +
                          "good stretch to establish baseline bloodwork with a vet as a comparison point for " +
                          "later years."
        },
        new()
        {
            Name = "Senior",
            MinYears = 10,
            MaxYears = 14,
            RangeLabel = "10 – 14 years",
            Summary = "More monitoring, more comfort",
            Description = "Many vets recommend moving to twice-yearly checkups around this stage, with closer " +
                          "attention to kidneys, thyroid, and joints. Longer naps and a preference for warm, " +
                          "easy-to-reach spots are common — small environmental tweaks like ramps or low-sided " +
                          "litter boxes can help a lot."
        },
        new()
        {
            Name = "Geriatric",
            MinYears = 14,
            MaxYears = null,
            RangeLabel = "14+ years",
            Summary = "Comfort and quality of life first",
            Description = "In the golden years, care tends to center on comfort and quality of life. Conditions " +
                          "like kidney disease, arthritis, and hyperthyroidism become more common, so frequent " +
                          "vet monitoring and a calm, accessible home matter more than ever."
        },
    };
}
