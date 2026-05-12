using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;
using Banderas.Tests.Helpers;
using FluentAssertions;

namespace Banderas.Tests.Domain;

[Trait("Category", "Unit")]
public sealed class FlagConstructorMetadataTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WhenDescriptionAndTagsOmitted_DefaultsToNullAndEmpty()
    {
        Flag flag = FlagBuilder.Build();

        flag.Description.Should().BeNull();
        flag.Tags.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WhenDescriptionAndTagsProvided_PersistsBoth()
    {
        var config = new StrategyConfig(RolloutStrategy.None, "{}");

        var flag = new Flag(
            name: "checkout-v2",
            environment: EnvironmentType.Development,
            isEnabled: true,
            strategyType: RolloutStrategy.None,
            strategyConfig: config,
            description: "Controls the v2 checkout flow",
            tags: ["squad-checkout", "release-q2"]
        );

        flag.Description.Should().Be("Controls the v2 checkout flow");
        flag.Tags.Should().BeEquivalentTo(["squad-checkout", "release-q2"]);
    }
}
