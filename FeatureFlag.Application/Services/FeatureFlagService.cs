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

    public async Task<Flag> GetFlagAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        return await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");
    }

    public async Task<bool> IsEnabledAsync(
        string flagName,
        FeatureEvaluationContext context,
        CancellationToken ct = default
    )
    {
        var flag = await _repository.GetByNameAsync(flagName, context.Environment, ct);

        if (flag is null)
            throw new KeyNotFoundException(
                $"Flag '{flagName}' not found in {context.Environment}."
            );

        if (!flag.IsEnabled)
            return false;

        return _evaluator.Evaluate(flag, context);
    }

    public async Task<IReadOnlyList<Flag>> GetAllFlagsAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        return await _repository.GetAllAsync(environment, ct);
    }

    public async Task<Flag> CreateFlagAsync(Flag flag, CancellationToken ct = default)
    {
        await _repository.AddAsync(flag, ct);
        await _repository.SaveChangesAsync(ct);
        return flag;
    }

    public async Task UpdateFlagAsync(
        string name,
        EnvironmentType environment,
        bool isEnabled,
        RolloutStrategy strategyType,
        string strategyConfig,
        CancellationToken ct = default
    )
    {
        var flag =
            await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        // Single atomic update — sets UpdatedAt exactly once
        flag.Update(isEnabled, strategyType, strategyConfig);
        await _repository.SaveChangesAsync(ct);
    }

    public async Task ArchiveFlagAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        var flag =
            await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        flag.Archive();
        await _repository.SaveChangesAsync(ct);
    }
}
