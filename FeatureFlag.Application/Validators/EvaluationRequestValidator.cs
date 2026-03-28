using FeatureFlag.Application.DTOs;
using FeatureFlag.Domain.Enums;
using FluentValidation;

namespace FeatureFlag.Application.Validators;

public sealed class EvaluationRequestValidator : AbstractValidator<EvaluationRequest>
{
    public EvaluationRequestValidator()
    {
        RuleFor(x => x.FlagName)
            .NotEmpty().WithMessage("FlagName is required.")
            .MaximumLength(100).WithMessage("FlagName must not exceed 100 characters.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.")
            .MaximumLength(256).WithMessage("UserId must not exceed 256 characters.");

        RuleFor(x => x.Environment)
            .NotEqual(EnvironmentType.None)
            .WithMessage("A valid environment must be specified (Development, Staging, or Production).");

        // UserRoles: not null, max 50 entries, each role max 100 chars after sanitization
        RuleFor(x => x.UserRoles)
            .NotNull()
            .WithMessage("UserRoles must not be null. Pass an empty array if the user has no roles.");

        // .Take(51).Count() > 50 short-circuits at 51 — avoids enumerating the full collection
        RuleFor(x => x.UserRoles)
            .Must(roles => roles.Take(51).Count() <= 50)
            .WithMessage("UserRoles must not exceed 50 entries.")
            .When(x => x.UserRoles is not null);

        // Validate cleaned length per role — consistent with service-layer sanitization behavior
        RuleForEach(x => x.UserRoles)
            .Must(role => (InputSanitizer.Clean(role)?.Length ?? 0) <= 100)
            .WithMessage("Each role must not exceed 100 characters.")
            .When(x => x.UserRoles is not null);
    }
}
