using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Application.DTOs;

/// <summary>
/// Payload for creating a new feature flag.
/// </summary>
/// <param name="Name">The unique name of the feature flag. Alphanumeric, hyphens, and underscores only.</param>
/// <param name="Environment">The deployment environment this flag applies to. Cannot be None.</param>
/// <param name="IsEnabled">Whether the flag is active. Inactive flags always evaluate to false.</param>
/// <param name="StrategyType">The rollout strategy used to evaluate this flag.</param>
/// <param name="StrategyConfig">
/// JSON configuration for the selected strategy. Required when StrategyType is
/// Percentage or RoleBased. Must be a valid JSON object. Maximum 2000 characters.
/// </param>
public sealed record CreateFlagRequest(
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string? StrategyConfig
);
