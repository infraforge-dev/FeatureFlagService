using Bandera.Application.DTOs;
using Bandera.Domain.Enums;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Interfaces;

public interface IBanderaService
{
    Task<FlagResponse> GetFlagAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task<bool> IsEnabledAsync(
        string flagName,
        FeatureEvaluationContext context,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<FlagResponse>> GetAllFlagsAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task<FlagResponse> CreateFlagAsync(CreateFlagRequest request, CancellationToken ct = default);
    Task UpdateFlagAsync(
        string name,
        EnvironmentType environment,
        UpdateFlagRequest request,
        CancellationToken ct = default
    );
    Task ArchiveFlagAsync(string name, EnvironmentType environment, CancellationToken ct = default);
}
