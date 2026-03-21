namespace FeatureFlag.Domain.ValueObjects;

public sealed class FeatureEvaluationContext
{
    public string UserId { get; }
    public IReadOnlyList<string> UserRoles { get; }
    public Enums.EnvironmentType Environment { get; }

    public FeatureEvaluationContext(string userId, IEnumerable<string> userRoles, Enums.EnvironmentType environment)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        UserId = userId;
        UserRoles = (userRoles ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        Environment = environment;
    }
}