using System.Text.Json;
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Interfaces;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Application.Strategies;

public sealed class RoleStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.RoleBased;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        var config = JsonSerializer.Deserialize<RoleConfig>(flag.StrategyConfig);

        if (config is null || config.Roles is null || config.Roles.Count == 0)
            return false;

        var allowedRoles = new HashSet<string>(config.Roles, StringComparer.OrdinalIgnoreCase);

        return context.UserRoles.Any(role => allowedRoles.Contains(role));
    }

    private sealed record RoleConfig(List<string> Roles);
}
