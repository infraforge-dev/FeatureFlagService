using Bandera.Domain.Entities;
using Bandera.Domain.Enums;

namespace Bandera.Domain.Interfaces;

public interface IBanderaRepository
{
    Task<Flag?> GetByNameAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns true if a non-archived flag with the given name and environment
    /// already exists in the store.
    /// </summary>
    Task<bool> ExistsAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<Flag>> GetAllAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task AddAsync(Flag flag, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
