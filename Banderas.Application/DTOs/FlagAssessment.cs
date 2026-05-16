namespace Banderas.Application.DTOs;

/// <summary>
/// Health assessment for a single feature flag.
/// </summary>
public record FlagAssessment
{
    /// <summary>The name of the assessed feature flag.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Health status assigned by the AI analysis.
    /// One of: <c>Healthy</c>, <c>Stale</c>, <c>Misconfigured</c>, <c>NeedsReview</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Plain-English explanation of why this status was assigned.</summary>
    public required string Reason { get; init; }

    /// <summary>Actionable recommendation for resolving the identified issue.</summary>
    public required string Recommendation { get; init; }
}
