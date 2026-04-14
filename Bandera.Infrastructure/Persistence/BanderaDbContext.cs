using Bandera.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bandera.Infrastructure.Persistence;

public sealed class BanderaDbContext : DbContext
{
    public BanderaDbContext(DbContextOptions<BanderaDbContext> options)
        : base(options) { }

    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BanderaDbContext).Assembly);
    }
}
