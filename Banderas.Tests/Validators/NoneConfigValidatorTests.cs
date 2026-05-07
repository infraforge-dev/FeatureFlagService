using Banderas.Application.Validators;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using FluentAssertions;

namespace Banderas.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class NoneConfigValidatorTests
{
    private readonly NoneConfigValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void StrategyType_ReturnsNone()
    {
        _validator.StrategyType.Should().Be(RolloutStrategy.None);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithNull_ReturnsEmptyJsonConfig()
    {
        // Arrange & Act
        var config = _validator.Validate(null);

        // Assert
        config.ValidatedFor.Should().Be(RolloutStrategy.None);
        config.RawJson.Should().Be("{}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithEmptyString_ReturnsEmptyJsonConfig()
    {
        // Arrange & Act
        var config = _validator.Validate("");

        // Assert
        config.ValidatedFor.Should().Be(RolloutStrategy.None);
        config.RawJson.Should().Be("{}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithWhitespace_ReturnsEmptyJsonConfig()
    {
        var config = _validator.Validate("   ");

        config.ValidatedFor.Should().Be(RolloutStrategy.None);
        config.RawJson.Should().Be("{}");
    }

    [Theory]
    [InlineData("""{"percentage":50}""")]
    [InlineData("""{"roles":["Admin"]}""")]
    [InlineData("some-value")]
    [Trait("Category", "Unit")]
    public void Validate_WithNonEmptyConfig_ThrowsBanderasValidationException(string rawJson)
    {
        Action act = () => _validator.Validate(rawJson);

        act.Should().Throw<BanderasValidationException>();
    }
}
