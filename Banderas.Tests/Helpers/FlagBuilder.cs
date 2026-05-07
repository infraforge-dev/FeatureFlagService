using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;

namespace Banderas.Tests.Helpers;

internal static class FlagBuilder
{
    internal static Flag Build(
        string name = "test-flag",
        EnvironmentType environment = EnvironmentType.Development,
        bool isEnabled = true,
        RolloutStrategy strategy = RolloutStrategy.None,
        string? strategyConfig = null
    )
    {
        var config = new StrategyConfig(strategy, strategyConfig ?? "{}");
        return new Flag(name, environment, isEnabled, strategy, config);
    }
}
