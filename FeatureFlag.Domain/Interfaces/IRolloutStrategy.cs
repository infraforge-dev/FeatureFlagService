using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Domain.Interfaces;

public interface IRolloutStrategy
{
    bool Evaluate(Flag flag, FeatureEvaluationContext context);
}
