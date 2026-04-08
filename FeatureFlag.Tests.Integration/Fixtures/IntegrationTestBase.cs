using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FeatureFlag.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FeatureFlag.Tests.Integration.Fixtures;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected HttpClient Client { get; }

    protected static JsonSerializerOptions JsonOptions { get; } =
        new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

    private readonly FeatureFlagApiFactory _factory;

    protected IntegrationTestBase(FeatureFlagApiFactory factory)
    {
        _factory = factory;
        Client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );
    }

    public async Task InitializeAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FeatureFlagDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<FeatureFlagDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM flags");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected static void AssertProblemContentType(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    protected async Task<ProblemDetails> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus
    )
    {
        AssertProblemContentType(response);

        ProblemDetails? body = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body!.Status.Should().Be((int)expectedStatus);
        return body;
    }

    protected async Task<ValidationProblemDetails> ReadValidationProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus = HttpStatusCode.BadRequest
    )
    {
        AssertProblemContentType(response);

        ValidationProblemDetails? body =
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be((int)expectedStatus);
        return body;
    }
}
