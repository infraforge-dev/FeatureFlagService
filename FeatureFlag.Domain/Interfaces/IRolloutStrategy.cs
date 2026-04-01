using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Domain.Interfaces;

public interface IRolloutStrategy
{
    RolloutStrategy StrategyType { get; }
    bool Evaluate(Flag flag, FeatureEvaluationContext context);
}
