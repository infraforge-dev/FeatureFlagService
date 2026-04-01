namespace FeatureFlag.Domain.Exceptions;

/// <summary>
/// Base class for all domain exceptions in FeatureFlagService.
/// Carries the HTTP status code that the middleware will use
/// when building the ProblemDetails response.
/// </summary>
public abstract class FeatureFlagException : Exception
{
    /// <summary>
    /// The HTTP status code this exception maps to.
    /// </summary>
    public int StatusCode { get; }

    protected FeatureFlagException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
