using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;
using Banderas.Infrastructure.Persistence;
using FluentAssertions;

namespace Banderas.Tests.Integration;

[Trait("Category", "Unit")]
public sealed class VariationListConverterTests
{
    private static readonly VariationListConverter Converter = new();

    private static string ToProvider(IReadOnlyList<Variation> input) =>
        (string)Converter.ConvertToProvider.Invoke(input)!;

    private static IReadOnlyList<Variation> FromProvider(string json) =>
        (IReadOnlyList<Variation>)Converter.ConvertFromProvider.Invoke(json)!;

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_BooleanMenu_PreservesOrder()
    {
        IReadOnlyList<Variation> input =
        [
            new("off", VariationKind.Boolean, "false"),
            new("on", VariationKind.Boolean, "true"),
        ];

        string json = ToProvider(input);
        IReadOnlyList<Variation> roundTripped = FromProvider(json);

        roundTripped.Should().HaveCount(2);
        roundTripped[0].Should().Be(input[0]);
        roundTripped[1].Should().Be(input[1]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_NumberMenu_PreservesValues()
    {
        IReadOnlyList<Variation> input =
        [
            new("low", VariationKind.Number, "0"),
            new("mid", VariationKind.Number, "50"),
            new("high", VariationKind.Number, "100"),
        ];

        IReadOnlyList<Variation> roundTripped = FromProvider(ToProvider(input));

        roundTripped.Should().Equal(input);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_StringMenu_PreservesJsonEncodedValues()
    {
        IReadOnlyList<Variation> input =
        [
            new("control", VariationKind.String, "\"control\""),
            new("treatment", VariationKind.String, "\"red-button\""),
        ];

        IReadOnlyList<Variation> roundTripped = FromProvider(ToProvider(input));

        roundTripped.Should().Equal(input);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_JsonMenu_PreservesNestedShape()
    {
        IReadOnlyList<Variation> input =
        [
            new("variant-a", VariationKind.Json, "{\"theme\":\"dark\"}"),
            new("variant-b", VariationKind.Json, "{\"theme\":\"light\"}"),
        ];

        IReadOnlyList<Variation> roundTripped = FromProvider(ToProvider(input));

        roundTripped.Should().Equal(input);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void WriteShape_EmitsCamelCaseAndEnumAsString()
    {
        IReadOnlyList<Variation> input = [new("off", VariationKind.Boolean, "false")];

        string json = ToProvider(input);

        json.Should().Contain("\"key\":\"off\"");
        json.Should().Contain("\"kind\":\"Boolean\"");
        json.Should().Contain("\"value\":\"false\"");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ConvertFromProvider_NullJsonPayload_ReturnsEmptyList()
    {
        // Defensive: migration backfill guarantees NOT NULL, but a hand-edited
        // row or future migration regression should not throw a NullReferenceException
        // at the converter — return empty so the domain invariant catches it.
        IReadOnlyList<Variation> result = FromProvider("null");

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ConvertFromProvider_EmptyJsonArray_ReturnsEmptyList()
    {
        IReadOnlyList<Variation> result = FromProvider("[]");

        result.Should().BeEmpty();
    }
}
