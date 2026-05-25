using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;

namespace Banderas.Tests.Helpers;

internal static class FlagBuilder
{
    /// <summary>
    /// Default variation menu — matches the migration backfill so any test that
    /// doesn't care about variations gets the same shape as legacy seeded flags.
    /// </summary>
    internal static IReadOnlyList<Variation> DefaultVariations() =>
        [new("off", VariationKind.Boolean, "false"), new("on", VariationKind.Boolean, "true")];

    internal static Flag Build(
        string name = "test-flag",
        EnvironmentType environment = EnvironmentType.Development,
        bool isEnabled = true,
        RolloutStrategy strategy = RolloutStrategy.None,
        string? strategyConfig = null,
        IReadOnlyList<Variation>? variations = null
    )
    {
        var config = new StrategyConfig(strategy, strategyConfig ?? "{}");
        return new Flag(
            name,
            environment,
            isEnabled,
            strategy,
            config,
            variations: variations ?? DefaultVariations()
        );
    }
}
