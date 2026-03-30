using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Domain.Interfaces;

public interface IFeatureFlagRepository
{
    Task<Flag?> GetByNameAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<Flag>> GetAllAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task AddAsync(Flag flag, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
