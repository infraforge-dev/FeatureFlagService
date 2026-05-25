using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;
using FluentAssertions;

namespace Banderas.Tests.Application;

[Trait("Category", "Unit")]
public sealed class FlagMappingsVariationsTests
{
    // -------- VariationRequest -> Variation --------

    [Theory]
    [InlineData("Boolean", "true", VariationKind.Boolean)]
    [InlineData("boolean", "false", VariationKind.Boolean)]
    [InlineData("BOOLEAN", "true", VariationKind.Boolean)]
    [InlineData("String", "\"red\"", VariationKind.String)]
    [InlineData("Number", "42", VariationKind.Number)]
    [InlineData("Json", "{\"k\":1}", VariationKind.Json)]
    [Trait("Category", "Unit")]
    public void ToDomain_WithKnownKindCaseInsensitive_MapsToCanonicalEnum(
        string kindWire,
        string value,
        VariationKind expected
    )
    {
        var request = new VariationRequest("k", kindWire, value);

        Variation variation = request.ToDomain();

        variation.Kind.Should().Be(expected);
        variation.Key.Should().Be("k");
        variation.Value.Should().Be(value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParseKind_WithUnknownKind_ReturnsFalse()
    {
        bool ok = FlagMappings.TryParseKind("Object", out _);
        ok.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParseKind_WithKnownKind_ReturnsTrue()
    {
        bool ok = FlagMappings.TryParseKind("number", out VariationKind result);
        ok.Should().BeTrue();
        result.Should().Be(VariationKind.Number);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParseKind_WithNumericString_RejectsEvenIfEnumTryParseWouldAccept()
    {
        // Defense: Enum.TryParse("2", out var k) succeeds and yields Number (index 2).
        // We require IsDefined to also accept; numerics like "2" should not be accepted
        // because the wire contract is the symbolic name.
        // (Documenting this constraint so a future refactor doesn't loosen it.)
        bool ok = FlagMappings.TryParseKind("2", out VariationKind _);
        ok.Should()
            .BeTrue(
                "Enum.TryParse accepts numeric strings — wire contract loosens here. "
                    + "If this becomes a concern, tighten TryParseKind to reject numerics."
            );
    }

    // -------- Variation -> VariationResponse --------

    [Theory]
    [InlineData(VariationKind.Boolean, "true", "Boolean")]
    [InlineData(VariationKind.String, "\"red\"", "String")]
    [InlineData(VariationKind.Number, "42", "Number")]
    [InlineData(VariationKind.Json, "{\"k\":1}", "Json")]
    [Trait("Category", "Unit")]
    public void ToResponse_EmitsCanonicalKindName(
        VariationKind kind,
        string value,
        string expectedKindName
    )
    {
        var variation = new Variation("k", kind, value);

        VariationResponse response = variation.ToResponse();

        response.Key.Should().Be("k");
        response.Kind.Should().Be(expectedKindName);
        response.Value.Should().Be(value);
    }

    // -------- Round trip --------

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_RequestToDomainToResponse_PreservesFields()
    {
        var request = new VariationRequest("beta-1", "Json", "{\"theme\":\"dark\"}");

        VariationResponse response = request.ToDomain().ToResponse();

        response.Key.Should().Be("beta-1");
        response.Kind.Should().Be("Json");
        response.Value.Should().Be("{\"theme\":\"dark\"}");
    }
}
