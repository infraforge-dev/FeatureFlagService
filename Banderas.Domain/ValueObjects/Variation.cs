using System.Text.Json;
using System.Text.RegularExpressions;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;

namespace Banderas.Domain.ValueObjects;

/// <summary>
/// A single output option in a flag's variation menu.
/// <para>
/// <see cref="Value"/> is always a JSON-encoded string matching the declared <see cref="Kind"/>:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="VariationKind.Boolean"/> — <c>"true"</c> or <c>"false"</c> (canonical lowercase).</description></item>
///   <item><description><see cref="VariationKind.Number"/> — any JSON number, e.g. <c>"42"</c>, <c>"3.14"</c>, <c>"-7"</c>.</description></item>
///   <item><description><see cref="VariationKind.String"/> — a JSON-encoded string, e.g. <c>"\"red-button\""</c>. Raw <c>red-button</c> is malformed.</description></item>
///   <item><description><see cref="VariationKind.Json"/> — a JSON object or array. Scalars belong to the other three kinds.</description></item>
/// </list>
/// <para>
/// Equality is record-default (ordinal). Case-insensitive key uniqueness is a
/// collection-level invariant enforced by <see cref="Banderas.Domain.Entities.Flag"/>,
/// not by this VO.
/// </para>
/// </summary>
public sealed record Variation
{
    /// <summary>Maximum length of <see cref="Key"/>, in characters.</summary>
    public const int MaxKeyLength = 50;

    /// <summary>Maximum length of <see cref="Value"/>, in characters.</summary>
    public const int MaxValueLength = 2000;

    private static readonly Regex KeyPattern = new(
        "^[a-z0-9\\-_]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public string Key { get; }
    public VariationKind Kind { get; }
    public string Value { get; }

    public Variation(string key, VariationKind kind, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new FlagDomainException("Variation key cannot be empty or whitespace.");
        }

        if (key.Length > MaxKeyLength)
        {
            throw new FlagDomainException(
                $"Variation key '{key}' exceeds maximum length of {MaxKeyLength} characters."
            );
        }

        if (!KeyPattern.IsMatch(key))
        {
            throw new FlagDomainException(
                $"Variation key '{key}' may only contain lowercase letters, numbers, hyphens, and underscores."
            );
        }

        if (value.Length > MaxValueLength)
        {
            throw new FlagDomainException(
                $"Variation value for key '{key}' exceeds maximum length of {MaxValueLength} characters."
            );
        }

        EnsureValueMatchesKind(key, kind, value);

        Key = key;
        Kind = kind;
        Value = value;
    }

    /// <summary>
    /// Verifies that <paramref name="value"/> is valid JSON of the type implied by
    /// <paramref name="kind"/>. Parses with <see cref="JsonDocument"/> inside a
    /// using-block — the document is disposed immediately; only the validity boolean is retained.
    /// </summary>
    private static void EnsureValueMatchesKind(string key, VariationKind kind, string value)
    {
        // Boolean is canonical lowercase per JSON spec. Compare ordinally before
        // attempting to parse so we reject "True"/"FALSE" without ambiguity.
        if (kind == VariationKind.Boolean)
        {
            if (value is not ("true" or "false"))
            {
                throw new FlagDomainException(
                    $"Variation '{key}' declared Kind=Boolean but Value '{value}' is not canonical JSON 'true' or 'false'."
                );
            }
            return;
        }

        JsonValueKind actualKind;
        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            actualKind = document.RootElement.ValueKind;
        }
        catch (JsonException ex)
        {
            throw new FlagDomainException(
                $"Variation '{key}' declared Kind={kind} but Value is not valid JSON: {ex.Message}"
            );
        }

        bool matches = kind switch
        {
            VariationKind.Number => actualKind == JsonValueKind.Number,
            VariationKind.String => actualKind == JsonValueKind.String,
            VariationKind.Json => actualKind is JsonValueKind.Object or JsonValueKind.Array,
            _ => false,
        };

        if (!matches)
        {
            throw new FlagDomainException(
                $"Variation '{key}' declared Kind={kind} but Value parsed as JSON {actualKind}."
            );
        }
    }
}
