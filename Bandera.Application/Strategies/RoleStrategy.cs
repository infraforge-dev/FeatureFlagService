using System.Text.Json;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Strategies;

public sealed class RoleStrategy : IRolloutStrategy
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public RolloutStrategy StrategyType => RolloutStrategy.RoleBased;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        RoleConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<RoleConfig>(flag.StrategyConfig, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        if (config is null || config.Roles is null || config.Roles.Count == 0)
        {
            return false;
        }

        var allowedRoles = new HashSet<string>(config.Roles, StringComparer.OrdinalIgnoreCase);

        return context.UserRoles.Any(role => allowedRoles.Contains(role));
    }

    private sealed record RoleConfig(List<string> Roles);
}
