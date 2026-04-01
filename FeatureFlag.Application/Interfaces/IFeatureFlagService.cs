using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Application.Interfaces;

public interface IFeatureFlagService
{
    Task<Flag> GetFlagAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task<bool> IsEnabledAsync(
        string flagName,
        FeatureEvaluationContext context,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<Flag>> GetAllFlagsAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task<Flag> CreateFlagAsync(Flag flag, CancellationToken ct = default);
    Task UpdateFlagAsync(
        string name,
        EnvironmentType environment,
        bool isEnabled,
        RolloutStrategy strategyType,
        string strategyConfig,
        CancellationToken ct = default
    );
    Task ArchiveFlagAsync(string name, EnvironmentType environment, CancellationToken ct = default);
}
