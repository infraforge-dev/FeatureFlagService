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
            .WithMessage(FlagValidationMessage.StrategyTypeInvalid.Message);

        // StrategyConfig: size limit first, then cross-field rules
        RuleFor(x => x.StrategyConfig)
            .MaximumLength(2000)
            .WithMessage(FlagValidationMessage.StrategyConfigTooLong.Message);

        // Cross-field validation: delegate to StrategyConfigFactory via StrategyConfigRules
        RuleFor(x => x.StrategyConfig)
            .Must((request, config) => rules.BeValidStrategyConfig(request.StrategyType, config))
            .WithMessage(FlagValidationMessage.StrategyConfigInvalid.Message);

        // Description: null = no change to existing value. Empty string is accepted
        // and the service maps it to null (DD-7). Non-null content is bounded ≤500.
        RuleFor(x => x.Description!)
            .MaximumLength(500)
            .WithMessage(FlagValidationMessage.DescriptionTooLong.Message)
            .When(x => x.Description is not null);

        // Tags: null = no change. RuleForEach already skips when null. The count rule
        // also short-circuits on null.
        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 20)
            .WithMessage(FlagValidationMessage.TagsTooMany.Message);

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

        // Variations: null is tolerated (means "no change"); when present, all seven
        // DD-2 invariants apply, including the non-empty rule (empty array is rejected).
        RuleFor(x => x.Variations).ApplyMenuRules();
    }
}
