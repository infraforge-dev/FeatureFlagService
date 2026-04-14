using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bandera.Infrastructure.Persistence;

// Gives dotnet ef a deterministic path to construct the DbContext for migrations,
// independent of ASPNETCORE_ENVIRONMENT. The hardcoded connection string is intentional —
// this factory is only ever used by design-time tooling, never at runtime.
public sealed class BanderaDbContextFactory : IDesignTimeDbContextFactory<BanderaDbContext>
{
    public BanderaDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<BanderaDbContext> options = new DbContextOptionsBuilder<BanderaDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=featureflags;Username=postgres;Password=postgres"
            )
            .Options;

        return new BanderaDbContext(options);
    }
}
