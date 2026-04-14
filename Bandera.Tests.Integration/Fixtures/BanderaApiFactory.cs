using Bandera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Bandera.Tests.Integration.Fixtures;

public sealed class BanderaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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
            services.RemoveAll<DbContextOptions<BanderaDbContext>>();
            services.RemoveAll<BanderaDbContext>();

            services.AddDbContext<BanderaDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString())
            );
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _ = base.CreateClient(FactoryClientOptions);

        using IServiceScope scope = Services.CreateScope();
        BanderaDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<BanderaDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
