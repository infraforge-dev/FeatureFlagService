using Banderas.Application.Validators;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.ValueObjects;
using FluentAssertions;

namespace Banderas.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class RoleBasedConfigValidatorTests
{
    private readonly RoleBasedConfigValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void StrategyType_ReturnsRoleBased()
    {
        _validator.StrategyType.Should().Be(RolloutStrategy.RoleBased);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithValidConfig_ReturnsStrategyConfig()
    {
        // Arrange & Act
        StrategyConfig config = _validator.Validate("""{"roles":["Admin","Editor"]}""");

        // Assert
        config.ValidatedFor.Should().Be(RolloutStrategy.RoleBased);
        config.RawJson.Should().Be("""{"roles":["Admin","Editor"]}""");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Validate_WithNullOrEmptyConfig_ThrowsBanderasValidationException(string? rawJson)
    {
        Action act = () => _validator.Validate(rawJson);

        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithNonJsonConfig_ThrowsBanderasValidationException()
    {
        Action act = () => _validator.Validate("not-json");

        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithMissingRolesField_ThrowsBanderasValidationException()
    {
        Action act = () => _validator.Validate("""{"percentage":50}""");

        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithEmptyRolesArray_ThrowsBanderasValidationException()
    {
        Action act = () => _validator.Validate("""{"roles":[]}""");

        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithRolesNotArray_ThrowsBanderasValidationException()
    {
        Action act = () => _validator.Validate("""{"roles":"Admin"}""");

        act.Should().Throw<BanderasValidationException>();
    }
}
