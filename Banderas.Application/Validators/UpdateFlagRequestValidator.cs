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

        // Description: null = no change to existing value. Empty string is accepted
        // and the service maps it to null (DD-7). Non-null content is bounded ≤500.
        RuleFor(x => x.Description!)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.")
            .When(x => x.Description is not null);

        // Tags: null = no change. RuleForEach already skips when null. The count rule
        // also short-circuits on null.
        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 20)
            .WithMessage("Tags may not contain more than 20 entries.");

        RuleForEach(x => x.Tags)
            .MaximumLength(50)
            .WithMessage("Each tag must not exceed 50 characters.")
            .Must(tag =>
                System.Text.RegularExpressions.Regex.IsMatch(
                    (InputSanitizer.Clean(tag) ?? string.Empty).ToLowerInvariant(),
                    @"^[a-z0-9\-_]+$"
                )
            )
            .WithMessage(
                "Tags may only contain lowercase letters, numbers, hyphens, and underscores."
            );
    }
}
