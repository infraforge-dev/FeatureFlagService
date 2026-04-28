using System.Net;
using System.Net.Http.Json;
using Banderas.Application.DTOs;
using Banderas.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Banderas.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class AiStartupResilienceTests
    : IClassFixture<AiStartupResilienceTests.MissingAzureOpenAiEndpointFactory>
{
    private readonly HttpClient _client;

    public AiStartupResilienceTests(MissingAzureOpenAiEndpointFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlags_NoAzureOpenAiEndpoint_ReturnsNonAiResponseAsync()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/flags?environment=Development"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        IReadOnlyList<FlagResponse>? body =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<FlagResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostHealth_NoAzureOpenAiEndpoint_Returns503ProblemDetailsAsync()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/flags/health",
            new { }
        );

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        ProblemDetails? body = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        body.Should().NotBeNull();
        body!.Status.Should().Be((int)HttpStatusCode.ServiceUnavailable);
        body.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.6.4");
        body.Title.Should().Be("AI analysis is currently unavailable.");
    }

    public sealed class MissingAzureOpenAiEndpointFactory
        : WebApplicationFactory<Program>,
            IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Staging");

            builder.ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Azure:KeyVaultUri"] = "",
                        ["AzureOpenAI:Endpoint"] = "",
                    }
                );
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BanderasDbContext>>();
                services.RemoveAll<BanderasDbContext>();

                services.AddDbContext<BanderasDbContext>(options =>
                    options.UseNpgsql(_postgres.GetConnectionString())
                );
            });
        }

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            _ = CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                }
            );

            using IServiceScope scope = Services.CreateScope();
            BanderasDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<BanderasDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        public new async Task DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
