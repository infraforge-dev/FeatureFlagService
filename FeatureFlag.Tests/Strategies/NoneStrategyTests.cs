using FeatureFlag.Application.Strategies;
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.ValueObjects;
using FeatureFlag.Tests.Helpers;
using FluentAssertions;

namespace FeatureFlag.Tests.Strategies;

[Trait("Category", "Unit")]
public sealed class NoneStrategyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WithDefaultContext_ReturnsTrue()
    {
        // Arrange
        var strategy = new NoneStrategy();
        Flag flag = FlagBuilder.Build(strategy: RolloutStrategy.None);
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WithEmptyUserId_ReturnsTrue()
    {
        // Arrange
        var strategy = new NoneStrategy();
        Flag flag = FlagBuilder.Build(strategy: RolloutStrategy.None);
        var context = new FeatureEvaluationContext("anon", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WithNoRoles_ReturnsTrue()
    {
        // Arrange
        var strategy = new NoneStrategy();
        Flag flag = FlagBuilder.Build(strategy: RolloutStrategy.None);
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_WhenFlagIsDisabled_StillReturnsTrue()
    {
        // Arrange
        // NoneStrategy.Evaluate itself returns true regardless of flag.IsEnabled.
        // The IsEnabled check is the service layer's responsibility (KI-002),
        // not the strategy's.
        var strategy = new NoneStrategy();
        Flag flag = FlagBuilder.Build(strategy: RolloutStrategy.None, isEnabled: false);
        var context = new FeatureEvaluationContext("user-1", [], EnvironmentType.Development);

        // Act
        bool result = strategy.Evaluate(flag, context);

        // Assert
        result.Should().BeTrue();
    }
}
