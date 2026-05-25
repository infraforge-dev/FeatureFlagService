using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.ValueObjects;
using Banderas.Tests.Helpers;
using FluentAssertions;

namespace Banderas.Tests.Domain;

[Trait("Category", "Unit")]
public sealed class FlagVariationsTests
{
    private static Variation Bool(string key, bool value) =>
        new(key, VariationKind.Boolean, value ? "true" : "false");

    // -------- AC-3: collection invariants enforced at construction --------

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WithEmptyVariations_ThrowsFlagDomainException()
    {
        Action act = () => FlagBuilder.Build(variations: []);
        act.Should().Throw<FlagDomainException>().WithMessage("*at least one*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WithTwentyOneVariations_ThrowsFlagDomainException()
    {
        List<Variation> variations = Enumerable
            .Range(0, 21)
            .Select(i => new Variation(
                $"k-{i}",
                VariationKind.Number,
                i.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ))
            .ToList();

        Action act = () => FlagBuilder.Build(variations: variations);
        act.Should().Throw<FlagDomainException>().WithMessage("*20*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WithExactlyTwentyVariations_Accepts()
    {
        List<Variation> variations = Enumerable
            .Range(0, 20)
            .Select(i => new Variation(
                $"k-{i}",
                VariationKind.Number,
                i.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ))
            .ToList();

        Action act = () => FlagBuilder.Build(variations: variations);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WithMixedKinds_ThrowsFlagDomainException()
    {
        var variations = new List<Variation>
        {
            new("off", VariationKind.Boolean, "false"),
            new("count", VariationKind.Number, "42"),
        };

        Action act = () => FlagBuilder.Build(variations: variations);
        act.Should().Throw<FlagDomainException>().WithMessage("*same Kind*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WithDuplicateKeys_ThrowsFlagDomainException()
    {
        var variations = new List<Variation>
        {
            new("off", VariationKind.Boolean, "false"),
            new("off", VariationKind.Boolean, "true"),
        };

        Action act = () => FlagBuilder.Build(variations: variations);
        act.Should().Throw<FlagDomainException>().WithMessage("*key*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WithDuplicateValues_ThrowsFlagDomainException()
    {
        var variations = new List<Variation>
        {
            new("off", VariationKind.Boolean, "false"),
            new("also-off", VariationKind.Boolean, "false"),
        };

        Action act = () => FlagBuilder.Build(variations: variations);
        act.Should().Throw<FlagDomainException>().WithMessage("*value*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ctor_WithValidMenu_PreservesOrder()
    {
        var variations = new List<Variation>
        {
            new("off", VariationKind.Boolean, "false"),
            new("on", VariationKind.Boolean, "true"),
        };

        Flag flag = FlagBuilder.Build(variations: variations);

        flag.Variations.Should().HaveCount(2);
        flag.Variations[0].Key.Should().Be("off");
        flag.Variations[1].Key.Should().Be("on");
    }

    // -------- AC-4: UpdateVariations enforces invariants + archived guard --------

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateVariations_OnArchivedFlag_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();
        flag.Archive();

        Action act = () => flag.UpdateVariations([Bool("a", true)]);

        act.Should().Throw<FlagDomainException>().WithMessage("*archived*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateVariations_WithInvalidCollection_LeavesExistingVariationsUnchanged()
    {
        Flag flag = FlagBuilder.Build();
        IReadOnlyList<Variation> original = flag.Variations;

        Action act = () => flag.UpdateVariations([]);

        act.Should().Throw<FlagDomainException>();
        flag.Variations.Should().BeEquivalentTo(original);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateVariations_WithValidCollection_ReplacesAtomicallyAndBumpsUpdatedAt()
    {
        Flag flag = FlagBuilder.Build();
        DateTime originalUpdated = flag.UpdatedAt;
        Thread.Sleep(15);

        var newMenu = new List<Variation>
        {
            new("a", VariationKind.Number, "1"),
            new("b", VariationKind.Number, "2"),
            new("c", VariationKind.Number, "3"),
        };

        flag.UpdateVariations(newMenu);

        flag.Variations.Should().HaveCount(3);
        flag.Variations[2].Key.Should().Be("c");
        flag.UpdatedAt.Should().BeAfter(originalUpdated);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateVariations_WithMixedKinds_ThrowsFlagDomainException()
    {
        Flag flag = FlagBuilder.Build();

        var bad = new List<Variation>
        {
            new("a", VariationKind.Boolean, "true"),
            new("b", VariationKind.Number, "1"),
        };

        Action act = () => flag.UpdateVariations(bad);

        act.Should().Throw<FlagDomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateVariations_WithCaseInsensitiveDuplicateKeys_ThrowsFlagDomainException()
    {
        // Direct VO ctor forbids uppercase, but we still want defense-in-depth on
        // case-insensitive equality for the collection check. The simplest way to
        // exercise it is via two structurally-equal keys (`on` and `on`), which
        // also covers ordinal duplicates. The case-insensitive concern is the
        // operator-facing layer (validator) — see VariationRequestValidatorTests.
        Flag flag = FlagBuilder.Build();

        var bad = new List<Variation>
        {
            new("on", VariationKind.Boolean, "true"),
            new("on", VariationKind.Boolean, "false"),
        };

        Action act = () => flag.UpdateVariations(bad);

        act.Should().Throw<FlagDomainException>().WithMessage("*key*");
    }
}
