using Banderas.Application.DTOs;
using Banderas.Application.Exceptions;

namespace Banderas.Application.AI;

/// <summary>
/// Sends sanitized flag data to Azure OpenAI and returns a structured
/// flag health analysis. Read-only — no side effects on flag state.
/// </summary>
public interface IAiFlagAnalyzer
{
    /// <summary>
    /// Analyzes the provided flags and returns a structured health assessment.
    /// Flags must be pre-sanitized before this call.
    /// </summary>
    /// <exception cref="AiAnalysisUnavailableException">
    /// Thrown when the AI service is unreachable, times out, or returns
    /// an unparseable response.
    /// </exception>
    Task<FlagHealthAnalysisResponse> AnalyzeAsync(
        IReadOnlyList<FlagResponse> flags,
        int stalenessThresholdDays,
        CancellationToken cancellationToken = default);
}
