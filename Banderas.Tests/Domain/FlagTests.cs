using Banderas.Domain.Entities;
using Banderas.Domain.Enums;

namespace Banderas.Tests.Domain;

[Trait("Category", "Unit")]
public sealed class FlagTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithoutIsSeededParameter_DefaultsToFalse()
    {
        var flag = new Flag(
            "dark-mode",
            EnvironmentType.Development,
            isEnabled: true,
            RolloutStrategy.None,
            strategyConfig: null
        );

        Assert.False(flag.IsSeeded);
        Assert.Equal("{}", flag.StrategyConfig);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithIsSeededParameter_SetsSeededState()
    {
        var flag = new Flag(
            "dark-mode",
            EnvironmentType.Development,
            isEnabled: true,
            RolloutStrategy.None,
            strategyConfig: "{}",
            isSeeded: true
        );

        Assert.True(flag.IsSeeded);
    }
}
