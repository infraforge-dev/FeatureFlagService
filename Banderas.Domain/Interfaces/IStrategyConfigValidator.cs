using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;

namespace Banderas.Domain.Interfaces;

public interface IStrategyConfigValidator
{
    RolloutStrategy StrategyType { get; }
    StrategyConfig Validate(string? rawJson);
}
