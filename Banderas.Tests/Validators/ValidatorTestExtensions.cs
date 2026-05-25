using Banderas.Application.DTOs;
using FluentValidation;
using FluentValidation.Results;

namespace Banderas.Tests.Validators;

/// <summary>
/// Test-only convenience: ensures the request being validated has a valid default
/// variation menu, so tests that target *non-variation* rules don't have to repeat
/// boilerplate variation payloads. Tests that explicitly exercise variation rules
/// supply their own <c>Variations</c> via <c>request with { Variations = ... }</c>.
/// </summary>
internal static class ValidatorTestExtensions
{
    private static IReadOnlyList<VariationRequest> DefaultMenu() =>
        [new("off", "Boolean", "false"), new("on", "Boolean", "true")];

    public static Task<ValidationResult> ValidateWithDefaultsAsync(
        this IValidator<CreateFlagRequest> validator,
        CreateFlagRequest request
    )
    {
        CreateFlagRequest withMenu =
            request.Variations.Count == 0 ? request with { Variations = DefaultMenu() } : request;
        return validator.ValidateAsync(withMenu);
    }

    public static Task<ValidationResult> ValidateWithDefaultsAsync(
        this IValidator<UpdateFlagRequest> validator,
        UpdateFlagRequest request
    )
    {
        // UpdateFlagRequest's Variations is nullable (null = no change); don't
        // inject defaults — that would change the semantic of the test. Pass
        // through unchanged.
        return validator.ValidateAsync(request);
    }
}
