using FeatureFlag.Application.DTOs;
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

    public async Task<FlagResponse> GetFlagAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        var flag = await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        return flag.ToResponse();
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

    public async Task<IReadOnlyList<FlagResponse>> GetAllFlagsAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        var flags = await _repository.GetAllAsync(environment, ct);
        return flags.Select(f => f.ToResponse()).ToList();
    }

    public async Task<FlagResponse> CreateFlagAsync(
        CreateFlagRequest request,
        CancellationToken ct = default
    )
    {
        var flag = new Flag(
            request.Name,
            request.Environment,
            request.IsEnabled,
            request.StrategyType,
            request.StrategyConfig);

        await _repository.AddAsync(flag, ct);
        await _repository.SaveChangesAsync(ct);
        return flag.ToResponse();
    }

    public async Task UpdateFlagAsync(
        string name,
        EnvironmentType environment,
        UpdateFlagRequest request,
        CancellationToken ct = default
    )
    {
        var flag =
            await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        // Single atomic update — sets UpdatedAt exactly once
        flag.Update(request.IsEnabled, request.StrategyType, request.StrategyConfig);
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
