using Banderas.Domain.Enums;

namespace Banderas.Application.DTOs;

/// <summary>
/// Payload for updating an existing feature flag's enabled state and rollout strategy.
/// </summary>
/// <param name="IsEnabled">Whether the flag should be active after this update.</param>
/// <param name="StrategyType">The rollout strategy to apply.</param>
/// <param name="StrategyConfig">
/// JSON configuration for the selected strategy. Required when StrategyType is
/// Percentage or RoleBased. Maximum 2000 characters.
/// </param>
/// <param name="Description">
/// Updated description. Null leaves the existing value unchanged.
/// Empty string removes the description. Maximum 500 characters.
/// </param>
/// <param name="Tags">
/// New tag set. Null = no change. Empty list = clear all tags. Otherwise replaces
/// the existing tags wholesale after normalization (trim, lowercase, dedupe).
/// </param>
public sealed record UpdateFlagRequest(
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string? StrategyConfig,
    string? Description = null,
    IReadOnlyList<string>? Tags = null
);
