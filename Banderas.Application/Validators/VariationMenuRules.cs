using System.Text.Json;
using System.Text.RegularExpressions;
using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using FluentValidation;

namespace Banderas.Application.Validators;

/// <summary>
/// Shared FluentValidation rules for the variation menu collection.
/// Both <see cref="CreateFlagRequestValidator"/> and <see cref="UpdateFlagRequestValidator"/>
/// apply the same rule set; the only difference is that Update tolerates <c>null</c>
/// (no change) while Create requires the collection to be set and non-empty.
/// </summary>
internal static class VariationMenuRules
{
    public const int MaxCount = 20;
    public const int MaxKeyLength = 50;
    public const int MaxValueLength = 2000;

    private static readonly Regex KeyPattern = new(
        "^[a-z0-9\\-_]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    /// <summary>
    /// Applies the seven DD-2 invariants to a variation menu collection. Each
    /// violation surfaces a field-level error on the <c>Variations</c> property.
    /// Per-element rules also surface (collectively under <c>Variations</c>).
    /// </summary>
    public static IRuleBuilderOptions<T, IReadOnlyList<VariationRequest>?> ApplyMenuRules<T>(
        this IRuleBuilder<T, IReadOnlyList<VariationRequest>?> rule
    )
    {
        return rule.Must(BeAValidMenuOrNull).WithMessage(BuildErrorMessage);
    }

    private static bool BeAValidMenuOrNull(IReadOnlyList<VariationRequest>? menu)
    {
        // Null is a separate concern handled at the caller (Create rejects null,
        // Update tolerates it). This helper validates *content* when a list is
        // present; null passes through here unchallenged.
        if (menu is null)
        {
            return true;
        }

        return EvaluateMenu(menu) is null;
    }

    private static string BuildErrorMessage<T>(T request, IReadOnlyList<VariationRequest>? menu)
    {
        if (menu is null)
        {
            return "Variations is required.";
        }

        return EvaluateMenu(menu) ?? "Variations is valid.";
    }

    /// <summary>
    /// Evaluates a non-null menu against all seven invariants and returns the
    /// first violation message encountered, or <c>null</c> if the menu is valid.
    /// </summary>
    private static string? EvaluateMenu(IReadOnlyList<VariationRequest> menu)
    {
        // Invariant 1: non-empty
        if (menu.Count == 0)
        {
            return "Variations must contain at least one variation.";
        }

        // Invariant 2: max count
        if (menu.Count > MaxCount)
        {
            return $"Variations may not contain more than {MaxCount} entries.";
        }

        // Per-element rules (invariants 6 + 7, plus value-is-valid-JSON-for-kind)
        for (int i = 0; i < menu.Count; i++)
        {
            VariationRequest v = menu[i];

            if (string.IsNullOrWhiteSpace(v.Key) || !KeyPattern.IsMatch(v.Key))
            {
                return $"variations[{i}].key may only contain lowercase letters, numbers, hyphens, and underscores.";
            }

            if (v.Key.Length > MaxKeyLength)
            {
                return $"variations[{i}].key must not exceed {MaxKeyLength} characters.";
            }

            if (v.Value is null || v.Value.Length > MaxValueLength)
            {
                return $"variations[{i}].value must be non-null and not exceed {MaxValueLength} characters.";
            }

            if (!FlagMappings.TryParseKind(v.Kind, out VariationKind parsedKind))
            {
                return $"variations[{i}].kind must be one of Boolean, String, Number, Json.";
            }

            if (!IsValueValidForKind(parsedKind, v.Value))
            {
                return $"variations[{i}].value is not valid JSON for the declared Kind={parsedKind}.";
            }
        }

        // Invariant 3: all same Kind
        VariationKind firstKind = ParseKindOrDefault(menu[0].Kind);
        for (int i = 1; i < menu.Count; i++)
        {
            if (ParseKindOrDefault(menu[i].Kind) != firstKind)
            {
                return "All variations on a flag must share the same Kind.";
            }
        }

        // Invariant 4: unique keys (case-insensitive)
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (VariationRequest v in menu)
        {
            if (!seenKeys.Add(v.Key))
            {
                return $"Variation key '{v.Key}' is duplicated (keys are case-insensitive).";
            }
        }

        // Invariant 5: unique values (ordinal)
        HashSet<string> seenValues = new(StringComparer.Ordinal);
        foreach (VariationRequest v in menu)
        {
            if (!seenValues.Add(v.Value))
            {
                return $"Variation value '{v.Value}' is duplicated within the menu.";
            }
        }

        return null;
    }

    private static VariationKind ParseKindOrDefault(string kind) =>
        FlagMappings.TryParseKind(kind, out VariationKind result) ? result : VariationKind.Boolean;

    private static bool IsValueValidForKind(VariationKind kind, string value)
    {
        if (kind == VariationKind.Boolean)
        {
            return value is "true" or "false";
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(value);
            JsonValueKind vk = doc.RootElement.ValueKind;
            return kind switch
            {
                VariationKind.Number => vk == JsonValueKind.Number,
                VariationKind.String => vk == JsonValueKind.String,
                VariationKind.Json => vk is JsonValueKind.Object or JsonValueKind.Array,
                _ => false,
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
