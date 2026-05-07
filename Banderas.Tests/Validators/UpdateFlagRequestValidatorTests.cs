using Banderas.Application.DTOs;
using Banderas.Application.Validators;
using Banderas.Domain.Enums;
using FluentAssertions;
using FluentValidation.Results;

namespace Banderas.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class UpdateFlagRequestValidatorTests
{
    private readonly UpdateFlagRequestValidator _validator;

    public UpdateFlagRequestValidatorTests()
    {
        var factory = new StrategyConfigFactory([
            new NoneConfigValidator(),
            new PercentageConfigValidator(),
            new RoleBasedConfigValidator(),
        ]);
        _validator = new UpdateFlagRequestValidator(factory);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsNoneButConfigIsProvided_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(true, RolloutStrategy.None, """{"percentage": 50}""");

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsNoneAndConfigIsNull_ReturnsValidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(true, RolloutStrategy.None, null!);

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsPercentageWithValidConfig_ReturnsValidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(
            true,
            RolloutStrategy.Percentage,
            """{"percentage": 75}"""
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsPercentageWithNullConfig_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(true, RolloutStrategy.Percentage, null!);

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsPercentageWithInvalidConfig_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(
            true,
            RolloutStrategy.Percentage,
            """{"roles": ["Admin"]}"""
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsRoleBasedWithValidConfig_ReturnsValidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(
            true,
            RolloutStrategy.RoleBased,
            """{"roles": ["Admin"]}"""
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsRoleBasedWithNullConfig_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(true, RolloutStrategy.RoleBased, null!);

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsRoleBasedWithInvalidConfig_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(
            true,
            RolloutStrategy.RoleBased,
            """{"percentage": 50}"""
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyConfigExceedsMaxLength_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new UpdateFlagRequest(
            true,
            RolloutStrategy.Percentage,
            new string('x', 2001)
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }
}
