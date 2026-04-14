using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;

namespace Banderas.Application.Strategies;

public sealed class NoneStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.None;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context) => true;
}
