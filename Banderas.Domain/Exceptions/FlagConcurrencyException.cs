using Banderas.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Banderas.Domain.Exceptions;

/// <summary>
/// Thrown when a flag was modified by another transaction between load and save.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class FlagConcurrencyException : BanderasException
{
    public FlagConcurrencyException(string flagName, EnvironmentType environment)
        : base(
            $"The feature flag '{flagName}' in {environment} was modified by another request. Reload and try again.",
            StatusCodes.Status409Conflict
        ) { }

    public FlagConcurrencyException()
        : base(
            "A feature flag was modified by another request. Reload and try again.",
            StatusCodes.Status409Conflict
        ) { }
}
