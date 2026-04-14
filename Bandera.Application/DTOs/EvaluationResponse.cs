namespace Bandera.Application.DTOs;

/// <summary>
/// The result of a feature flag evaluation for a given user context.
/// </summary>
/// <param name="IsEnabled">Whether the feature flag is enabled for the requesting user.</param>
public sealed record EvaluationResponse(bool IsEnabled);
