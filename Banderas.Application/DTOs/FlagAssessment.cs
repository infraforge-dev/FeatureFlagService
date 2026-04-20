namespace Banderas.Application.DTOs;

public record FlagAssessment
{
    public required string Name { get; init; }

    /// <summary>One of: Healthy, Stale, Misconfigured, NeedsReview</summary>
    public required string Status { get; init; }

    /// <summary>Plain English explanation of why this status was assigned.</summary>
    public required string Reason { get; init; }

    /// <summary>Actionable recommendation for the developer.</summary>
    public required string Recommendation { get; init; }
}
