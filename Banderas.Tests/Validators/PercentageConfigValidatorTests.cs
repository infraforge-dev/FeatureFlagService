using Banderas.Application.Validators;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using FluentAssertions;

namespace Banderas.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class PercentageConfigValidatorTests
{
    private readonly PercentageConfigValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void StrategyType_ReturnsPercentage()
    {
        _validator.StrategyType.Should().Be(RolloutStrategy.Percentage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithValidConfig_ReturnsStrategyConfig()
    {
        // Arrange & Act
        var config = _validator.Validate("""{"percentage":50}""");

        // Assert
        config.ValidatedFor.Should().Be(RolloutStrategy.Percentage);
        config.RawJson.Should().Be("""{"percentage":50}""");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Validate_WithNullOrEmptyConfig_ThrowsBanderasValidationException(string? rawJson)
    {
        // Arrange & Act
        Action act = () => _validator.Validate(rawJson);

        // Assert
        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithNonJsonConfig_ThrowsBanderasValidationException()
    {
        // Arrange & Act
        Action act = () => _validator.Validate("not-json");

        // Assert
        act.Should().Throw<BanderasValidationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WithMissingPercentageField_ThrowsBanderasValidationException()
    {
        // Arrange & Act
        Action act = () => _validator.Validate("""{"rollout":50}""");

        // Assert
        act.Should().Throw<BanderasValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [Trait("Category", "Unit")]
    public void Validate_WithPercentageOutOfRange_ThrowsBanderasValidationException(int percentage)
    {
        // Arrange & Act
        Action act = () => _validator.Validate($$$"""{"percentage":{{{percentage}}}}""");

        // Assert
        act.Should().Throw<BanderasValidationException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [Trait("Category", "Unit")]
    public void Validate_WithPercentageAtBoundary_ReturnsStrategyConfig(int percentage)
    {
        // Arrange & Act
        var config = _validator.Validate($$$"""{"percentage":{{{percentage}}}}""");

        // Assert
        config.ValidatedFor.Should().Be(RolloutStrategy.Percentage);
    }
}
