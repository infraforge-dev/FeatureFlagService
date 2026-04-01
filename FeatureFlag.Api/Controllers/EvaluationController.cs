using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Domain.ValueObjects;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlag.Api.Controllers;

[ApiController]
[Route("api/evaluate")]
public sealed class EvaluationController : ControllerBase
{
    private readonly IFeatureFlagService _service;
    private readonly IValidator<EvaluationRequest> _validator;

    public EvaluationController(IFeatureFlagService service, IValidator<EvaluationRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost]
    public async Task<IActionResult> Evaluate(
        [FromBody] EvaluationRequest request,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        try
        {
            var context = new FeatureEvaluationContext(
                request.UserId,
                request.UserRoles,
                request.Environment);

            var isEnabled = await _service.IsEnabledAsync(request.FlagName, context, ct);
            return Ok(new { isEnabled });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { error = e.Message });
        }
    }
}
