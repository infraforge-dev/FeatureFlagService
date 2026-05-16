namespace Banderas.Application.DTOs;

/// <summary>
/// The result of an AI-generated health analysis across all active feature flags.
/// </summary>
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
