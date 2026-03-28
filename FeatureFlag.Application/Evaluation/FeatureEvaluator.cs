using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Interfaces;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Application.Evaluation;

public sealed class FeatureEvaluator
{
    private readonly Dictionary<RolloutStrategy, IRolloutStrategy> _strategies;

    public FeatureEvaluator(IEnumerable<IRolloutStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.StrategyType);
    }

    /// <summary>
    /// Evaluates whether the given flag is enabled for the provided context.
    /// </summary>
    /// <remarks>
    /// PRECONDITION: Callers are responsible for checking <see cref="Flag.IsEnabled"/>
    /// before calling this method. The evaluator assumes the flag is active and will
    /// dispatch to the appropriate strategy regardless of enabled state.
    ///
    /// This contract is intentionally not enforced here via a guard clause — the
    /// evaluator is a pure strategy dispatcher, not a policy enforcer. Policy lives
    /// at the service boundary (<see cref="Services.FeatureFlagService"/>).
    ///
    /// If this method is called from any context other than FeatureFlagService,
    /// revisit whether the IsEnabled check needs to be added back here.
    /// </remarks>
    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        if (!_strategies.TryGetValue(flag.StrategyType, out var strategy))
            return false;

        return strategy.Evaluate(flag, context);
    }
}
