using Banderas.Application.Telemetry;
using Banderas.Domain.Enums;

namespace Banderas.Infrastructure.Telemetry;

public sealed class NullTelemetryService : ITelemetryService
{
    public void TrackEvaluation(
        string flagName,
        bool result,
        RolloutStrategy strategy,
        EnvironmentType environment
    ) { }
}
