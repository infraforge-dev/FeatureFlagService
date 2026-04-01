using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Evaluation;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Exceptions;
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
        Flag flag =
            await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new FlagNotFoundException(name);

        return flag.ToResponse();
    }

    public async Task<bool> IsEnabledAsync(
        string flagName,
        FeatureEvaluationContext context,
        CancellationToken ct = default
    )
    {
        // Sanitize evaluation inputs. RuleFor lambdas in validators do not mutate the DTO.
        // UserId and UserRoles must be cleaned here to ensure consistent SHA256 hashing
        // in PercentageStrategy and HashSet lookups in RoleStrategy.
        var sanitizedContext = new FeatureEvaluationContext(
            userId: Validators.InputSanitizer.Clean(context.UserId) ?? context.UserId,
            userRoles: Validators.InputSanitizer.CleanCollection(context.UserRoles),
            environment: context.Environment
        );

        Flag flag =
            await _repository.GetByNameAsync(flagName, sanitizedContext.Environment, ct)
            ?? throw new FlagNotFoundException(flagName);

        if (!flag.IsEnabled)
        {
            return false;
        }

        return _evaluator.Evaluate(flag, sanitizedContext);
    }

    public async Task<IReadOnlyList<FlagResponse>> GetAllFlagsAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        IReadOnlyList<Flag> flags = await _repository.GetAllAsync(environment, ct);
        return flags.Select(f => f.ToResponse()).ToList();
    }

    public async Task<FlagResponse> CreateFlagAsync(
        CreateFlagRequest request,
        CancellationToken ct = default
    )
    {
        // Sanitize Name so the stored value matches the validated form.
        string sanitizedName = Validators.InputSanitizer.Clean(request.Name) ?? request.Name;

        var flag = new Flag(
            sanitizedName,
            request.Environment,
            request.IsEnabled,
            request.StrategyType,
            request.StrategyConfig
        );

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
        Flag flag =
            await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new FlagNotFoundException(name);

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
        Flag flag =
            await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new FlagNotFoundException(name);

        flag.Archive();
        await _repository.SaveChangesAsync(ct);
    }
}
