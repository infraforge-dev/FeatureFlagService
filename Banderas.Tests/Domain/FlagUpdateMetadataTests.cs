using Banderas.Domain.Entities;
using Banderas.Domain.Exceptions;
using Banderas.Tests.Helpers;
using FluentAssertions;

namespace Banderas.Tests.Domain;

[Trait("Category", "Unit")]
public sealed class FlagUpdateMetadataTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateMetadata_ReplacesDescriptionAndTags_AndBumpsUpdatedAt()
    {
        Flag flag = FlagBuilder.Build();
        DateTime original = flag.UpdatedAt;
        Thread.Sleep(15);

        flag.UpdateMetadata("Controls checkout v2", ["squad-checkout", "release-q2"]);

        flag.Description.Should().Be("Controls checkout v2");
        flag.Tags.Should().BeEquivalentTo(["squad-checkout", "release-q2"]);
        flag.UpdatedAt.Should().BeAfter(original);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateMetadata_WhenFlagIsArchived_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        Action act = () => flag.UpdateMetadata("anything", []);

        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateMetadata_WithNullDescription_ClearsDescription()
    {
        Flag flag = FlagBuilder.Build();
        flag.UpdateMetadata("temp", ["tag"]);

        flag.UpdateMetadata(null, ["tag"]);

        flag.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateMetadata_WithEmptyTags_ClearsTags()
    {
        Flag flag = FlagBuilder.Build();
        flag.UpdateMetadata("desc", ["one", "two"]);

        flag.UpdateMetadata("desc", []);

        flag.Tags.Should().BeEmpty();
    }
}
