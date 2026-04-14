using Banderas.Infrastructure.Persistence;
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

            List<Banderas.Domain.Entities.Flag> seededFlags = await dbContext
                .Flags.OrderBy(f => f.Environment)
                .ThenBy(f => f.Name)
                .ToListAsync();

            seededFlags.Should().HaveCount(6);
            seededFlags.Should().OnlyContain(flag => flag.IsSeeded);
            seededFlags
                .Select(flag => (flag.Name, flag.Environment))
                .Should()
                .BeEquivalentTo([
                    ("beta-features", Domain.Enums.EnvironmentType.Development),
                    ("dark-mode", Domain.Enums.EnvironmentType.Development),
                    ("maintenance-mode", Domain.Enums.EnvironmentType.Development),
                    ("new-dashboard", Domain.Enums.EnvironmentType.Development),
                    ("dark-mode", Domain.Enums.EnvironmentType.Staging),
                    ("new-dashboard", Domain.Enums.EnvironmentType.Staging),
                ]);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }
}
