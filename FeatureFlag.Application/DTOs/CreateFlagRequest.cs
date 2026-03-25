using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Application.DTOs;

public sealed record CreateFlagRequest(
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string StrategyConfig
);
