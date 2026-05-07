using System.Text.Json;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;

namespace Banderas.Application.Validators;

public sealed class RoleBasedConfigValidator : IStrategyConfigValidator
{
    public RolloutStrategy StrategyType => RolloutStrategy.RoleBased;

    public StrategyConfig Validate(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new BanderasValidationException(
                "StrategyConfig is required for RoleBased strategy."
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
                "StrategyConfig must be valid JSON for RoleBased strategy."
            );
        }

        if (!root.TryGetProperty("roles", out JsonElement prop))
        {
            throw new BanderasValidationException(
                "StrategyConfig for RoleBased strategy must contain a 'roles' array."
            );
        }

        if (prop.ValueKind != JsonValueKind.Array || prop.GetArrayLength() == 0)
        {
            throw new BanderasValidationException(
                "StrategyConfig 'roles' must be a non-empty array."
            );
        }

        return StrategyConfig.Create(RolloutStrategy.RoleBased, rawJson);
    }
}
