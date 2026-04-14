using Microsoft.AspNetCore.Http;

namespace Bandera.Domain.Exceptions;

/// <summary>
/// Thrown when a request parameter fails allowlist or structural validation
/// outside the FluentValidation pipeline (e.g. route parameters).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class BanderaValidationException : BanderaException
{
    public BanderaValidationException(string message)
        : base(message, StatusCodes.Status400BadRequest) { }
}
