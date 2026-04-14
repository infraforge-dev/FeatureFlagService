namespace Banderas.Domain.Exceptions;

/// <summary>
/// Base class for all domain exceptions in Banderas.
/// Carries the HTTP status code that the middleware will use
/// when building the ProblemDetails response.
/// </summary>
public abstract class BanderasException : Exception
{
    /// <summary>
    /// The HTTP status code this exception maps to.
    /// </summary>
    public int StatusCode { get; }

    protected BanderasException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
