namespace HenryTracker.Models;

/// <summary>
/// A reference entry describing one feline life stage: an age band, and
/// what's developmentally/medically notable about it. See
/// docs/cat-age-research.md for sourcing.
/// </summary>
public class LifeStageInfo
{
    public required string Name { get; init; }
    public required double MinYears { get; init; }
    /// <summary>Null means open-ended (the final stage).</summary>
    public double? MaxYears { get; init; }
    public required string RangeLabel { get; init; }
    public required string Summary { get; init; }
    public required string Description { get; init; }
}
