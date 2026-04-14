using Bandera.Domain.Enums;

namespace Bandera.Domain.ValueObjects;

public sealed class FeatureEvaluationContext : IEquatable<FeatureEvaluationContext>
{
    public string UserId { get; }
    public IReadOnlyList<string> UserRoles { get; }
    public EnvironmentType Environment { get; }

    public FeatureEvaluationContext(
        string userId,
        IEnumerable<string> userRoles,
        EnvironmentType environment
    )
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(environment) || environment == EnvironmentType.None)
        {
            throw new ArgumentException(
                "A valid environment must be specified.",
                nameof(environment)
            );
        }

        UserId = userId;
        UserRoles = (userRoles ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        Environment = environment;
    }

    public bool Equals(FeatureEvaluationContext? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return UserId == other.UserId
            && Environment == other.Environment
            && UserRoles.SequenceEqual(other.UserRoles);
    }

    public override bool Equals(object? obj) => Equals(obj as FeatureEvaluationContext);

    public override int GetHashCode() =>
        HashCode.Combine(UserId, Environment, UserRoles.Aggregate(0, HashCode.Combine));
}
