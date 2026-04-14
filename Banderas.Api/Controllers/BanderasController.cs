using Banderas.Api.Helpers;
using Banderas.Application.DTOs;
using Banderas.Application.Interfaces;
using Banderas.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Banderas.Api.Controllers;

[ApiController]
[Route("api/flags")]
public sealed class BanderasController : ControllerBase
{
    private readonly IBanderasService _service;
    private readonly IValidator<CreateFlagRequest> _createValidator;
    private readonly IValidator<UpdateFlagRequest> _updateValidator;

    public BanderasController(
        IBanderasService service,
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
    [ProducesResponseType<IEnumerable<FlagResponse>>(
        StatusCodes.Status200OK,
        Description = "The list of feature flags for the specified environment."
    )]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] EnvironmentType environment,
        CancellationToken ct
    )
    {
        IReadOnlyList<FlagResponse> flags = await _service.GetAllFlagsAsync(environment, ct);
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
    [HttpGet("{name}", Name = nameof(GetByNameAsync))]
    [ProducesResponseType<FlagResponse>(
        StatusCodes.Status200OK,
        Description = "The requested feature flag."
    )]
    [ProducesResponseType(
        StatusCodes.Status404NotFound,
        Description = "No flag with the given name exists in the specified environment."
    )]
    public async Task<IActionResult> GetByNameAsync(
        string name,
        [FromQuery] EnvironmentType environment,
        CancellationToken ct
    )
    {
        RouteParameterGuard.ValidateName(name);
        FlagResponse flag = await _service.GetFlagAsync(name, environment, ct);
        return Ok(flag);
    }

    /// <summary>
    /// Creates a new feature flag.
    /// </summary>
    /// <param name="request">The flag creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Flag created successfully. Returns the created flag.</response>
    /// <response code="400">Validation failed. See the errors collection for details.</response>
    [HttpPost]
    [ProducesResponseType<FlagResponse>(
        StatusCodes.Status201Created,
        Description = "The newly created feature flag."
    )]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        Description = "One or more validation errors. See the errors field for details."
    )]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateFlagRequest request,
        CancellationToken ct
    )
    {
        ValidationResult validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        FlagResponse created = await _service.CreateFlagAsync(request, ct);
        return CreatedAtRoute(
            nameof(GetByNameAsync),
            new { name = created.Name, environment = created.Environment },
            created
        );
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
    [ProducesResponseType(
        StatusCodes.Status204NoContent,
        Description = "Flag updated successfully."
    )]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        Description = "One or more validation errors. See the errors field for details."
    )]
    [ProducesResponseType(
        StatusCodes.Status404NotFound,
        Description = "No flag with the given name exists in the specified environment."
    )]
    public async Task<IActionResult> UpdateAsync(
        string name,
        [FromQuery] EnvironmentType environment,
        [FromBody] UpdateFlagRequest request,
        CancellationToken ct
    )
    {
        RouteParameterGuard.ValidateName(name);
        ValidationResult validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        await _service.UpdateFlagAsync(name, environment, request, ct);
        return NoContent();
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
    [ProducesResponseType(
        StatusCodes.Status204NoContent,
        Description = "Flag archived successfully."
    )]
    [ProducesResponseType(
        StatusCodes.Status404NotFound,
        Description = "No flag with the given name exists in the specified environment."
    )]
    public async Task<IActionResult> ArchiveAsync(
        string name,
        [FromQuery] EnvironmentType environment,
        CancellationToken ct
    )
    {
        RouteParameterGuard.ValidateName(name);
        await _service.ArchiveFlagAsync(name, environment, ct);
        return NoContent();
    }
}
