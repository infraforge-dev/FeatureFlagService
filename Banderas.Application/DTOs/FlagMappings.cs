using Banderas.Domain.Entities;

namespace Banderas.Application.DTOs;

public static class FlagMappings
{
    public static FlagResponse ToResponse(this Flag flag) =>
        new(
            flag.Id,
            flag.Name,
            flag.Environment,
            flag.IsEnabled,
            flag.IsArchived,
            flag.StrategyType,
            flag.StrategyConfig,
            flag.CreatedAt,
            flag.UpdatedAt
        );
}
