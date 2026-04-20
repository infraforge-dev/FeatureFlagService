using Banderas.Application.AI;
using Banderas.Application.DTOs;
using Banderas.Infrastructure.Persistence;
using Banderas.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Banderas.Tests.Integration.Fixtures;

public sealed class BanderasApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    private static readonly WebApplicationFactoryClientOptions FactoryClientOptions = new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Azure:KeyVaultUri"] = "" }
            );
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BanderasDbContext>>();
            services.RemoveAll<BanderasDbContext>();

            services.AddDbContext<BanderasDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString())
            );

            // Semantic Kernel and Azure OpenAI are not registered in Testing.
            // Provide a deterministic stub so integration tests don't hit Azure.
            services.AddScoped<IAiFlagAnalyzer, StubAiFlagAnalyzer>();
        });
    }

    private sealed class StubAiFlagAnalyzer : IAiFlagAnalyzer
    {
        public Task<FlagHealthAnalysisResponse> AnalyzeAsync(
            IReadOnlyList<FlagResponse> flags,
            int stalenessThresholdDays,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(
                new FlagHealthAnalysisResponse
                {
                    Summary = $"{flags.Count} flag(s) analyzed.",
                    Flags = flags
                        .Select(f => new FlagAssessment
                        {
                            Name = f.Name,
                            Status = "Healthy",
                            Reason = "Stub assessment.",
                            Recommendation = "No action required.",
                        })
                        .ToList(),
                    AnalyzedAt = DateTimeOffset.UtcNow,
                    StalenessThresholdDays = stalenessThresholdDays,
                }
            );
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _ = base.CreateClient(FactoryClientOptions);

        using IServiceScope scope = Services.CreateScope();
        BanderasDbContext dbContext = scope.ServiceProvider.GetRequiredService<BanderasDbContext>();
        await dbContext.Database.MigrateAsync();

        DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(reset: false);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
