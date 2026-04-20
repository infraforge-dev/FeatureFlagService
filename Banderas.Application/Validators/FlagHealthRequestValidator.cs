using Banderas.Application.AI;
using Banderas.Application.DTOs;
using FluentValidation;

namespace Banderas.Application.Validators;

public sealed class FlagHealthRequestValidator : AbstractValidator<FlagHealthRequest>
{
    public FlagHealthRequestValidator()
    {
        When(
            x => x.StalenessThresholdDays.HasValue,
            () =>
            {
                RuleFor(x => x.StalenessThresholdDays!.Value)
                    .InclusiveBetween(
                        FlagHealthConstants.MinStalenessThresholdDays,
                        FlagHealthConstants.MaxStalenessThresholdDays
                    )
                    .WithMessage(
                        $"Staleness threshold must be between "
                            + $"{FlagHealthConstants.MinStalenessThresholdDays} and "
                            + $"{FlagHealthConstants.MaxStalenessThresholdDays} days."
                    );
            }
        );
    }
}
