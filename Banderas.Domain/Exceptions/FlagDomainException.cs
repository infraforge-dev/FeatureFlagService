using Microsoft.AspNetCore.Http;

namespace Banderas.Domain.Exceptions;

/// <summary>
/// Thrown when an operation violates a domain invariant on Flag.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class FlagDomainException : BanderasException
{
    public FlagDomainException(string message)
        : base(message, StatusCodes.Status409Conflict) { }
}
