using Bandera.Application.Strategies;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.ValueObjects;
using Bandera.Tests.Helpers;
using FluentAssertions;

namespace Bandera.Tests.Strategies;

[Trait("Category", "Unit")]
public sealed class PercentageStrategyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenStrategyConfigIsNull_ReturnsFalse()
    {
        // Arrange
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(strategy: RolloutStrategy.Percentage, strategyConfig: null);
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

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
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: string.Empty
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

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
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: "not-json"
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenPercentageFieldIsMissing_ReturnsFalse()
    {
        // Arrange
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"rollout": 50}"""
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenPercentageIsZero_ReturnsFalse()
    {
        // Arrange
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 0}"""
        );

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var context = new FeatureEvaluationContext(
                $"user-{i}",
                [],
                EnvironmentType.Development
            );
            bool result = strategy.Evaluate(flag, context);
            result.Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenPercentageIsOneHundred_ReturnsTrue()
    {
        // Arrange
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 100}"""
        );

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var context = new FeatureEvaluationContext(
                $"user-{i}",
                [],
                EnvironmentType.Development
            );
            bool result = strategy.Evaluate(flag, context);
            result.Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_CalledTwiceWithSameInput_ReturnsSameResult()
    {
        // Arrange
        // SHA-256 must produce the same bucket for the same userId:flagName input
        // on every call and every process restart — this is the determinism contract.
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 50}"""
        );
        var context = new FeatureEvaluationContext(
            "user-determinism",
            [],
            EnvironmentType.Development
        );

        // Act
        bool result1 = strategy.Evaluate(flag, context);
        bool result2 = strategy.Evaluate(flag, context);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenPercentageExceedsOneHundred_ReturnsFalse()
    {
        // Arrange
        var strategy = new PercentageStrategy();
        Flag flag = FlagBuilder.Build(
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 150}"""
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_SameUserDifferentFlagNames_MayProduceDifferentResults()
    {
        // Arrange
        // The flag name is included in the hash input ($"{userId}:{flagName}"),
        // ensuring independent bucketing per flag. Two flags at 50% may assign
        // the same user to different buckets. This test documents the design intent:
        // both calls must complete without throwing.
        var strategy = new PercentageStrategy();
        Flag flagA = FlagBuilder.Build(
            name: "flag-a",
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 50}"""
        );
        Flag flagB = FlagBuilder.Build(
            name: "flag-b",
            strategy: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 50}"""
        );
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool resultA = strategy.Evaluate(flagA, context);
        bool resultB = strategy.Evaluate(flagB, context);

        // Assert — neither call must throw; a bool always equals itself (tautology
        // intentional — the goal is to confirm both calls complete without an exception)
        resultA.Should().Be(resultA);
        resultB.Should().Be(resultB);
    }
}
