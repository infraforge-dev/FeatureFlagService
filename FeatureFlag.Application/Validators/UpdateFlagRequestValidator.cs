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
            .NotEmpty()
            .WithMessage("StrategyConfig is required for Percentage strategy.")
            .Must(StrategyConfigRules.BeValidPercentageConfig)
            .WithMessage(
                "StrategyConfig for Percentage strategy must be valid JSON with "
                    + "a 'percentage' field between 1 and 100."
            )
            .When(x => x.StrategyType == RolloutStrategy.Percentage);

        // When strategy is RoleBased, config must contain a non-empty 'roles' array
        RuleFor(x => x.StrategyConfig)
            .NotEmpty()
            .WithMessage("StrategyConfig is required for RoleBased strategy.")
            .Must(StrategyConfigRules.BeValidRoleConfig)
            .WithMessage(
                "StrategyConfig for RoleBased strategy must be valid JSON with "
                    + "a non-empty 'roles' array."
            )
            .When(x => x.StrategyType == RolloutStrategy.RoleBased);
    }
}
