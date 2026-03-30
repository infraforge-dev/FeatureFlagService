using System.Text.Json;
using FeatureFlag.Application.DTOs;
using FeatureFlag.Domain.Enums;
using FluentValidation;

namespace FeatureFlag.Application.Validators;

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateFlagRequest>
{
    public CreateFlagRequestValidator()
    {
        // Validate the raw property for emptiness and length.
        // Regex runs on the cleaned value — accepts padded input like " dark-mode "
        // which the service layer will sanitize to "dark-mode" before storing.
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Flag name is required.")
            .MaximumLength(100)
            .WithMessage("Flag name must not exceed 100 characters.")
            .Must(name =>
                System.Text.RegularExpressions.Regex.IsMatch(
                    InputSanitizer.Clean(name) ?? string.Empty,
                    @"^[a-zA-Z0-9\-_]+$"
                )
            )
            .WithMessage("Flag name may only contain letters, numbers, hyphens, and underscores.");

        RuleFor(x => x.Environment)
            .NotEqual(EnvironmentType.None)
            .WithMessage(
                "A valid environment must be specified (Development, Staging, or Production)."
            );

        RuleFor(x => x.StrategyType)
            .IsInEnum()
            .WithMessage("StrategyType must be a valid value (None, Percentage, or RoleBased).");

        // StrategyConfig: enforce size limit first, then cross-field structure rules.
        // Note: StrategyConfig is NOT sanitized — it is JSON and must be stored verbatim.
        // Only its length and internal structure are validated.
        RuleFor(x => x.StrategyConfig)
            .MaximumLength(2000)
            .WithMessage("StrategyConfig must not exceed 2000 characters.");

        // When strategy is None, StrategyConfig must be null or empty
        RuleFor(x => x.StrategyConfig)
            .Empty()
            .When(x => x.StrategyType == RolloutStrategy.None)
            .WithMessage("StrategyConfig must be empty when StrategyType is None.");

        // When strategy is Percentage, config must contain a 'percentage' field (1–100)
        RuleFor(x => x.StrategyConfig)
            .NotEmpty()
            .WithMessage("StrategyConfig is required for Percentage strategy.")
            .Must(BeValidPercentageConfig)
            .WithMessage(
                "StrategyConfig for Percentage strategy must be valid JSON with "
                    + "a 'percentage' field between 1 and 100."
            )
            .When(x => x.StrategyType == RolloutStrategy.Percentage);

        // When strategy is RoleBased, config must contain a non-empty 'roles' array
        RuleFor(x => x.StrategyConfig)
            .NotEmpty()
            .WithMessage("StrategyConfig is required for RoleBased strategy.")
            .Must(BeValidRoleConfig)
            .WithMessage(
                "StrategyConfig for RoleBased strategy must be valid JSON with "
                    + "a non-empty 'roles' array."
            )
            .When(x => x.StrategyType == RolloutStrategy.RoleBased);
    }

    private static bool BeValidPercentageConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            return false;
        }

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("percentage", out JsonElement prop))
            {
                return false;
            }

            if (!prop.TryGetInt32(out int percentage))
            {
                return false;
            }

            return percentage >= 1 && percentage <= 100;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool BeValidRoleConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
        {
            return false;
        }

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("roles", out JsonElement prop))
            {
                return false;
            }

            if (prop.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return false;
            }

            return prop.GetArrayLength() > 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
