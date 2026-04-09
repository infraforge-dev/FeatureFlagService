using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Application.Evaluation;

/// <summary>
/// Machine-readable reason for a flag evaluation outcome.
/// Carried on every EvaluationResult subtype for structured log querying
/// and as the foundation for the Phase 4 trace endpoint.
/// </summary>
public enum EvaluationReason
{
    FlagDisabled,
    StrategyEvaluated,
}

/// <summary>
/// Discriminated union representing the outcome of a feature flag evaluation.
/// Each subtype carries only the data relevant to its specific outcome.
/// Reason is defined on the base so every log entry carries a queryable field
/// regardless of which branch fired.
/// </summary>
public abstract record EvaluationResult(
    string FlagName,
    EnvironmentType Environment,
    string UserId,
    EvaluationReason Reason
);

/// <summary>
/// The flag exists but IsEnabled is false. Strategy was never consulted.
/// </summary>
public sealed record FlagDisabled(string FlagName, EnvironmentType Environment, string UserId)
    : EvaluationResult(FlagName, Environment, UserId, EvaluationReason.FlagDisabled);

/// <summary>
/// The flag is enabled and a rollout strategy produced the final decision.
/// </summary>
public sealed record StrategyEvaluated(
    string FlagName,
    EnvironmentType Environment,
    string UserId,
    bool IsEnabled,
    RolloutStrategy StrategyType
) : EvaluationResult(FlagName, Environment, UserId, EvaluationReason.StrategyEvaluated);
