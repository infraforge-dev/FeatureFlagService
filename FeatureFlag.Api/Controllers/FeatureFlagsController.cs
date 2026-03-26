using FeatureFlag.Application.DTOs;
using FeatureFlag.Application.Interfaces;
using FeatureFlag.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlag.Api.Controllers;

[ApiController]
[Route("api/flags")]
public sealed class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _service;

    public FeatureFlagsController(IFeatureFlagService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] EnvironmentType environment,
        CancellationToken ct)
    {
        var flags = await _service.GetAllFlagsAsync(environment, ct);
        return Ok(flags);
    }

    [HttpGet("{name}")]
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

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateFlagRequest request,
        CancellationToken ct)
    {
        var created = await _service.CreateFlagAsync(request, ct);
        return CreatedAtAction(
            nameof(GetByName),
            new { name = created.Name, environment = created.Environment },
            created);
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(
        string name,
        [FromQuery] EnvironmentType environment,
        [FromBody] UpdateFlagRequest request,
        CancellationToken ct)
    {
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

    [HttpDelete("{name}")]
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
