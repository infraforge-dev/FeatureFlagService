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
);
