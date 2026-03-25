using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Application.DTOs;

public sealed record EvaluationRequest(
    string FlagName,
    string UserId,
    IEnumerable<string> UserRoles,
    EnvironmentType Environment
);
