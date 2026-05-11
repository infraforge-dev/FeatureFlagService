using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.ValueObjects;
using Banderas.Tests.Helpers;
using FluentAssertions;

namespace Banderas.Tests.Domain;

[Trait("Category", "Unit")]
public sealed class FlagArchivedInvariantTests
{
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
    public void Reconfigure_WhenFlagIsArchived_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        var config = new StrategyConfig(RolloutStrategy.None, "{}");
        Action act = () => flag.Reconfigure(false, RolloutStrategy.None, config);

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
    public void UpdateName_WhenFlagIsNotArchived_Succeeds()
    {
        Flag flag = FlagBuilder.Build();

        flag.UpdateName("renamed-flag");

        flag.Name.Should().Be("renamed-flag");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Reconfigure_WhenFlagIsNotArchived_Succeeds()
    {
        Flag flag = FlagBuilder.Build(isEnabled: true);
        var config = new StrategyConfig(RolloutStrategy.Percentage, """{"percentage":25}""");

        flag.Reconfigure(false, RolloutStrategy.Percentage, config);

        flag.IsEnabled.Should().BeFalse();
        flag.StrategyType.Should().Be(RolloutStrategy.Percentage);
        flag.StrategyConfig.RawJson.Should().Be("""{"percentage":25}""");
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
