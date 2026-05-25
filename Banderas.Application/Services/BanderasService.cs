using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Banderas.Application.AI;
using Banderas.Application.DTOs;
using Banderas.Application.Evaluation;
using Banderas.Application.Interfaces;
using Banderas.Application.Telemetry;
using Banderas.Application.Validation;
using Banderas.Application.Validators;
using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Banderas.Application.Services;

public sealed class BanderasService : IBanderasService
{
    private readonly IBanderasRepository _repository;
    private readonly FeatureEvaluator _evaluator;
    private readonly ILogger<BanderasService> _logger;
    private readonly ITelemetryService _telemetryService;
    private readonly IPromptSanitizer _promptSanitizer;
    private readonly IAiFlagAnalyzer _aiFlagAnalyzer;
    private readonly StrategyConfigFactory _configFactory;

    public BanderasService(
        IBanderasRepository repository,
        FeatureEvaluator evaluator,
        ILogger<BanderasService> logger,
        ITelemetryService telemetryService,
        IPromptSanitizer promptSanitizer,
        IAiFlagAnalyzer aiFlagAnalyzer,
        StrategyConfigFactory configFactory
    )
    {
        _repository = repository;
        _evaluator = evaluator;
        _logger = logger;
        _telemetryService = telemetryService;
        _promptSanitizer = promptSanitizer;
        _aiFlagAnalyzer = aiFlagAnalyzer;
        _configFactory = configFactory;
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
            _telemetryService.TrackEvaluation(
                flagName,
                false,
                RolloutStrategy.None,
                sanitizedContext.Environment
            );
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
        _telemetryService.TrackEvaluation(
            flagName,
            isEnabled,
            flag.StrategyType,
            sanitizedContext.Environment
        );
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

        StrategyConfig strategyConfig = _configFactory.Create(
            request.StrategyType,
            request.StrategyConfig
        );

        // Validator guarantees a non-null, non-empty, valid menu by the time we land here.
        IReadOnlyList<Variation> variations = request.Variations.Select(v => v.ToDomain()).ToList();

        var flag = new Flag(
            name,
            request.Environment,
            request.IsEnabled,
            request.StrategyType,
            strategyConfig,
            variations,
            SanitizeDescription(request.Description),
            NormalizeTags(request.Tags)
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

        // Atomic rollout reconfiguration — sets UpdatedAt exactly once
        StrategyConfig strategyConfig = _configFactory.Create(
            request.StrategyType,
            request.StrategyConfig
        );
        flag.Reconfigure(request.IsEnabled, request.StrategyType, strategyConfig);

        // Metadata mutation is a distinct concern (DD-6). Null on either field
        // means "no change" (DD-7); skip the call entirely if neither is touched.
        if (request.Description is not null || request.Tags is not null)
        {
            flag.UpdateMetadata(
                description: request.Description is not null
                    ? SanitizeDescription(request.Description)
                    : flag.Description,
                tags: request.Tags is not null ? NormalizeTags(request.Tags) : flag.Tags
            );
        }

        // Variations follow the same null-means-no-change semantics. Empty array
        // is rejected by the validator (cannot violate the non-empty invariant
        // here). Single SaveChangesAsync flushes Reconfigure + UpdateMetadata +
        // UpdateVariations together.
        if (request.Variations is not null)
        {
            IReadOnlyList<Variation> newVariations = request
                .Variations.Select(v => v.ToDomain())
                .ToList();
            flag.UpdateVariations(newVariations);
        }

        await _repository.SaveChangesAsync(ct);
    }

    private static string? SanitizeDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        // Empty after Clean (incl. whitespace-only input) collapses to null —
        // this is the "clear the description" signal on PUT (DD-7).
        string? cleaned = Validators.InputSanitizer.Clean(description);
        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return Validators
            .InputSanitizer.CleanCollection(tags)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();
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

    public async Task<FlagHealthAnalysisResponse> AnalyzeFlagsAsync(
        FlagHealthRequest request,
        CancellationToken cancellationToken = default
    )
    {
        int threshold =
            request.StalenessThresholdDays ?? FlagHealthConstants.DefaultStalenessThresholdDays;

        IReadOnlyList<Flag> flags = await _repository.GetAllAsync(ct: cancellationToken);
        List<FlagResponse> flagResponses = flags.Select(FlagMappings.ToResponse).ToList();

        // StrategyConfig and Description are string? — null guard required (AC-7).
        // Tags are short structured labels but still pass through the sanitizer
        // to defend against operator-authored prompt injection attempts.
        // Variations: Key and Value are operator-authored — both sanitized.
        // Kind is enum-derived (canonical name from VariationKind), not operator
        // input, so it is emitted verbatim through ToString().
        List<FlagResponse> sanitizedFlags = flagResponses
            .Select(f =>
                f with
                {
                    Name = _promptSanitizer.Sanitize(f.Name),
                    StrategyConfig = f.StrategyConfig is not null
                        ? _promptSanitizer.Sanitize(f.StrategyConfig)
                        : null,
                    Description = f.Description is not null
                        ? _promptSanitizer.Sanitize(f.Description)
                        : null,
                    Tags = f.Tags.Select(_promptSanitizer.Sanitize).ToList(),
                    Variations = f
                        .Variations.Select(v => new VariationResponse(
                            _promptSanitizer.Sanitize(v.Key),
                            v.Kind,
                            _promptSanitizer.Sanitize(v.Value)
                        ))
                        .ToList(),
                }
            )
            .ToList();

        return await _aiFlagAnalyzer.AnalyzeAsync(sanitizedFlags, threshold, cancellationToken);
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
