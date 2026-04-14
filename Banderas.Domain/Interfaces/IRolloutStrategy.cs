using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;

namespace Banderas.Domain.Interfaces;

public interface IRolloutStrategy
{
    RolloutStrategy StrategyType { get; }
    bool Evaluate(Flag flag, FeatureEvaluationContext context);
}
