using Banderas.Domain.Enums;

namespace Banderas.Application.DTOs;

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
/// <param name="Description">
/// Optional human-readable description (≤500 characters). Empty or null = no description.
/// </param>
/// <param name="Tags">
/// Optional organizational labels. Each tag is normalized to lowercase, trimmed, and
/// deduplicated by the service before persistence. Maximum 20 entries, each ≤50 characters,
/// matching <c>^[a-z0-9\-_]+$</c> after normalization. Null = no tags.
/// </param>
public sealed record CreateFlagRequest(
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string? StrategyConfig,
    string? Description = null,
    IReadOnlyList<string>? Tags = null
);
