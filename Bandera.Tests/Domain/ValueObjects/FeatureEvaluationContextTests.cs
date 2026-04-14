using Bandera.Domain.Enums;
using Bandera.Domain.ValueObjects;
using FluentAssertions;

namespace Bandera.Tests.Domain.ValueObjects;

[Trait("Category", "Unit")]
public class FeatureEvaluationContextTests
{
    private readonly List<string> _defaultRoles = ["admin", "user"];

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_ShouldThrow_WhenUserIdIsInvalid(string? userId)
    {
        // Act
        Action act = () =>
            new FeatureEvaluationContext(userId!, _defaultRoles, EnvironmentType.Production);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEnvironmentIsNone()
    {
        // Act
        Action act = () =>
            new FeatureEvaluationContext("user123", _defaultRoles, EnvironmentType.None);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*environment*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEnvironmentIsNotDefined()
    {
        // Arrange
        var invalidEnvironment = (EnvironmentType)999;

        // Act
        Action act = () =>
            new FeatureEvaluationContext("user123", _defaultRoles, invalidEnvironment);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*environment*");
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForIdenticalContexts()
    {
        // Arrange
        var context1 = new FeatureEvaluationContext(
            "user123",
            _defaultRoles,
            EnvironmentType.Production
        );
        var context2 = new FeatureEvaluationContext(
            "user123",
            _defaultRoles,
            EnvironmentType.Production
        );

        // Act
        bool areEqual = context1.Equals(context2);

        // Assert
        areEqual.Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_ForDifferentContexts()
    {
        // Arrange
        var context1 = new FeatureEvaluationContext(
            "user123",
            _defaultRoles,
            EnvironmentType.Production
        );
        var context2 = new FeatureEvaluationContext(
            "user456",
            _defaultRoles,
            EnvironmentType.Production
        );

        // Act
        bool areEqual = context1.Equals(context2);

        // Assert
        areEqual.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForIdenticalContexts()
    {
        // Arrange
        var context1 = new FeatureEvaluationContext(
            "user123",
            _defaultRoles,
            EnvironmentType.Production
        );
        var context2 = new FeatureEvaluationContext(
            "user123",
            _defaultRoles,
            EnvironmentType.Production
        );

        // Act
        int hashCode1 = context1.GetHashCode();
        int hashCode2 = context2.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }
}
