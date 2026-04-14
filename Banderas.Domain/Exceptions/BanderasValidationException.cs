using Microsoft.AspNetCore.Http;

namespace Banderas.Domain.Exceptions;

/// <summary>
/// Thrown when a request parameter fails allowlist or structural validation
/// outside the FluentValidation pipeline (e.g. route parameters).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class BanderasValidationException : BanderasException
{
    public BanderasValidationException(string message)
        : base(message, StatusCodes.Status400BadRequest) { }
}
