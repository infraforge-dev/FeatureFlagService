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
            .WithMessage(FlagValidationMessage.NameRequired.Message)
            .MaximumLength(100)
            .WithMessage(FlagValidationMessage.NameTooLong.Message)
            .Must(name =>
                System.Text.RegularExpressions.Regex.IsMatch(
                    InputSanitizer.Clean(name) ?? string.Empty,
                    @"^[a-zA-Z0-9\-_]+$"
                )
            )
            .WithMessage(FlagValidationMessage.NameInvalidCharacters.Message);

        RuleFor(x => x.Environment)
            .Must(EnvironmentRules.IsValid)
            .WithMessage(EnvironmentRules.InvalidEnvironmentMessage);

        RuleFor(x => x.StrategyType)
            .IsInEnum()
            .WithMessage(FlagValidationMessage.StrategyTypeInvalid.Message);

        // StrategyConfig: enforce size limit first, then cross-field structure rules.
        // Note: StrategyConfig is NOT sanitized — it is JSON and must be stored verbatim.
        // Only its length and internal structure are validated.
        RuleFor(x => x.StrategyConfig)
            .MaximumLength(2000)
            .WithMessage(FlagValidationMessage.StrategyConfigTooLong.Message);

        // Cross-field validation: delegate to StrategyConfigFactory via StrategyConfigRules
        RuleFor(x => x.StrategyConfig)
            .Must((request, config) => rules.BeValidStrategyConfig(request.StrategyType, config))
            .WithMessage(FlagValidationMessage.StrategyConfigInvalid.Message);

        // Description: optional, ≤500 characters. Length runs on the raw value
        // (consistent with how Name is handled).
        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage(FlagValidationMessage.DescriptionTooLong.Message);

        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 20)
            .WithMessage(FlagValidationMessage.TagsTooMany.Message);

        // RuleForEach skips when the collection is null. Length runs on the raw value;
        // char-class runs on the cleaned + lowercased projection so padded/mixed-case
        // input the service will normalize ("Checkout", " checkout ") is accepted.
        RuleForEach(x => x.Tags)
            .MaximumLength(50)
            .WithMessage(FlagValidationMessage.TagTooLong.Message)
            .Must(tag =>
                System.Text.RegularExpressions.Regex.IsMatch(
                    (InputSanitizer.Clean(tag) ?? string.Empty).ToLowerInvariant(),
                    @"^[a-z0-9\-_]+$"
                )
            )
            .WithMessage(FlagValidationMessage.TagInvalidCharacters.Message);

        // Variations: required + non-empty + all seven DD-2 invariants.
        // Both rules surface under "Variations" as field-level 400 messages.
        RuleFor(x => (IReadOnlyList<DTOs.VariationRequest>?)x.Variations)
            .NotNull()
            .WithMessage(VariationValidationMessage.Required.Message)
            .ApplyMenuRules();
    }
}
