using Banderas.Application.DTOs;
using Banderas.Application.Validators;
using Banderas.Domain.Enums;
using FluentAssertions;
using FluentValidation.Results;

namespace Banderas.Tests.Validators;

[Trait("Category", "Unit")]
public sealed class CreateFlagRequestValidatorTests
{
    private readonly CreateFlagRequestValidator _validator;

    public CreateFlagRequestValidatorTests()
    {
        var factory = new StrategyConfigFactory([
            new NoneConfigValidator(),
            new PercentageConfigValidator(),
            new RoleBasedConfigValidator(),
        ]);
        _validator = new CreateFlagRequestValidator(factory);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameIsEmpty_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new CreateFlagRequest(
            "",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameIsWhitespaceOnly_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new CreateFlagRequest(
            "   ",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameExceedsMaxLength_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new CreateFlagRequest(
            new string('a', 101),
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

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
    public async Task Validate_WhenNameContainsInvalidCharacters_ReturnsInvalidAsync(string name)
    {
        // Arrange
        var request = new CreateFlagRequest(
            name,
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameHasPaddedWhitespace_ReturnsValidAsync()
    {
        // Arrange
        // The validator runs the regex on the cleaned value; the service layer
        // strips whitespace before storing. Padded input like " dark-mode " is accepted.
        var request = new CreateFlagRequest(
            " dark-mode ",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenNameUsesAllowedCharacters_ReturnsValidAsync()
    {
        // Arrange
        var request = new CreateFlagRequest(
            "my-flag_v2",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenEnvironmentIsNone_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.None,
            true,
            RolloutStrategy.None,
            null!
        );

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Environment");
    }

    [Theory]
    [InlineData(EnvironmentType.Development)]
    [InlineData(EnvironmentType.Staging)]
    [InlineData(EnvironmentType.Production)]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenEnvironmentIsValid_ReturnsValidAsync(EnvironmentType env)
    {
        // Arrange
        var request = new CreateFlagRequest("test-flag", env, true, RolloutStrategy.None, null!);

        // Act
        ValidationResult result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenStrategyIsNoneButConfigIsProvided_ReturnsInvalidAsync()
    {
        // Arrange
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
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
    public async Task Validate_WhenStrategyIsNoneAndConfigIsNull_ReturnsValidAsync()
    {
        // Arrange
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            null!
        );

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
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.Percentage,
            """{"percentage": 50}"""
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
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.Percentage,
            null!
        );

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
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
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
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.RoleBased,
            """{"roles": ["Admin", "Editor"]}"""
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
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.RoleBased,
            null!
        );

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
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
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
        // Use Percentage strategy so the config field is expected; the 2000-char
        // rule applies before the structure validation rule triggers.
        var request = new CreateFlagRequest(
            "test-flag",
            EnvironmentType.Development,
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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenDescriptionIsAt500Chars_ReturnsValidAsync()
    {
        CreateFlagRequest request = NewRequest() with { Description = new string('a', 500) };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenDescriptionExceeds500Chars_ReturnsInvalidAsync()
    {
        CreateFlagRequest request = NewRequest() with { Description = new string('a', 501) };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenDescriptionIsNull_ReturnsValidAsync()
    {
        CreateFlagRequest request = NewRequest() with { Description = null };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagsCountIs20_ReturnsValidAsync()
    {
        CreateFlagRequest request = NewRequest() with
        {
            Tags = Enumerable.Range(1, 20).Select(i => $"tag-{i}").ToList(),
        };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagsCountExceeds20_ReturnsInvalidAsync()
    {
        CreateFlagRequest request = NewRequest() with
        {
            Tags = Enumerable.Range(1, 21).Select(i => $"tag-{i}").ToList(),
        };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Tags");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagEntryExceeds50Chars_ReturnsInvalidAsync()
    {
        CreateFlagRequest request = NewRequest() with { Tags = [new string('a', 51)] };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Tags"));
    }

    [Theory]
    [InlineData("Bad Tag!")]
    [InlineData("space inside")]
    [InlineData("tag.with.dots")]
    [InlineData("tag/slash")]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagViolatesCharClass_ReturnsInvalidAsync(string tag)
    {
        CreateFlagRequest request = NewRequest() with { Tags = [tag] };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Tags"));
    }

    [Theory]
    [InlineData("checkout")]
    [InlineData("Checkout")]
    [InlineData(" checkout ")]
    [InlineData("CHECKOUT")]
    [InlineData("release-q2")]
    [InlineData("team_alpha")]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagIsNormalizationFriendly_ReturnsValidAsync(string tag)
    {
        CreateFlagRequest request = NewRequest() with { Tags = [tag] };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagsIsNull_ReturnsValidAsync()
    {
        CreateFlagRequest request = NewRequest() with { Tags = null };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Validate_WhenTagsIsEmpty_ReturnsValidAsync()
    {
        CreateFlagRequest request = NewRequest() with { Tags = [] };

        ValidationResult result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    private static CreateFlagRequest NewRequest() =>
        new("test-flag", EnvironmentType.Development, true, RolloutStrategy.None, null);
}
