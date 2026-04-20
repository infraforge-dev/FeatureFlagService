using System.Net;
using System.Net.Http.Json;
using Banderas.Application.DTOs;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Banderas.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class AnalyzeFlagsEndpointTests : IntegrationTestBase
{
    public AnalyzeFlagsEndpointTests(BanderasApiFactory factory)
        : base(factory) { }

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
}
