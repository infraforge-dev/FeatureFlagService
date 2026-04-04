using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Validators;
using FeatureFlag.Domain.Enums;
using FluentAssertions;
using FluentValidation.Results;

namespace FeatureFlag.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class CreateFlagRequestValidatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameIsEmpty_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameIsWhitespaceOnly_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "   ",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameExceedsMaxLength_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            new string('a', 101),
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData("my flag")]
    [InlineData("flag!")]
    [InlineData("flag.name")]
    [InlineData("flag/name")]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameContainsInvalidCharacters_ReturnsInvalid(string name)
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            name,
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameHasPaddedWhitespace_ReturnsValid()
    {
        // Arrange
        // The validator runs the regex on the cleaned value; the service layer
        // strips whitespace before storing. Padded input like " dark-mode " is accepted.
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            " dark-mode ",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameUsesAllowedCharacters_ReturnsValid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "my-flag_v2",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenEnvironmentIsNone_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.None,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Environment");
    }

    [Theory]
    [InlineData(EnvironmentType.Development)]
    [InlineData(EnvironmentType.Staging)]
    [InlineData(EnvironmentType.Production)]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenEnvironmentIsValid_ReturnsValid(EnvironmentType env)
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest("test-flag", env, true, RolloutStrategy.None, null!);

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsNoneButConfigIsProvided_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            """{"percentage": 50}"""
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsNoneAndConfigIsNull_ReturnsValid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsPercentageWithValidConfig_ReturnsValid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.Percentage,
            """{"percentage": 50}"""
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsPercentageWithNullConfig_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.Percentage,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsPercentageWithInvalidConfig_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.Percentage,
            """{"roles": ["Admin"]}"""
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsRoleBasedWithValidConfig_ReturnsValid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.RoleBased,
            """{"roles": ["Admin", "Editor"]}"""
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsRoleBasedWithNullConfig_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.RoleBased,
            null!
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsRoleBasedWithInvalidConfig_ReturnsInvalid()
    {
        // Arrange
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.RoleBased,
            """{"percentage": 50}"""
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyConfigExceedsMaxLength_ReturnsInvalid()
    {
        // Arrange
        // Use Percentage strategy so the config field is expected; the 2000-char
        // rule applies before the structure validation rule triggers.
        var validator = new CreateFlagRequestValidator();
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.Percentage,
            new string('x', 2001)
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }
}
