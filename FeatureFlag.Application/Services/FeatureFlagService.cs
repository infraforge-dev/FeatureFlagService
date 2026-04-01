using FeatureFlag.Application.Evaluation;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Interfaces;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Application.Services;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _repository;
    private readonly FeatureEvaluator _evaluator;

    public FeatureFlagService(IFeatureFlagRepository repository, FeatureEvaluator evaluator)
    {
        _repository = repository;
        _evaluator = evaluator;
    }

    public Flag GetFlag(string name, EnvironmentType environment)
    {
        return _repository.GetByName(name, environment)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");
    }

    public bool IsEnabled(string flagName, FeatureEvaluationContext context)
    {
        var flag = _repository.GetByName(flagName, context.Environment);

        if (flag is null || !flag.IsEnabled)
            return false;

        return _evaluator.Evaluate(flag, context);
    }
}
