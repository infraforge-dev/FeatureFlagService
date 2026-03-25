using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlag.Api.Controllers;

[ApiController]
[Route("api/evaluate")]
public sealed class EvaluationController : ControllerBase
{
    private readonly IFeatureFlagService _service;

    public EvaluationController(IFeatureFlagService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Evaluate(
        [FromBody] EvaluationRequest request,
        CancellationToken ct)
    {
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
