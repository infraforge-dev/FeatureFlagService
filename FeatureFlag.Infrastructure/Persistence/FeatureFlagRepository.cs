using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;
using FeatureFlag.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlag.Infrastructure.Persistence;

public sealed class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly FeatureFlagDbContext _context;

    public FeatureFlagRepository(FeatureFlagDbContext context)
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

    public async Task AddAsync(Flag flag, CancellationToken ct = default)
    {
        await _context.Flags.AddAsync(flag, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
