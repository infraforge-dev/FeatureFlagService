using FeatureFlag.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlag.Infrastructure.Persistence;

public sealed class FeatureFlagDbContext : DbContext
{
    public FeatureFlagDbContext(DbContextOptions<FeatureFlagDbContext> options)
        : base(options) { }

    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FeatureFlagDbContext).Assembly);
    }
}
