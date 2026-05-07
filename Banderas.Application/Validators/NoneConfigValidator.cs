using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;

namespace Banderas.Application.Validators;

public sealed class NoneConfigValidator : IStrategyConfigValidator
{
    public RolloutStrategy StrategyType => RolloutStrategy.None;

    public StrategyConfig Validate(string? rawJson)
    {
        if (!string.IsNullOrWhiteSpace(rawJson))
        {
            throw new BanderasValidationException(
                "StrategyConfig must be empty when StrategyType is None."
            );
        }

        return StrategyConfig.Create(RolloutStrategy.None, "{}");
    }
}
