namespace Banderas.Application.DTOs;

public record FlagHealthRequest
{
    /// <summary>
    /// Number of days without an update before a flag is considered stale.
    /// Defaults to FlagHealthConstants.DefaultStalenessThresholdDays (30) if not specified.
    /// Min: 1. Max: 365.
    /// </summary>
    public int? StalenessThresholdDays { get; init; }
}
