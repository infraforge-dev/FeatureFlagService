using Banderas.Application.Validators;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using FluentAssertions;

namespace Banderas.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class StrategyConfigFactoryTests
{
    private readonly StrategyConfigFactory _factory = new([
        new NoneConfigValidator(),
        new PercentageConfigValidator(),
        new RoleBasedConfigValidator(),
    ]);

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_Percentage_WithValidConfig_ReturnsStrategyConfig()
    {
        var config = _factory.Create(RolloutStrategy.Percentage, """{"percentage":50}""");

        config.ValidatedFor.Should().Be(RolloutStrategy.Percentage);
        config.RawJson.Should().Be("""{"percentage":50}""");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_RoleBased_WithValidConfig_ReturnsStrategyConfig()
    {
        var config = _factory.Create(RolloutStrategy.RoleBased, """{"roles":["Admin"]}""");

        config.ValidatedFor.Should().Be(RolloutStrategy.RoleBased);
        config.RawJson.Should().Be("""{"roles":["Admin"]}""");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_None_WithNullConfig_ReturnsEmptyJsonConfig()
    {
        var config = _factory.Create(RolloutStrategy.None, null);

        config.ValidatedFor.Should().Be(RolloutStrategy.None);
        config.RawJson.Should().Be("{}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_Percentage_WithRolesConfig_ThrowsBanderasValidationException()
    {
        Action act = () => _factory.Create(RolloutStrategy.Percentage, """{"roles":["Admin"]}""");

        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_RoleBased_WithPercentageConfig_ThrowsBanderasValidationException()
    {
        Action act = () => _factory.Create(RolloutStrategy.RoleBased, """{"percentage":50}""");

        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_None_WithNonEmptyConfig_ThrowsBanderasValidationException()
    {
        Action act = () => _factory.Create(RolloutStrategy.None, """{"percentage":50}""");

        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_UnregisteredStrategy_ThrowsInvalidOperationException()
    {
        // A factory with no validators registered
        var emptyFactory = new StrategyConfigFactory([]);

        Action act = () => emptyFactory.Create(RolloutStrategy.Percentage, """{"percentage":50}""");

        act.Should().Throw<InvalidOperationException>();
    }
}
