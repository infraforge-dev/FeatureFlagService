using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Strategies;

public sealed class NoneStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.None;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context) => true;
}
