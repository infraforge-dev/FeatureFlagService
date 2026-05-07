using Banderas.Domain.Enums;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;

namespace Banderas.Application.Validators;

public sealed class StrategyConfigFactory
{
    private readonly Dictionary<RolloutStrategy, IStrategyConfigValidator> _validators;

    public StrategyConfigFactory(IEnumerable<IStrategyConfigValidator> validators)
    {
        _validators = validators.ToDictionary(v => v.StrategyType);
    }

    public StrategyConfig Create(RolloutStrategy strategy, string? rawJson)
    {
        if (!_validators.TryGetValue(strategy, out IStrategyConfigValidator? validator))
        {
            throw new InvalidOperationException(
                $"No IStrategyConfigValidator registered for strategy '{strategy}'."
            );
        }

        return validator.Validate(rawJson);
    }
}
