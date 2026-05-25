namespace Banderas.Application.Validators;

/// <summary>
/// Strongly-typed validation messages for variation menu rules.
/// Using a <c>readonly record struct</c> groups related messages into a single
/// type-safe unit with zero heap allocation.
/// </summary>
public readonly record struct VariationValidationMessage(string Message)
{
    public static VariationValidationMessage Required => new("Variations is required.");

    public static VariationValidationMessage Empty =>
        new("Variations must contain at least one variation.");

    public static VariationValidationMessage TooMany(int max) =>
        new($"Variations may not contain more than {max} entries.");

    public static VariationValidationMessage KeyInvalidCharacters(int index) =>
        new(
            $"variations[{index}].key may only contain lowercase letters, numbers, hyphens, and underscores."
        );

    public static VariationValidationMessage KeyTooLong(int index, int max) =>
        new($"variations[{index}].key must not exceed {max} characters.");

    public static VariationValidationMessage ValueInvalid(int index, int max) =>
        new($"variations[{index}].value must be non-null and not exceed {max} characters.");

    public static VariationValidationMessage KindInvalid(int index) =>
        new($"variations[{index}].kind must be one of Boolean, String, Number, Json.");

    public static VariationValidationMessage ValueNotValidForKind(int index, string kind) =>
        new($"variations[{index}].value is not valid JSON for the declared Kind={kind}.");

    public static VariationValidationMessage MixedKinds =>
        new("All variations on a flag must share the same Kind.");

    public static VariationValidationMessage DuplicateKey(string key) =>
        new($"Variation key '{key}' is duplicated (keys are case-insensitive).");

    public static VariationValidationMessage DuplicateValue(string value) =>
        new($"Variation value '{value}' is duplicated within the menu.");
}
