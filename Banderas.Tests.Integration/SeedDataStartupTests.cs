using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.ValueObjects;
using Banderas.Infrastructure.Persistence;
using Banderas.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banderas.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SeedDataStartupTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApplicationStartup_SeedsBaselineFlagsAsync()
    {
        var factory = new Fixtures.BanderasApiFactory();
        await factory.InitializeAsync();

        try
        {
            using IServiceScope scope = factory.Services.CreateScope();
            BanderasDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<BanderasDbContext>();

            List<Flag> seededFlags = await dbContext
                .Flags.Where(f => EF.Property<bool>(f, "IsSeeded"))
                .OrderBy(f => f.Environment)
                .ThenBy(f => f.Name)
                .ToListAsync();

            seededFlags.Should().HaveCount(6);
            seededFlags
                .Select(flag => (flag.Name, flag.Environment))
                .Should()
                .BeEquivalentTo([
                    ("beta-features", EnvironmentType.Development),
                    ("dark-mode", EnvironmentType.Development),
                    ("maintenance-mode", EnvironmentType.Development),
                    ("new-dashboard", EnvironmentType.Development),
                    ("dark-mode", EnvironmentType.Staging),
                    ("new-dashboard", EnvironmentType.Staging),
                ]);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeedReset_DoesNotDeleteManuallyCreatedFlagsAsync()
    {
        var factory = new Fixtures.BanderasApiFactory();
        await factory.InitializeAsync();

        try
        {
            using IServiceScope scope = factory.Services.CreateScope();
            BanderasDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<BanderasDbContext>();

            // Insert a manual flag in a slot not occupied by the seed manifest.
            var manualFlag = new Flag(
                "manual-only",
                EnvironmentType.Production,
                isEnabled: true,
                RolloutStrategy.None,
                strategyConfig: StrategyConfig.Create(RolloutStrategy.None, "{}"),
                variations:
                [
                    new("off", VariationKind.Boolean, "false"),
                    new("on", VariationKind.Boolean, "true"),
                ]
            );
            await dbContext.Flags.AddAsync(manualFlag);
            await dbContext.SaveChangesAsync();

            DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(reset: true);

            Flag? survivor = await dbContext
                .Flags.AsNoTracking()
                .SingleOrDefaultAsync(f =>
                    f.Name == "manual-only" && f.Environment == EnvironmentType.Production
                );

            survivor.Should().NotBeNull();

            int seededCount = await dbContext
                .Flags.Where(f => EF.Property<bool>(f, "IsSeeded"))
                .CountAsync();
            seededCount.Should().Be(6);

            int manualCount = await dbContext
                .Flags.Where(f => !EF.Property<bool>(f, "IsSeeded"))
                .CountAsync();
            manualCount.Should().Be(1);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }
}
