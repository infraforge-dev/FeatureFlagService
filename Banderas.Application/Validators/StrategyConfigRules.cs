using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;

namespace Banderas.Application.Validators;

/// <summary>
/// Shared strategy config validation rules. Called by both
/// CreateFlagRequestValidator and UpdateFlagRequestValidator.
/// Delegates to StrategyConfigFactory for structural validation.
/// </summary>
internal sealed class StrategyConfigRules
{
    private readonly StrategyConfigFactory _factory;

    internal StrategyConfigRules(StrategyConfigFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Attempts to create a validated StrategyConfig via the factory.
    /// Returns true if validation passes, false otherwise.
    /// Used inside FluentValidation Must() lambdas.
    /// </summary>
    internal bool BeValidStrategyConfig(RolloutStrategy strategyType, string? config)
    {
        try
        {
            _factory.Create(strategyType, config);
            return true;
        }
        catch (BanderasValidationException)
        {
            return false;
        }
    }
}
