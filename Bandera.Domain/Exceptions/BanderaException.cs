namespace Bandera.Domain.Exceptions;

/// <summary>
/// Base class for all domain exceptions in Bandera.
/// Carries the HTTP status code that the middleware will use
/// when building the ProblemDetails response.
/// </summary>
public abstract class BanderaException : Exception
{
    /// <summary>
    /// The HTTP status code this exception maps to.
    /// </summary>
    public int StatusCode { get; }

    protected BanderaException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
