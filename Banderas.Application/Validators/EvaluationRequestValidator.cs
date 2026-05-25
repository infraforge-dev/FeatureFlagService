using Banderas.Application.DTOs;
using Banderas.Application.Validation;
using FluentValidation;

namespace Banderas.Application.Validators;

public sealed class EvaluationRequestValidator : AbstractValidator<EvaluationRequest>
{
    public EvaluationRequestValidator()
    {
        RuleFor(x => x.FlagName)
            .NotEmpty()
            .WithMessage(FlagValidationMessage.EvaluationFlagNameRequired.Message)
            .MaximumLength(100)
            .WithMessage(FlagValidationMessage.EvaluationFlagNameTooLong.Message);

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(FlagValidationMessage.EvaluationUserIdRequired.Message)
            .MaximumLength(256)
            .WithMessage(FlagValidationMessage.EvaluationUserIdTooLong.Message);

        RuleFor(x => x.Environment)
            .Must(EnvironmentRules.IsValid)
            .WithMessage(EnvironmentRules.InvalidEnvironmentMessage);

        // UserRoles: not null, max 50 entries, each role max 100 chars after sanitization
        RuleFor(x => x.UserRoles)
            .NotNull()
            .WithMessage(FlagValidationMessage.EvaluationUserRolesNull.Message);

        // .Take(51).Count() > 50 short-circuits at 51 — avoids enumerating the full collection
        RuleFor(x => x.UserRoles)
            .Must(roles => roles.Take(51).Count() <= 50)
            .WithMessage(FlagValidationMessage.EvaluationUserRolesTooMany.Message)
            .When(x => x.UserRoles is not null);

        // Validate cleaned length per role — consistent with service-layer sanitization behavior
        RuleForEach(x => x.UserRoles)
            .Must(role => (InputSanitizer.Clean(role)?.Length ?? 0) <= 100)
            .WithMessage(FlagValidationMessage.EvaluationRoleTooLong.Message)
            .When(x => x.UserRoles is not null);
    }
}
