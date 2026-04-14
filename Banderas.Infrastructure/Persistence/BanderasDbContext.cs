using Banderas.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Banderas.Infrastructure.Persistence;

public sealed class BanderasDbContext : DbContext
{
    public BanderasDbContext(DbContextOptions<BanderasDbContext> options)
        : base(options) { }

    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BanderasDbContext).Assembly);
    }
}
