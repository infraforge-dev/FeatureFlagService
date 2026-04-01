using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Interfaces;
using FeatureFlag.Domain.ValueObjects;

namespace FeatureFlag.Application.Strategies;

public sealed class PercentageStrategy : IRolloutStrategy
{
    public RolloutStrategy StrategyType => RolloutStrategy.Percentage;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        PercentageConfig? config = JsonSerializer.Deserialize<PercentageConfig>(
            flag.StrategyConfig
        );

        if (config is null || config.Percentage is < 0 or > 100)
        {
            return false;
        }

        string input = $"{context.UserId}:{flag.Name}";
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        uint bucket = BitConverter.ToUInt32(hashBytes, 0) % 100;

        return bucket < (uint)config.Percentage;
    }

    private sealed record PercentageConfig(int Percentage);
}
