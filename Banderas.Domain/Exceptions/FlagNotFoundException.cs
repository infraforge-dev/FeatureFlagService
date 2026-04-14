using Microsoft.AspNetCore.Http;

namespace Banderas.Domain.Exceptions;

/// <summary>
/// Thrown when a feature flag cannot be found by name and environment.
/// Maps to HTTP 404 Not Found.
/// </summary>
public sealed class FlagNotFoundException : BanderasException
{
    public FlagNotFoundException(string flagName)
        : base($"No feature flag with name '{flagName}' was found.", StatusCodes.Status404NotFound)
    { }
}
