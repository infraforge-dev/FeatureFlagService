using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.ValueObjects;

namespace Bandera.Domain.Interfaces;

public interface IRolloutStrategy
{
    RolloutStrategy StrategyType { get; }
    bool Evaluate(Flag flag, FeatureEvaluationContext context);
}
