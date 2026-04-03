using System.Text.RegularExpressions;
using FeatureFlag.Domain.Exceptions;

namespace FeatureFlag.Api.Helpers;

/// <summary>
/// Guards route parameters against values that do not conform to the
/// flag name allowlist. Called at the top of controller actions that
/// accept a {name} route segment before any service logic runs.
/// </summary>
public static class RouteParameterGuard
{
    private static readonly Regex NamePattern = new(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);

    /// <summary>
    /// Throws <see cref="FeatureFlagValidationException"/> if <paramref name="name"/>
    /// contains characters outside the allowed set (letters, digits, hyphens,
    /// underscores). Callers should return the resulting 400 response immediately.
    /// </summary>
    /// <exception cref="FeatureFlagValidationException">
    /// Thrown when <paramref name="name"/> fails the allowlist check.
    /// </exception>
    public static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!NamePattern.IsMatch(name))
        {
            throw new FeatureFlagValidationException(
                "Flag name may only contain letters, numbers, hyphens, and underscores."
            );
        }
    }
}
