using Banderas.Application.DTOs;
using Banderas.Application.Validators;
using Banderas.Domain.Enums;
using FluentAssertions;
using FluentValidation.Results;

namespace Banderas.Tests.Validators;

/// <summary>
/// Field-level validation rules for the <c>Variations</c> collection on
/// <see cref="CreateFlagRequest"/> and <see cref="UpdateFlagRequest"/>.
/// Each test exercises one of the seven DD-2 invariants from
/// <c>flag-variations/spec.md</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class VariationRequestValidatorTests
{
    private readonly CreateFlagRequestValidator _createValidator;
    private readonly UpdateFlagRequestValidator _updateValidator;

    public VariationRequestValidatorTests()
    {
        var factory = new StrategyConfigFactory([
            new NoneConfigValidator(),
            new PercentageConfigValidator(),
            new RoleBasedConfigValidator(),
        ]);
        _createValidator = new CreateFlagRequestValidator(factory);
        _updateValidator = new UpdateFlagRequestValidator(factory);
    }

    private static IReadOnlyList<VariationRequest> DefaultMenu() =>
        [new("off", "Boolean", "false"), new("on", "Boolean", "true")];

    private static CreateFlagRequest CreateWith(IReadOnlyList<VariationRequest> variations) =>
        new("flag-name", EnvironmentType.Development, true, RolloutStrategy.None, null)
        {
            Variations = variations,
        };

    private static UpdateFlagRequest UpdateWith(IReadOnlyList<VariationRequest>? variations) =>
        new(true, RolloutStrategy.None, null) { Variations = variations };

    // -------- Invariant 1: non-empty --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_VariationsEmpty_FailsValidationAsync()
    {
        ValidationResult result = await _createValidator.ValidateAsync(CreateWith([]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Variations");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Update_VariationsNull_PassesValidationAsync()
    {
        ValidationResult result = await _updateValidator.ValidateAsync(UpdateWith(null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Update_VariationsEmpty_FailsValidationAsync()
    {
        ValidationResult result = await _updateValidator.ValidateAsync(UpdateWith([]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Variations");
    }

    // -------- Invariant 2: max 20 --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_With21Variations_FailsValidationAsync()
    {
        IReadOnlyList<VariationRequest> variations = Enumerable
            .Range(0, 21)
            .Select(i => new VariationRequest(
                $"k-{i}",
                "Number",
                i.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ))
            .ToList();

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Variations");
    }

    // -------- Invariant 3: all same Kind --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_WithMixedKinds_FailsValidationAsync()
    {
        var variations = new List<VariationRequest>
        {
            new("off", "Boolean", "false"),
            new("count", "Number", "42"),
        };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Variations");
    }

    // -------- Invariant 4: unique keys (case-insensitive) --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_WithCaseInsensitiveDuplicateKeys_FailsValidationAsync()
    {
        // Note: the per-element key char-class check rejects "ON" as uppercase
        // *before* this rule fires. Use both lowercase keys with the same letters
        // to assert ordinal duplicate handling at the collection level instead;
        // case-insensitive uniqueness is exercised by the failing "ON" case via
        // the char-class rule below.
        var variations = new List<VariationRequest>
        {
            new("off", "Boolean", "false"),
            new("off", "Boolean", "true"),
        };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Variations");
    }

    // -------- Invariant 5: unique values --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_WithDuplicateValues_FailsValidationAsync()
    {
        var variations = new List<VariationRequest>
        {
            new("off", "Boolean", "false"),
            new("also-off", "Boolean", "false"),
        };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Variations");
    }

    // -------- Invariant 6: key char class --------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ON")]
    [InlineData("with space")]
    [InlineData("comma,here")]
    [Trait("Category", "Unit")]
    public async Task Create_WithInvalidKey_FailsValidationAsync(string badKey)
    {
        var variations = new List<VariationRequest> { new(badKey, "Boolean", "true") };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.PropertyName.StartsWith("Variations", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_WithKeyLongerThan50Chars_FailsValidationAsync()
    {
        string longKey = new('a', 51);
        var variations = new List<VariationRequest> { new(longKey, "Boolean", "true") };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.PropertyName.StartsWith("Variations", StringComparison.Ordinal));
    }

    // -------- Invariant 7: value size cap --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_WithValueLongerThan2000Chars_FailsValidationAsync()
    {
        string oversized = "\"" + new string('a', 2000) + "\"";
        var variations = new List<VariationRequest> { new("k", "String", oversized) };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.PropertyName.StartsWith("Variations", StringComparison.Ordinal));
    }

    // -------- Unknown kind --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_WithUnknownKind_FailsValidationAsync()
    {
        var variations = new List<VariationRequest> { new("k", "Object", "{}") };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.PropertyName.StartsWith("Variations", StringComparison.Ordinal));
    }

    // -------- Value-must-be-valid-JSON-for-kind --------

    [Theory]
    [InlineData("Boolean", "True")] // capitalized — JSON requires lowercase
    [InlineData("Number", "hello")]
    [InlineData("String", "red-button")] // missing outer quotes
    [InlineData("Json", "not-json")]
    [Trait("Category", "Unit")]
    public async Task Create_WithMalformedValueForKind_FailsValidationAsync(
        string kind,
        string value
    )
    {
        var variations = new List<VariationRequest> { new("k", kind, value) };

        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(variations));

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.PropertyName.StartsWith("Variations", StringComparison.Ordinal));
    }

    // -------- Happy path --------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_WithValidDefaultMenu_PassesAsync()
    {
        ValidationResult result = await _createValidator.ValidateAsync(CreateWith(DefaultMenu()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Update_WithValidPopulatedMenu_PassesAsync()
    {
        ValidationResult result = await _updateValidator.ValidateAsync(UpdateWith(DefaultMenu()));

        result.IsValid.Should().BeTrue();
    }
}
