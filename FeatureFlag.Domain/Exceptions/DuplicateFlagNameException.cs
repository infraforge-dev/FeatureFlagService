using Microsoft.AspNetCore.Http;

namespace FeatureFlag.Domain.Exceptions;

/// <summary>
/// Thrown when a flag creation attempt conflicts with an existing flag
/// of the same name in the same environment.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class DuplicateFlagNameException : FeatureFlagException
{
    public DuplicateFlagNameException(string flagName)
        : base(
            $"A feature flag with name '{flagName}' already exists in this environment.",
            StatusCodes.Status409Conflict
        ) { }
}
