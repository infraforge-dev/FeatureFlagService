using Banderas.Domain.Enums;

namespace Banderas.Application.Telemetry;

public interface ITelemetryService
{
    void TrackEvaluation(
        string flagName,
        bool result,
        RolloutStrategy strategy,
        EnvironmentType environment
    );
}
