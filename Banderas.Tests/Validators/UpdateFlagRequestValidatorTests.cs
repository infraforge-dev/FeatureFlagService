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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

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
        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenDescriptionIsNull_ReturnsValidAsync()
    {
        UpdateFlagRequest request = NewRequest() with { Description = null };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenDescriptionIsEmptyString_ReturnsValidAsync()
    {
        // Empty string is the "clear to null" signal; the service handles the
        // mapping. The validator only enforces the length ceiling.
        UpdateFlagRequest request = NewRequest() with
        {
            Description = "",
        };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenDescriptionExceeds500Chars_ReturnsInvalidAsync()
    {
        UpdateFlagRequest request = NewRequest() with { Description = new string('a', 501) };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagsIsNull_ReturnsValidAsync()
    {
        UpdateFlagRequest request = NewRequest() with { Tags = null };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagsIsEmpty_ReturnsValidAsync()
    {
        // Empty list is the "clear all" signal; the validator accepts it.
        UpdateFlagRequest request = NewRequest() with
        {
            Tags = [],
        };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagsCountExceeds20_ReturnsInvalidAsync()
    {
        UpdateFlagRequest request = NewRequest() with
        {
            Tags = Enumerable.Range(1, 21).Select(i => $"tag-{i}").ToList(),
        };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Tags");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagEntryExceeds50Chars_ReturnsInvalidAsync()
    {
        UpdateFlagRequest request = NewRequest() with { Tags = [new string('a', 51)] };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Tags"));
    }

    [Theory]
    [InlineData("Bad Tag!")]
    [InlineData("space inside")]
    [InlineData("tag.with.dots")]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagViolatesCharClass_ReturnsInvalidAsync(string tag)
    {
        UpdateFlagRequest request = NewRequest() with { Tags = [tag] };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Tags"));
    }

    [Theory]
    [InlineData("checkout")]
    [InlineData("Checkout")]
    [InlineData(" checkout ")]
    [InlineData("release-q2")]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagIsNormalizationFriendly_ReturnsValidAsync(string tag)
    {
        UpdateFlagRequest request = NewRequest() with { Tags = [tag] };

        ValidationResult result = await _validator.ValidateWithDefaultsAsync(request);

        result.IsValid.Should().BeTrue();
    }

    private static UpdateFlagRequest NewRequest() => new(true, RolloutStrategy.None, null);
}
