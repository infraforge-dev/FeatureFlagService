using System.Net;
using System.Net.Http.Json;
using Banderas.Application.DTOs;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Banderas.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class AnalyzeFlagsEndpointTests : IntegrationTestBase
{
    private readonly BanderasApiFactory _factory;

    public AnalyzeFlagsEndpointTests(BanderasApiFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostHealth_EmptyBody_Returns200WithStructuredResponseAsync()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/flags/health", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        FlagHealthAnalysisResponse? body =
            await response.Content.ReadFromJsonAsync<FlagHealthAnalysisResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Summary.Should().NotBeNullOrWhiteSpace();
        body.StalenessThresholdDays.Should().Be(30);
        body.Flags.Should().NotBeNull();
        body.AnalyzedAt.Should().NotBe(default);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostHealth_WithThreshold_UsesSuppliedThresholdAsync()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags/health",
            new { stalenessThresholdDays = 7 }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        FlagHealthAnalysisResponse? body =
            await response.Content.ReadFromJsonAsync<FlagHealthAnalysisResponse>(JsonOptions);

        body!.StalenessThresholdDays.Should().Be(7);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostHealth_ThresholdZero_Returns400Async()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags/health",
            new { stalenessThresholdDays = 0 }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertProblemContentType(response);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostHealth_ThresholdTooHigh_Returns400Async()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags/health",
            new { stalenessThresholdDays = 366 }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertProblemContentType(response);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostHealth_AnalyzedAt_IsUtcAsync()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/flags/health", new { });

        FlagHealthAnalysisResponse? body =
            await response.Content.ReadFromJsonAsync<FlagHealthAnalysisResponse>(JsonOptions);

        body!.AnalyzedAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostHealth_WhenAiAnalyzerThrows_Returns503ProblemDetailsAsync()
    {
        using HttpClient client = _factory.CreateClientWithThrowingAiFlagAnalyzer();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/flags/health", new { });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        AssertProblemContentType(response);

        ProblemDetails? body = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body!.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.6.4");
        body.Title.Should().Be("Flag health analysis is currently unavailable.");
        body.Status.Should().Be((int)HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlags_WhenAiAnalyzerThrows_Returns200Async()
    {
        using HttpClient client = _factory.CreateClientWithThrowingAiFlagAnalyzer();

        HttpResponseMessage response = await client.GetAsync("/api/flags?environment=Development");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
