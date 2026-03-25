using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Application.DTOs;

public sealed record UpdateFlagRequest(
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string StrategyConfig
);
