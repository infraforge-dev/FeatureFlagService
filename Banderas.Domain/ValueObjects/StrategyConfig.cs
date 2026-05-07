using Banderas.Domain.Enums;

namespace Banderas.Domain.ValueObjects;

public sealed record StrategyConfig
{
    public RolloutStrategy ValidatedFor { get; }
    public string RawJson { get; }

    public static StrategyConfig Create(RolloutStrategy strategy, string rawJson) =>
        new(strategy, rawJson);

    internal StrategyConfig(RolloutStrategy validatedFor, string rawJson)
    {
        ValidatedFor = validatedFor;
        RawJson = rawJson ?? throw new ArgumentNullException(nameof(rawJson));
    }
}
