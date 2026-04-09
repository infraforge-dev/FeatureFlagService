using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Evaluation;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Application.Validation;
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Exceptions;
using FeatureFlag.Domain.Interfaces;
using FeatureFlag.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FeatureFlag.Application.Services;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _repository;
    private readonly FeatureEvaluator _evaluator;
    private readonly ILogger<FeatureFlagService> _logger;

    public FeatureFlagService(
        IFeatureFlagRepository repository,
        FeatureEvaluator evaluator,
        ILogger<FeatureFlagService> logger
    )
    {
        _repository = repository;
        _evaluator = evaluator;
        _logger = logger;
    }

    public async Task<FlagResponse> GetFlagAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        EnvironmentRules.RequireValid(environment);

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

        Flag? flag = await _repository.GetByNameAsync(flagName, sanitizedContext.Environment, ct);

        if (flag is null)
        {
            _logger.LogWarning(
                "Flag evaluation: not found. Flag={FlagName} Environment={Environment}",
                flagName,
                sanitizedContext.Environment
            );

            throw new FlagNotFoundException(flagName);
        }

        if (!flag.IsEnabled)
        {
            var result = new FlagDisabled(
                FlagName: flagName,
                Environment: sanitizedContext.Environment,
                UserId: sanitizedContext.UserId
            );

            LogResult(result);
            return false;
        }

        bool isEnabled = _evaluator.Evaluate(flag, sanitizedContext);

        var strategyResult = new StrategyEvaluated(
            FlagName: flagName,
            Environment: sanitizedContext.Environment,
            UserId: sanitizedContext.UserId,
            IsEnabled: isEnabled,
            StrategyType: flag.StrategyType
        );

        LogResult(strategyResult);
        return isEnabled;
    }

    public async Task<IReadOnlyList<FlagResponse>> GetAllFlagsAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        EnvironmentRules.RequireValid(environment);

        IReadOnlyList<Flag> flags = await _repository.GetAllAsync(environment, ct);
        return flags.Select(f => f.ToResponse()).ToList();
    }

    public async Task<FlagResponse> CreateFlagAsync(
        CreateFlagRequest request,
        CancellationToken ct = default
    )
    {
        EnvironmentRules.RequireValid(request.Environment);

        // NotEmpty in the validator guarantees non-null, non-whitespace — ! is safe here.
        string name = Validators.InputSanitizer.Clean(request.Name)!;

        if (await _repository.ExistsAsync(name, request.Environment, ct))
        {
            throw new DuplicateFlagNameException(name, request.Environment);
        }

        var flag = new Flag(
            name,
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
        EnvironmentRules.RequireValid(environment);

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
        EnvironmentRules.RequireValid(environment);

        Flag flag =
            await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new FlagNotFoundException(name);

        flag.Archive();
        await _repository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Writes a structured log entry for a completed evaluation outcome.
    /// UserId is hashed to a short SHA256 surrogate and never logged raw.
    /// Each branch logs only the fields meaningful to that outcome.
    /// </summary>
    private void LogResult(EvaluationResult result)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        switch (result)
        {
            case FlagDisabled d:
                _logger.LogInformation(
                    "Flag evaluation complete. Flag={FlagName} Environment={Environment} "
                        + "UserId={UserId} Reason={Reason}",
                    d.FlagName,
                    d.Environment,
                    HashUserId(d.UserId),
                    d.Reason
                );
                break;

            case StrategyEvaluated s:
                _logger.LogInformation(
                    "Flag evaluation complete. Flag={FlagName} Environment={Environment} "
                        + "UserId={UserId} Reason={Reason} Result={Result} Strategy={StrategyType}",
                    s.FlagName,
                    s.Environment,
                    HashUserId(s.UserId),
                    s.Reason,
                    s.IsEnabled ? "enabled" : "disabled",
                    s.StrategyType
                );
                break;

            default:
                throw new UnreachableException(
                    $"Unhandled EvaluationResult subtype: {result.GetType().Name}. "
                        + "Add a logging branch for every new EvaluationResult subtype."
                );
        }
    }

    /// <summary>
    /// Returns a short deterministic SHA256 fingerprint of the raw UserId.
    /// Deterministic output enables correlation without logging the original value.
    /// </summary>
    private static string HashUserId(string userId)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
}
