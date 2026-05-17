using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Banderas.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Banderas.Tests.Integration.Fixtures;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected HttpClient Client { get; }

    protected static JsonSerializerOptions JsonOptions { get; } =
        new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

    private readonly BanderasApiFactory _factory;

    protected IntegrationTestBase(BanderasApiFactory factory)
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
        BanderasDbContext dbContext = scope.ServiceProvider.GetRequiredService<BanderasDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM flags");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected static void AssertProblemContentType(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    protected static async Task<JsonDocument> ReadRawJsonAsync(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    protected async Task<ProblemDetails> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus
    )
    {
        AssertProblemContentType(response);

        using JsonDocument doc = await ReadRawJsonAsync(response);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("type", out _).Should().BeTrue("ProblemDetails must contain 'type'");
        root.TryGetProperty("title", out _).Should().BeTrue("ProblemDetails must contain 'title'");
        root.TryGetProperty("status", out _)
            .Should()
            .BeTrue("ProblemDetails must contain 'status'");
        root.TryGetProperty("detail", out _)
            .Should()
            .BeTrue("ProblemDetails must contain 'detail'");

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

        using JsonDocument doc = await ReadRawJsonAsync(response);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("errors", out _)
            .Should()
            .BeTrue("ValidationProblemDetails must contain 'errors'");

        ValidationProblemDetails? body =
            await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be((int)expectedStatus);
        return body;
    }
}
