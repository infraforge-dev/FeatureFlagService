using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Exceptions;
using Bandera.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bandera.Infrastructure.Persistence;

public sealed class BanderaRepository : IBanderaRepository
{
    private readonly BanderaDbContext _context;

    public BanderaRepository(BanderaDbContext context)
    {
        _context = context;
    }

    public async Task<Flag?> GetByNameAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        return await _context
            .Flags.Where(f => f.Name == name && f.Environment == environment && !f.IsArchived)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Flag>> GetAllAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    )
    {
        return await _context
            .Flags.Where(f => f.Environment == environment && !f.IsArchived)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    ) =>
        await _context
            .Flags.Where(f => f.Name == name && f.Environment == environment && !f.IsArchived)
            .AnyAsync(ct);

    public async Task AddAsync(Flag flag, CancellationToken ct = default)
    {
        await _context.Flags.AddAsync(flag, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        // Capture any pending Added flags before saving so we have context
        // if a concurrent request races past the ExistsAsync check and the DB
        // unique constraint fires (Postgres error 23505).
        var pendingAdds = _context
            .ChangeTracker.Entries<Flag>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" }
                && pendingAdds.Count == 1
            )
        {
            throw new DuplicateFlagNameException(pendingAdds[0].Name, pendingAdds[0].Environment);
        }
    }
}
