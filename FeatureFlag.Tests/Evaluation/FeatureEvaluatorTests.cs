using FeatureFlag.Application.Evaluation;
using FeatureFlag.Application.Strategies;
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Interfaces;
using FeatureFlag.Domain.ValueObjects;
using FeatureFlag.Tests.Helpers;
using FluentAssertions;

namespace FeatureFlag.Tests.Evaluation;

[Trait("Category", "Unit")]
public sealed class FeatureEvaluatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyIsNone_ReturnsTrue()
    {
        // Arrange
        var evaluator = new FeatureEvaluator(
            new IRolloutStrategy[]
            {
                new NoneStrategy(),
                new PercentageStrategy(),
                new RoleStrategy(),
            }
        );
        Flag flag = FlagBuilder.Build(strategy: RolloutStrategy.None, strategyConfig: null);
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = evaluator.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyIsPercentage_DelegatesToPercentageStrategy()
    {
        // Arrange
        // Percentage set to 100 guarantees true for any userId without needing
        // to know the specific bucket.
        var evaluator = new FeatureEvaluator(
            new IRolloutStrategy[]
            {
                new NoneStrategy(),
                new PercentageStrategy(),
                new RoleStrategy(),
            }
        );
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 100}"""
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = evaluator.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyIsRoleBased_DelegatesToRoleStrategy()
    {
        // Arrange
        var evaluator = new FeatureEvaluator(
            new IRolloutStrategy[]
            {
                new NoneStrategy(),
                new PercentageStrategy(),
                new RoleStrategy(),
            }
        );
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin"]}"""
        );
        var context = new FeatureEvaluationContext(
            "user-1",
            ["Admin"],
            EnvironmentType.Development
        );

        // Act
        bool result = evaluator.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyNotRegistered_ReturnsFalse()
    {
        // Arrange
        // Only NoneStrategy is registered; a Percentage flag is not in the registry.
        // An unknown strategy type must never accidentally grant access — fail-closed contract.
        var evaluator = new FeatureEvaluator(new IRolloutStrategy[] { new NoneStrategy() });
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 100}"""
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = evaluator.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }
}
