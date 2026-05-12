using Banderas.Infrastructure.Persistence;
using FluentAssertions;

namespace Banderas.Tests.Integration;

[Trait("Category", "Unit")]
public sealed class TagListConverterTests
{
    private static readonly TagListConverter Converter = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_EmptyList_PreservesShape()
    {
        IReadOnlyList<string> input = [];

        string json = (string)Converter.ConvertToProvider.Invoke(input)!;
        IReadOnlyList<string> roundTripped =
            (IReadOnlyList<string>)Converter.ConvertFromProvider.Invoke(json)!;

        json.Should().Be("[]");
        roundTripped.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_PopulatedList_PreservesEntriesAndOrder()
    {
        IReadOnlyList<string> input = ["checkout", "release-q2", "team_alpha"];

        string json = (string)Converter.ConvertToProvider.Invoke(input)!;
        IReadOnlyList<string> roundTripped =
            (IReadOnlyList<string>)Converter.ConvertFromProvider.Invoke(json)!;

        roundTripped.Should().Equal(input);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ConvertFromProvider_NullJsonPayload_ReturnsEmptyList()
    {
        // Defensive: jsonb default '[]' should prevent this, but assert the
        // converter's null-fallback so the domain invariant (Tags != null) holds
        // even if a NULL slipped through.
        IReadOnlyList<string> roundTripped =
            (IReadOnlyList<string>)Converter.ConvertFromProvider.Invoke("null")!;

        roundTripped.Should().BeEmpty();
    }
}
