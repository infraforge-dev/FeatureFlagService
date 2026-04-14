using Bandera.Application.Strategies;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.ValueObjects;
using Bandera.Tests.Helpers;
using FluentAssertions;

namespace Bandera.Tests.Strategies;

[Trait("Category", "Unit")]
public sealed class RoleStrategyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyConfigIsNull_ReturnsFalse()
    {
        // Arrange
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(strategy: RolloutStrategy.RoleBased, strategyConfig: null);
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Admin"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyConfigIsEmpty_ReturnsFalse()
    {
        // Arrange
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: string.Empty
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Admin"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyConfigIsNotJson_ReturnsFalse()
    {
        // Arrange
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: "not-json"
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Admin"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenConfigRolesArrayIsEmpty_ReturnsFalse()
    {
        // Arrange
        // An empty allowlist blocks everyone — fail-closed behavior.
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": []}"""
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Admin"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenUserHasMatchingRole_ReturnsTrue()
    {
        // Arrange
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin", "Editor"]}"""
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Admin"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenUserHasNoMatchingRole_ReturnsFalse()
    {
        // Arrange
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin"]}"""
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Viewer"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenRoleCaseDiffers_ReturnsTrue()
    {
        // Arrange
        // Identity providers frequently mismatch casing between the stored config
        // and the token's role claims — case-insensitive matching is critical.
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin"]}"""
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["admin"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenUserHasOneOfManyRoles_ReturnsTrue()
    {
        // Arrange
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin", "Editor", "Reviewer"]}"""
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Reviewer"],
            EnvironmentType.Development
        );

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenUserHasNoRoles_ReturnsFalse()
    {
        // Arrange
        var strategy = new RoleStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin"]}"""
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }
}
