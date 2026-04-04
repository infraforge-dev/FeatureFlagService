using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Validators;
using FeatureFlag.Domain.Enums;
using FluentAssertions;
using FluentValidation.Results;

namespace FeatureFlag.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class EvaluationRequestValidatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenFlagNameIsEmpty_ReturnsInvalid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest("", "user-1", [], EnvironmentType.Development);

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FlagName");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenFlagNameExceedsMaxLength_ReturnsInvalid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest(
            new string('a', 101),
            "user-1",
            [],
            EnvironmentType.Development
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FlagName");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenUserIdIsEmpty_ReturnsInvalid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest("dark-mode", "", [], EnvironmentType.Development);

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenUserIdExceedsMaxLength_ReturnsInvalid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest(
            "dark-mode",
            new string('a', 257),
            [],
            EnvironmentType.Development
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenEnvironmentIsNone_ReturnsInvalid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest("dark-mode", "user-1", [], EnvironmentType.None);

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Environment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenUserRolesIsNull_ReturnsInvalid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest(
            "dark-mode",
            "user-1",
            null!,
            EnvironmentType.Development
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserRoles");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenUserRolesIsEmpty_ReturnsValid()
    {
        // Arrange
        // An empty roles array is a legitimate state — the user is authenticated
        // but has no roles. This is different from null, which signals a missing payload.
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest("dark-mode", "user-1", [], EnvironmentType.Development);

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenUserRolesExceedsMaxCount_ReturnsInvalid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest(
            "dark-mode",
            "user-1",
            Enumerable.Range(0, 51).Select(i => $"role-{i}").ToList(),
            EnvironmentType.Development
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserRoles");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenSingleRoleExceedsMaxLength_ReturnsInvalid()
    {
        // Arrange
        // FluentValidation's RuleForEach generates property names in the format
        // "UserRoles[0]", "UserRoles[1]", etc.
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest(
            "dark-mode",
            "user-1",
            [new string('a', 101)],
            EnvironmentType.Development
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserRoles[0]");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenAllFieldsAreValid_ReturnsValid()
    {
        // Arrange
        var validator = new EvaluationRequestValidator();
        var request = new EvaluationRequest(
            "dark-mode",
            "user-42",
            ["Admin", "Editor"],
            EnvironmentType.Production
        );

        // Act
        ValidationResult result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
