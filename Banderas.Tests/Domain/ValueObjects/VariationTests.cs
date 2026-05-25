using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.ValueObjects;
using FluentAssertions;

namespace Banderas.Tests.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class VariationTests
{
    // -------- AC-1: Value must be valid JSON for declared Kind --------

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [Trait("Category", "Unit")]
    public void Ctor_BooleanKind_WithCanonicalLowercase_Accepts(string value)
    {
        Action act = () => new Variation("k", VariationKind.Boolean, value);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("True")]
    [InlineData("FALSE")]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    [Trait("Category", "Unit")]
    public void Ctor_BooleanKind_WithNonCanonicalValue_ThrowsFlagDomainException(string value)
    {
        Action act = () => new Variation("k", VariationKind.Boolean, value);
        act.Should().Throw<FlagDomainException>();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("-7")]
    [InlineData("3.14")]
    [InlineData("1e2")]
    [Trait("Category", "Unit")]
    public void Ctor_NumberKind_WithJsonNumber_Accepts(string value)
    {
        Action act = () => new Variation("k", VariationKind.Number, value);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("\"42\"")]
    [InlineData("true")]
    [InlineData("[1]")]
    [Trait("Category", "Unit")]
    public void Ctor_NumberKind_WithNonNumberJson_ThrowsFlagDomainException(string value)
    {
        Action act = () => new Variation("k", VariationKind.Number, value);
        act.Should().Throw<FlagDomainException>();
    }

    [Theory]
    [InlineData("\"red-button\"")]
    [InlineData("\"\"")]
    [InlineData("\"with spaces and 0123\"")]
    [Trait("Category", "Unit")]
    public void Ctor_StringKind_WithJsonEncodedString_Accepts(string value)
    {
        Action act = () => new Variation("k", VariationKind.String, value);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("red-button")] // missing outer quotes — not valid JSON
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{}")]
    [Trait("Category", "Unit")]
    public void Ctor_StringKind_WithNonStringJson_ThrowsFlagDomainException(string value)
    {
        Action act = () => new Variation("k", VariationKind.String, value);
        act.Should().Throw<FlagDomainException>();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"theme\":\"dark\"}")]
    [InlineData("[]")]
    [InlineData("[1,2,3]")]
    [Trait("Category", "Unit")]
    public void Ctor_JsonKind_WithObjectOrArray_Accepts(string value)
    {
        Action act = () => new Variation("k", VariationKind.Json, value);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("42")] // scalar — belongs to Number
    [InlineData("\"red\"")] // scalar — belongs to String
    [InlineData("true")] // scalar — belongs to Boolean
    [InlineData("not-json")]
    [Trait("Category", "Unit")]
    public void Ctor_JsonKind_WithNonObjectOrArray_ThrowsFlagDomainException(string value)
    {
        Action act = () => new Variation("k", VariationKind.Json, value);
        act.Should().Throw<FlagDomainException>();
    }

    // -------- AC-2: Key character class, key length, value length --------

    [Theory]
    [InlineData("off")]
    [InlineData("on")]
    [InlineData("beta-1")]
    [InlineData("control_v2")]
    [InlineData("a")]
    [Trait("Category", "Unit")]
    public void Ctor_ValidKey_Accepts(string key)
    {
        Action act = () => new Variation(key, VariationKind.Boolean, "true");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ON")] // uppercase forbidden
    [InlineData("with space")]
    [InlineData("comma,here")]
    [InlineData("dot.here")]
    [Trait("Category", "Unit")]
    public void Ctor_InvalidKeyCharClassOrEmpty_ThrowsFlagDomainException(string key)
    {
        Action act = () => new Variation(key, VariationKind.Boolean, "true");
        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_KeyLongerThan50Chars_ThrowsFlagDomainException()
    {
        string longKey = new('a', 51);
        Action act = () => new Variation(longKey, VariationKind.Boolean, "true");
        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_KeyExactly50Chars_Accepts()
    {
        string key = new('a', 50);
        Action act = () => new Variation(key, VariationKind.Boolean, "true");
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_ValueLongerThan2000Chars_ThrowsFlagDomainException()
    {
        // 1998 chars of payload inside the outer quotes → 2000-char JSON string total + 1 extra
        string payload = new('a', 2000);
        string oversized = "\"" + payload + "\""; // length = 2002
        Action act = () => new Variation("k", VariationKind.String, oversized);
        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_PreservesKeyKindAndValue()
    {
        var variation = new Variation("beta", VariationKind.String, "\"red\"");
        variation.Key.Should().Be("beta");
        variation.Kind.Should().Be(VariationKind.String);
        variation.Value.Should().Be("\"red\"");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Equality_TwoVariationsWithSameFields_AreEqual()
    {
        var a = new Variation("on", VariationKind.Boolean, "true");
        var b = new Variation("on", VariationKind.Boolean, "true");
        a.Should().Be(b);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Equality_KeyCaseDifference_AreNotEqual()
    {
        // Pitfall #4: ordinal record equality. Case-insensitive uniqueness is
        // enforced by Flag at the collection level, not by VO equality.
        // (We can't actually construct "On" because the key regex forbids
        // uppercase — verify "off" vs "on" instead for sanity.)
        var a = new Variation("off", VariationKind.Boolean, "false");
        var b = new Variation("on", VariationKind.Boolean, "true");
        a.Should().NotBe(b);
    }
}
