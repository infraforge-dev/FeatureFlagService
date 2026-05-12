using Banderas.Application.DTOs;
using Banderas.Application.Validation;
using FluentValidation;

namespace Banderas.Application.Validators;

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateFlagRequest>
{
    public CreateFlagRequestValidator(StrategyConfigFactory configFactory)
    {
        var rules = new StrategyConfigRules(configFactory);

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
            .Must(EnvironmentRules.IsValid)
            .WithMessage(EnvironmentRules.InvalidEnvironmentMessage);

        RuleFor(x => x.StrategyType)
            .IsInEnum()
            .WithMessage("StrategyType must be a valid value (None, Percentage, or RoleBased).");

        // StrategyConfig: enforce size limit first, then cross-field structure rules.
        // Note: StrategyConfig is NOT sanitized — it is JSON and must be stored verbatim.
        // Only its length and internal structure are validated.
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

        // Description: optional, ≤500 characters. Length runs on the raw value
        // (consistent with how Name is handled).
        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 20)
            .WithMessage("Tags may not contain more than 20 entries.");

        // RuleForEach skips when the collection is null. Length runs on the raw value;
        // char-class runs on the cleaned + lowercased projection so padded/mixed-case
        // input the service will normalize ("Checkout", " checkout ") is accepted.
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
