using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Tests.Helpers;
using FluentAssertions;

namespace Banderas.Tests.Domain;

[Trait("Category", "Unit")]
public sealed class FlagArchivedInvariantTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SetEnabled_WhenFlagIsArchived_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        Action act = () => flag.SetEnabled(true);

        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateStrategy_WhenFlagIsArchived_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        Action act = () => flag.UpdateStrategy(RolloutStrategy.None, null);

        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateName_WhenFlagIsArchived_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        Action act = () => flag.UpdateName("new-name");

        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Update_WhenFlagIsArchived_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        Action act = () => flag.Update(false, RolloutStrategy.None, null);

        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Archive_WhenFlagIsAlreadyArchived_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        Action act = () => flag.Archive();

        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SetEnabled_WhenFlagIsNotArchived_Succeeds()
    {
        Flag flag = FlagBuilder.Build(isEnabled: false);

        flag.SetEnabled(true);

        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateStrategy_WhenFlagIsNotArchived_Succeeds()
    {
        Flag flag = FlagBuilder.Build();

        flag.UpdateStrategy(RolloutStrategy.Percentage, "{\"percentage\":50}");

        flag.StrategyType.Should().Be(RolloutStrategy.Percentage);
        flag.StrategyConfig.Should().Be("{\"percentage\":50}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateName_WhenFlagIsNotArchived_Succeeds()
    {
        Flag flag = FlagBuilder.Build();

        flag.UpdateName("renamed-flag");

        flag.Name.Should().Be("renamed-flag");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Update_WhenFlagIsNotArchived_Succeeds()
    {
        Flag flag = FlagBuilder.Build(isEnabled: true);

        flag.Update(false, RolloutStrategy.Percentage, "{\"percentage\":25}");

        flag.IsEnabled.Should().BeFalse();
        flag.StrategyType.Should().Be(RolloutStrategy.Percentage);
        flag.StrategyConfig.Should().Be("{\"percentage\":25}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Archive_WhenFlagIsNotArchived_Succeeds()
    {
        Flag flag = FlagBuilder.Build();

        flag.Archive();

        flag.IsArchived.Should().BeTrue();
        flag.ArchivedAt.Should().NotBeNull();
    }
}
