using Banderas.Domain.Enums;

namespace Banderas.Application.DTOs;

/// <summary>
/// Payload for evaluating a feature flag against a user context.
/// </summary>
/// <param name="FlagName">The name of the feature flag to evaluate.</param>
/// <param name="UserId">The unique identifier of the requesting user.</param>
/// <param name="UserRoles">The roles assigned to the requesting user. Used by RoleBased strategy.</param>
/// <param name="Environment">The deployment environment to evaluate the flag in.</param>
public sealed record EvaluationRequest(
    string FlagName,
    string UserId,
    IEnumerable<string> UserRoles,
    EnvironmentType Environment
);
