using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlag.Api.Controllers;

[ApiController]
[Route("api/flags")]
public sealed class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _service;
    private readonly IValidator<CreateFlagRequest> _createValidator;
    private readonly IValidator<UpdateFlagRequest> _updateValidator;

    public FeatureFlagsController(
        IFeatureFlagService service,
        IValidator<CreateFlagRequest> createValidator,
        IValidator<UpdateFlagRequest> updateValidator
    )
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Retrieves all feature flags for the specified environment.
    /// </summary>
    /// <param name="environment">The target deployment environment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the list of feature flags.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<FlagResponse>>(StatusCodes.Status200OK,
        Description = "The list of feature flags for the specified environment.")]
    public async Task<IActionResult> GetAll(
        [FromQuery] EnvironmentType environment,
        CancellationToken ct)
    {
        var flags = await _service.GetAllFlagsAsync(environment, ct);
        return Ok(flags);
    }

    /// <summary>
    /// Retrieves a single feature flag by name and environment.
    /// </summary>
    /// <param name="name">The unique name of the feature flag.</param>
    /// <param name="environment">The target deployment environment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the feature flag.</response>
    /// <response code="404">No flag found with the given name in the specified environment.</response>
    [HttpGet("{name}")]
    [ProducesResponseType<FlagResponse>(StatusCodes.Status200OK,
        Description = "The requested feature flag.")]
    [ProducesResponseType(StatusCodes.Status404NotFound,
        Description = "No flag with the given name exists in the specified environment.")]
    public async Task<IActionResult> GetByName(
        string name,
        [FromQuery] EnvironmentType environment,
        CancellationToken ct)
    {
        try
        {
            var flag = await _service.GetFlagAsync(name, environment, ct);
            return Ok(flag);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Creates a new feature flag.
    /// </summary>
    /// <param name="request">The flag creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Flag created successfully. Returns the created flag.</response>
    /// <response code="400">Validation failed. See the errors collection for details.</response>
    [HttpPost]
    [ProducesResponseType<FlagResponse>(StatusCodes.Status201Created,
        Description = "The newly created feature flag.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest,
        Description = "One or more validation errors. See the errors field for details.")]
    public async Task<IActionResult> Create(
        [FromBody] CreateFlagRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var created = await _service.CreateFlagAsync(request, ct);
        return CreatedAtAction(
            nameof(GetByName),
            new { name = created.Name, environment = created.Environment },
            created);
    }

    /// <summary>
    /// Updates an existing feature flag's enabled state and rollout strategy.
    /// </summary>
    /// <param name="name">The name of the flag to update.</param>
    /// <param name="environment">The target deployment environment.</param>
    /// <param name="request">The update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Flag updated successfully.</response>
    /// <response code="400">Validation failed. See the errors collection for details.</response>
    /// <response code="404">No flag found with the given name in the specified environment.</response>
    [HttpPut("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent,
        Description = "Flag updated successfully.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest,
        Description = "One or more validation errors. See the errors field for details.")]
    [ProducesResponseType(StatusCodes.Status404NotFound,
        Description = "No flag with the given name exists in the specified environment.")]
    public async Task<IActionResult> Update(
        string name,
        [FromQuery] EnvironmentType environment,
        [FromBody] UpdateFlagRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        try
        {
            await _service.UpdateFlagAsync(name, environment, request, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Archives a feature flag (soft delete). The flag is retained for audit history
    /// but will no longer appear in active flag queries.
    /// </summary>
    /// <param name="name">The name of the flag to archive.</param>
    /// <param name="environment">The target deployment environment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Flag archived successfully.</response>
    /// <response code="404">No flag found with the given name in the specified environment.</response>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent,
        Description = "Flag archived successfully.")]
    [ProducesResponseType(StatusCodes.Status404NotFound,
        Description = "No flag with the given name exists in the specified environment.")]
    public async Task<IActionResult> Archive(
        string name,
        [FromQuery] EnvironmentType environment,
        CancellationToken ct)
    {
        try
        {
            await _service.ArchiveFlagAsync(name, environment, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
