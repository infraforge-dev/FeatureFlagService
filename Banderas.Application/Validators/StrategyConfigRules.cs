using System.Text.Json;

namespace Banderas.Application.Validators;

/// <summary>
/// Shared strategy config validation rules. Called by both
/// CreateFlagRequestValidator and UpdateFlagRequestValidator.
/// Add new strategy rules here when new IRolloutStrategy types are introduced.
/// </summary>
internal static class StrategyConfigRules
{
    /// <summary>
    /// Returns true if config is valid JSON containing a 'percentage'
    /// integer field with a value between 1 and 100 inclusive.
    /// </summary>
    internal static bool BeValidPercentageConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("percentage", out JsonElement prop))
            {
                return false;
            }

            if (!prop.TryGetInt32(out int percentage))
            {
                return false;
            }

            return percentage >= 1 && percentage <= 100;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true if config is valid JSON containing a 'roles' array
    /// with at least one element.
    /// </summary>
    internal static bool BeValidRoleConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("roles", out JsonElement prop))
            {
                return false;
            }

            if (prop.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return prop.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
