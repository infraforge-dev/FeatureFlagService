using Microsoft.AspNetCore.Http;

namespace FeatureFlag.Domain.Exceptions;

/// <summary>
/// Thrown when a request parameter fails allowlist or structural validation
/// outside the FluentValidation pipeline (e.g. route parameters).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class FeatureFlagValidationException : FeatureFlagException
{
    public FeatureFlagValidationException(string message)
        : base(message, StatusCodes.Status400BadRequest) { }
}
