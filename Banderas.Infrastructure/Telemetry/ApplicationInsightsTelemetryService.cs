using Banderas.Application.Telemetry;
using Banderas.Domain.Enums;
using Microsoft.ApplicationInsights;

namespace Banderas.Infrastructure.Telemetry;

public sealed class ApplicationInsightsTelemetryService : ITelemetryService
{
    private readonly TelemetryClient _telemetryClient;

    public ApplicationInsightsTelemetryService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public void TrackEvaluation(
        string flagName,
        bool result,
        RolloutStrategy strategy,
        EnvironmentType environment
    )
    {
        var properties = new Dictionary<string, string>
        {
            ["FlagName"] = flagName,
            ["Result"] = result ? "enabled" : "disabled",
            ["Strategy"] = strategy.ToString(),
            ["Environment"] = environment.ToString(),
        };

        _telemetryClient.TrackEvent("flag.evaluated", properties);
    }
}
