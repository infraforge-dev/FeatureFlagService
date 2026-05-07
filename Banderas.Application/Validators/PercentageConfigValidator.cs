using System.Text.Json;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;

namespace Banderas.Application.Validators;

public sealed class PercentageConfigValidator : IStrategyConfigValidator
{
    public RolloutStrategy StrategyType => RolloutStrategy.Percentage;

    public StrategyConfig Validate(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new BanderasValidationException(
                "StrategyConfig is required for Percentage strategy."
            );
        }

        JsonElement root;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(rawJson);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BanderasValidationException(
                "StrategyConfig must be valid JSON for Percentage strategy."
            );
        }

        if (
            !root.TryGetProperty("percentage", out JsonElement prop)
            || !prop.TryGetInt32(out int percentage)
        )
        {
            throw new BanderasValidationException(
                "StrategyConfig for Percentage strategy must contain a 'percentage' integer field."
            );
        }

        if (percentage < 1 || percentage > 100)
        {
            throw new BanderasValidationException(
                "StrategyConfig 'percentage' must be between 1 and 100."
            );
        }

        return StrategyConfig.Create(RolloutStrategy.Percentage, rawJson);
    }
}
