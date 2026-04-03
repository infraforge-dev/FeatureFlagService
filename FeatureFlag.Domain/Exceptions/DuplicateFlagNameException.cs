using FeatureFlag.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FeatureFlag.Domain.Exceptions;

/// <summary>
/// Thrown when a flag with the given name already exists in the specified
/// environment. Maps to HTTP 409 Conflict.
/// </summary>
public sealed class DuplicateFlagNameException : FeatureFlagException
{
    public DuplicateFlagNameException(string flagName, EnvironmentType environment)
        : base(
            $"A feature flag named '{flagName}' already exists in {environment}.",
            StatusCodes.Status409Conflict
        ) { }
}
