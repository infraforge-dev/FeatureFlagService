using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;

namespace Banderas.Application.Strategies;

public sealed class PercentageStrategy : IRolloutStrategy
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public RolloutStrategy StrategyType => RolloutStrategy.Percentage;

    public bool Evaluate(Flag flag, FeatureEvaluationContext context)
    {
        PercentageConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<PercentageConfig>(flag.StrategyConfig, Options);
        }
        catch (JsonException)
        {
            return false;
        }

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
