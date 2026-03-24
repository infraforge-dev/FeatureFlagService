using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Domain.Interfaces;

public interface IFeatureFlagRepository
{
    Flag? GetByName(string name, EnvironmentType environment);
    IReadOnlyList<Flag> GetAll(EnvironmentType environment);
}
