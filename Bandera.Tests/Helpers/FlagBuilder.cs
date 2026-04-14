using Bandera.Domain.Entities;
using Bandera.Domain.Enums;

namespace Bandera.Tests.Helpers;

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
        return new Flag(name, environment, isEnabled, strategy, strategyConfig!);
    }
}
