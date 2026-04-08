using FeatureFlag.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace FeatureFlag.Tests.Integration.Fixtures;

public sealed class FeatureFlagApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    private static readonly WebApplicationFactoryClientOptions FactoryClientOptions = new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FeatureFlagDbContext>>();
            services.RemoveAll<FeatureFlagDbContext>();

            services.AddDbContext<FeatureFlagDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString())
            );
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _ = base.CreateClient(FactoryClientOptions);

        using IServiceScope scope = Services.CreateScope();
        FeatureFlagDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<FeatureFlagDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
