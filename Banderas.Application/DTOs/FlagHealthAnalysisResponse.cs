namespace Banderas.Application.DTOs;

public record FlagHealthAnalysisResponse
{
    /// <summary>One-sentence natural language headline.</summary>
    public required string Summary { get; init; }

    /// <summary>Per-flag assessments. Includes all flags — healthy and unhealthy.</summary>
    public required List<FlagAssessment> Flags { get; init; }

    /// <summary>UTC timestamp of when the analysis was generated.</summary>
    public required DateTimeOffset AnalyzedAt { get; init; }

    /// <summary>Staleness threshold used for this analysis (days).</summary>
    public required int StalenessThresholdDays { get; init; }
}
