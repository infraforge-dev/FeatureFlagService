using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FeatureFlag.Infrastructure.Persistence;

// Gives dotnet ef a deterministic path to construct the DbContext for migrations,
// independent of ASPNETCORE_ENVIRONMENT. The hardcoded connection string is intentional —
// this factory is only ever used by design-time tooling, never at runtime.
public sealed class FeatureFlagDbContextFactory : IDesignTimeDbContextFactory<FeatureFlagDbContext>
{
    public FeatureFlagDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<FeatureFlagDbContext> options =
            new DbContextOptionsBuilder<FeatureFlagDbContext>()
                .UseNpgsql(
                    "Host=localhost;Port=5432;Database=featureflags;Username=postgres;Password=postgres"
                )
                .Options;

        return new FeatureFlagDbContext(options);
    }
}
