using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Application.DTOs;

public sealed record FlagResponse(
    Guid Id,
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    bool IsArchived,
    RolloutStrategy StrategyType,
    string StrategyConfig,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
