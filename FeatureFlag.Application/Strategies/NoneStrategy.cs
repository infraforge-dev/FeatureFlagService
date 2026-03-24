using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Interfaces;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Application.Strategies;

public sealed class NoneStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.None;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context) => true;
}
