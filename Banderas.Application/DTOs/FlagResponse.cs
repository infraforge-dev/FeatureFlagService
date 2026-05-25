using Banderas.Domain.Enums;

namespace Banderas.Application.DTOs;

/// <summary>
/// Represents a feature flag as returned by the API.
/// </summary>
/// <param name="Id">The unique identifier of the flag.</param>
/// <param name="Name">The unique name of the flag within its environment.</param>
/// <param name="Environment">The deployment environment this flag belongs to.</param>
/// <param name="IsEnabled">Whether the flag is currently active.</param>
/// <param name="IsArchived">Whether the flag has been archived (soft-deleted).</param>
/// <param name="StrategyType">The rollout strategy used to evaluate this flag.</param>
/// <param name="StrategyConfig">The raw JSON strategy configuration.</param>
/// <param name="CreatedAt">UTC timestamp when the flag was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
public sealed record FlagResponse(
    Guid Id,
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    bool IsArchived,
    RolloutStrategy StrategyType,
    string? StrategyConfig,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    /// <summary>Operator-authored description, or null if none.</summary>
    public string? Description { get; init; }

    /// <summary>Operator-authored organizational labels (normalized).</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Ordered, non-empty menu of variations this flag may produce.
    /// Always present and contains at least one entry on any successful response
    /// (enforced by the domain invariant + migration backfill).
    /// </summary>
    public IReadOnlyList<VariationResponse> Variations { get; init; } = [];
}
