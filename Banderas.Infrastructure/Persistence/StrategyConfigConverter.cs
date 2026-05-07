using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Banderas.Infrastructure.Persistence;

/// <summary>
/// Converts StrategyConfig VO to/from the jsonb column.
/// On write: serializes RawJson.
/// On read: reconstructs with RolloutStrategy.None as placeholder —
/// FlagConfiguration post-processes via AfterSaveBehavior to set the
/// correct ValidatedFor from Flag.StrategyType. Since ValidatedFor
/// is always equal to Flag.StrategyType and the data was validated
/// on write, this is safe.
/// </summary>
public sealed class StrategyConfigConverter : ValueConverter<StrategyConfig, string>
{
    public StrategyConfigConverter()
        : base(config => config.RawJson, json => new StrategyConfig(RolloutStrategy.None, json)) { }
}
