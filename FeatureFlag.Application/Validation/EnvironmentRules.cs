using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Exceptions;

namespace FeatureFlag.Application.Validation;

internal static class EnvironmentRules
{
    internal const string InvalidEnvironmentMessage =
        "A valid environment must be specified (Development, Staging, or Production).";

    internal static bool IsValid(EnvironmentType environment) =>
        Enum.IsDefined(environment) && environment != EnvironmentType.None;

    internal static void RequireValid(EnvironmentType environment)
    {
        if (!IsValid(environment))
        {
            throw new FeatureFlagValidationException(InvalidEnvironmentMessage);
        }
    }
}
