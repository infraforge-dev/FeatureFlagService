namespace Banderas.Application.Exceptions;

public sealed class AiAnalysisUnavailableException : Exception
{
    public AiAnalysisUnavailableException(string message)
        : base(message) { }

    public AiAnalysisUnavailableException(string message, Exception inner)
        : base(message, inner) { }
}
