using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Domain.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlag.Api.Controllers;

[ApiController]
[Route("api/evaluate")]
public sealed class EvaluationController : ControllerBase
{
    private readonly IFeatureFlagService _service;
    private readonly IValidator<EvaluationRequest> _validator;

    public EvaluationController(
        IFeatureFlagService service,
        IValidator<EvaluationRequest> validator
    )
    {
        _service = service;
        _validator = validator;
    }

    /// <summary>
    /// Evaluates whether a feature flag is enabled for a given user context.
    /// Evaluation is deterministic — the same user will always receive the same result
    /// for a given flag and strategy configuration.
    /// </summary>
    /// <param name="request">The evaluation context including user identity, roles, and environment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the evaluation result.</response>
    /// <response code="400">Validation failed. See the errors collection for details.</response>
    /// <response code="404">No flag found with the given name in the specified environment.</response>
    [HttpPost]
    [ProducesResponseType<EvaluationResponse>(
        StatusCodes.Status200OK,
        Description = "The evaluation result for the given user context."
    )]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        Description = "One or more validation errors. See the errors field for details."
    )]
    [ProducesResponseType(
        StatusCodes.Status404NotFound,
        Description = "No flag found with the given name exists in the specified environment."
    )]
    public async Task<IActionResult> EvaluateAsync(
        [FromBody] EvaluationRequest request,
        CancellationToken ct
    )
    {
        ValidationResult validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var context = new FeatureEvaluationContext(
            request.UserId,
            request.UserRoles,
            request.Environment
        );

        bool isEnabled = await _service.IsEnabledAsync(request.FlagName, context, ct);
        return Ok(new EvaluationResponse(isEnabled));
    }
}
