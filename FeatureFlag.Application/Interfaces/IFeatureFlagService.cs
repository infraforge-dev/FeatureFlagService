using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Application.Interfaces;

public interface IFeatureFlagService
{
    Flag GetFlag(string name, EnvironmentType environment);
    bool IsEnabled(string flagName, FeatureEvaluationContext context);
}
