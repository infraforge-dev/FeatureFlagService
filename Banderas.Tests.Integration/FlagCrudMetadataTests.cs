using System.Net;
using System.Net.Http.Json;
using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Banderas.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FlagCrudMetadataTests : IntegrationTestBase
{
    public FlagCrudMetadataTests(BanderasApiFactory factory)
        : base(factory) { }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithDescriptionAndTags_Returns201WithBothOnResponseAsync()
    {
        var payload = new
        {
            Name = "checkout-v2",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Description = "Checkout v2 experiment",
            Tags = new[] { "squad-checkout", "release-q2" },
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FlagResponse body = (await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions))!;
        body.Description.Should().Be("Checkout v2 experiment");
        body.Tags.Should().BeEquivalentTo(["squad-checkout", "release-q2"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithoutMetadata_Returns201WithDefaultsAsync()
    {
        var payload = new
        {
            Name = "minimal-flag",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FlagResponse body = (await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions))!;
        body.Description.Should().BeNull();
        body.Tags.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_NormalizesTags_TrimLowercaseDedupeAsync()
    {
        var payload = new
        {
            Name = "normalize-flag",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Tags = new[] { "Checkout", " checkout ", "CHECKOUT", "Release-Q2" },
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FlagResponse body = (await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions))!;
        body.Tags.Should().Equal("checkout", "release-q2");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_With21Tags_Returns400ValidationProblemDetailsAsync()
    {
        var payload = new
        {
            Name = "too-many-tags",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Tags = Enumerable.Range(1, 21).Select(i => $"tag-{i}").ToArray(),
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails body = await ReadValidationProblemDetailsAsync(response);
        body.Errors.Should().ContainKey("Tags");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithDescriptionOver500Chars_Returns400Async()
    {
        var payload = new
        {
            Name = "long-description",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Description = new string('a', 501),
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails body = await ReadValidationProblemDetailsAsync(response);
        body.Errors.Should().ContainKey("Description");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_TagsNull_PreservesExistingTagsAsync()
    {
        await CreateFlagWithTagsAsync("preserve-tags", ["a", "b"]);

        var updatePayload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Description = (string?)null,
            Tags = (string[]?)null,
        };

        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/preserve-tags?environment=Development",
            updatePayload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        FlagResponse updated = await GetFlagAsync("preserve-tags");
        updated.Tags.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_TagsEmpty_ClearsTagsAsync()
    {
        await CreateFlagWithTagsAsync("clear-tags", ["a", "b"]);

        var updatePayload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Tags = Array.Empty<string>(),
        };

        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/clear-tags?environment=Development",
            updatePayload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        FlagResponse updated = await GetFlagAsync("clear-tags");
        updated.Tags.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_DescriptionEmptyString_ClearsDescriptionAsync()
    {
        await CreateFlagWithDescriptionAsync("clear-description", "old text");

        var updatePayload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Description = "",
        };

        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/clear-description?environment=Development",
            updatePayload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        FlagResponse updated = await GetFlagAsync("clear-description");
        updated.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_DescriptionNonEmpty_ReplacesDescriptionAsync()
    {
        await CreateFlagWithDescriptionAsync("replace-description", "old text");

        var updatePayload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Description = "new text",
        };

        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/replace-description?environment=Development",
            updatePayload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        FlagResponse updated = await GetFlagAsync("replace-description");
        updated.Description.Should().Be("new text");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlag_AfterCreateWithMetadata_RoundTripsViaDatabaseAsync()
    {
        await CreateFlagWithTagsAsync(
            "roundtrip-flag",
            ["squad-x", "release-q3"],
            description: "Round-trip persistence check."
        );

        FlagResponse fetched = await GetFlagAsync("roundtrip-flag");

        fetched.Description.Should().Be("Round-trip persistence check.");
        fetched.Tags.Should().BeEquivalentTo(["squad-x", "release-q3"]);
    }

    private async Task CreateFlagWithTagsAsync(
        string name,
        IReadOnlyList<string> tags,
        string? description = null
    )
    {
        var payload = new
        {
            Name = name,
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Description = description,
            Tags = tags,
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task CreateFlagWithDescriptionAsync(string name, string description)
    {
        var payload = new
        {
            Name = name,
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Description = description,
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<FlagResponse> GetFlagAsync(string name)
    {
        HttpResponseMessage response = await Client.GetAsync(
            $"/api/flags/{name}?environment=Development"
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions))!;
    }
}
