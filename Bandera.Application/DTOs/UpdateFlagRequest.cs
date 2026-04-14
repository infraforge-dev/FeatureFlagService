using Bandera.Domain.Enums;

namespace Bandera.Application.DTOs;

/// <summary>
/// Payload for updating an existing feature flag's enabled state and rollout strategy.
/// </summary>
/// <param name="IsEnabled">Whether the flag should be active after this update.</param>
/// <param name="StrategyType">The rollout strategy to apply.</param>
/// <param name="StrategyConfig">
/// JSON configuration for the selected strategy. Required when StrategyType is
/// Percentage or RoleBased. Maximum 2000 characters.
/// </param>
public sealed record UpdateFlagRequest(
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string? StrategyConfig
);
