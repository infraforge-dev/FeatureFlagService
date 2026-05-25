using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.ValueObjects;
using Banderas.Tests.Helpers;
using FluentAssertions;

namespace Banderas.Tests.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class StrategyConfigTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WithValidInputs_SetsProperties()
    {
        // Arrange & Act
        var config = StrategyConfig.Create(RolloutStrategy.Percentage, """{"percentage":50}""");

        // Assert
        config.ValidatedFor.Should().Be(RolloutStrategy.Percentage);
        config.RawJson.Should().Be("""{"percentage":50}""");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InternalConstructor_WithNullRawJson_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new StrategyConfig(RolloutStrategy.None, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WithNoneStrategy_SetsValidatedForToNone()
    {
        // Arrange & Act
        var config = StrategyConfig.Create(RolloutStrategy.None, "{}");

        // Assert
        config.ValidatedFor.Should().Be(RolloutStrategy.None);
        config.RawJson.Should().Be("{}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TwoConfigs_WithSameValues_AreEqual()
    {
        // Arrange
        var config1 = StrategyConfig.Create(RolloutStrategy.Percentage, """{"percentage":50}""");
        var config2 = StrategyConfig.Create(RolloutStrategy.Percentage, """{"percentage":50}""");

        // Assert
        config1.Should().Be(config2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TwoConfigs_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var config1 = StrategyConfig.Create(RolloutStrategy.Percentage, """{"percentage":50}""");
        var config2 = StrategyConfig.Create(RolloutStrategy.Percentage, """{"percentage":75}""");

        // Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FlagConstructor_WithMismatchedConfig_ThrowsFlagDomainException()
    {
        // Arrange — config validated for Percentage, but Flag strategy is RoleBased
        var config = new StrategyConfig(RolloutStrategy.Percentage, """{"percentage":50}""");

        // Act
        Action act = () =>
            new Flag(
                "test",
                EnvironmentType.Development,
                true,
                RolloutStrategy.RoleBased,
                config,
                FlagBuilder.DefaultVariations()
            );

        // Assert
        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FlagReconfigure_WithMismatchedConfig_ThrowsFlagDomainException()
    {
        // Arrange
        Flag flag = FlagBuilder.Build();
        var config = new StrategyConfig(RolloutStrategy.Percentage, """{"percentage":50}""");

        // Act
        Action act = () => flag.Reconfigure(true, RolloutStrategy.RoleBased, config);

        // Assert
        act.Should().Throw<FlagDomainException>();
    }
}
