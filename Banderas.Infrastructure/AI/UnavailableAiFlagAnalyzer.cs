using Banderas.Application.AI;
using Banderas.Application.DTOs;
using Banderas.Application.Exceptions;

namespace Banderas.Infrastructure.AI;

public sealed class UnavailableAiFlagAnalyzer : IAiFlagAnalyzer
{
    private readonly string _message;

    public UnavailableAiFlagAnalyzer(string message)
    {
        _message = message;
    }

    public Task<FlagHealthAnalysisResponse> AnalyzeAsync(
        IReadOnlyList<FlagResponse> flags,
        int stalenessThresholdDays,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromException<FlagHealthAnalysisResponse>(
            new AiAnalysisUnavailableException(_message)
        );
    }
}
