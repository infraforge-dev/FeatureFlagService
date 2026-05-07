using Banderas.Application.DTOs;
using FluentValidation;

namespace Banderas.Application.Validators;

public sealed class UpdateFlagRequestValidator : AbstractValidator<UpdateFlagRequest>
{
    public UpdateFlagRequestValidator(StrategyConfigFactory configFactory)
    {
        var rules = new StrategyConfigRules(configFactory);

        RuleFor(x => x.StrategyType)
            .IsInEnum()
            .WithMessage("StrategyType must be a valid value (None, Percentage, or RoleBased).");

        // StrategyConfig: size limit first, then cross-field rules
        RuleFor(x => x.StrategyConfig)
            .MaximumLength(2000)
            .WithMessage("StrategyConfig must not exceed 2000 characters.");

        // Cross-field validation: delegate to StrategyConfigFactory via StrategyConfigRules
        RuleFor(x => x.StrategyConfig)
            .Must((request, config) => rules.BeValidStrategyConfig(request.StrategyType, config))
            .WithMessage(
                "StrategyConfig is invalid for the specified StrategyType. "
                    + "Percentage requires a 'percentage' field (1-100). "
                    + "RoleBased requires a non-empty 'roles' array. "
                    + "None requires no config."
            );
    }
}
