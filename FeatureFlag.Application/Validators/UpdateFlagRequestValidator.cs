using FeatureFlag.Application.DTOs;
using FeatureFlag.Domain.Enums;
using FluentValidation;

namespace FeatureFlag.Application.Validators;

public sealed class UpdateFlagRequestValidator : AbstractValidator<UpdateFlagRequest>
{
    public UpdateFlagRequestValidator()
    {
        RuleFor(x => x.StrategyType)
            .IsInEnum()
            .WithMessage("StrategyType must be a valid value (None, Percentage, or RoleBased).");

        // StrategyConfig: size limit first, then cross-field rules
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
            .NotEmpty().WithMessage("StrategyConfig is required for Percentage strategy.")
            .Must(BeValidPercentageConfig)
            .WithMessage(
                "StrategyConfig for Percentage strategy must be valid JSON with " +
                "a 'percentage' field between 1 and 100.")
            .When(x => x.StrategyType == RolloutStrategy.Percentage);

        // When strategy is RoleBased, config must contain a non-empty 'roles' array
        RuleFor(x => x.StrategyConfig)
            .NotEmpty().WithMessage("StrategyConfig is required for RoleBased strategy.")
            .Must(BeValidRoleConfig)
            .WithMessage(
                "StrategyConfig for RoleBased strategy must be valid JSON with " +
                "a non-empty 'roles' array.")
            .When(x => x.StrategyType == RolloutStrategy.RoleBased);
    }

    private static bool BeValidPercentageConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config)) return false;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("percentage", out var prop)) return false;
            if (!prop.TryGetInt32(out var percentage)) return false;
            return percentage >= 1 && percentage <= 100;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }

    private static bool BeValidRoleConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config)) return false;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("roles", out var prop)) return false;
            if (prop.ValueKind != System.Text.Json.JsonValueKind.Array) return false;
            return prop.GetArrayLength() > 0;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }
}
