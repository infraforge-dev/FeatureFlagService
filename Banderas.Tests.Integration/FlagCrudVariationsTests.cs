using System.Net;
using System.Net.Http.Json;
using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Banderas.Tests.Integration;

/// <summary>
/// AC-11 / AC-12 / AC-13 — full HTTP round-trip for the Variations field across
/// all four <c>VariationKind</c>s and PUT null/empty/populated semantics.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FlagCrudVariationsTests : IntegrationTestBase
{
    public FlagCrudVariationsTests(BanderasApiFactory factory)
        : base(factory) { }

    // -------- AC-11: round-trip across all four Kinds --------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithBooleanVariations_Returns201AndRoundTripsAsync()
    {
        var payload = new
        {
            Name = "bool-flag",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new[]
            {
                new
                {
                    Key = "off",
                    Kind = "Boolean",
                    Value = "false",
                },
                new
                {
                    Key = "on",
                    Kind = "Boolean",
                    Value = "true",
                },
            },
        };

        HttpResponseMessage create = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        FlagResponse body = (await create.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions))!;
        body.Variations.Should().HaveCount(2);
        body.Variations[0].Key.Should().Be("off");
        body.Variations[0].Kind.Should().Be("Boolean");
        body.Variations[0].Value.Should().Be("false");

        FlagResponse fetched = await GetFlagAsync("bool-flag");
        fetched.Variations.Should().HaveCount(2);
        fetched.Variations[1].Key.Should().Be("on");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithNumberVariations_RoundTripsAsync()
    {
        var payload = new
        {
            Name = "num-flag",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new[]
            {
                new
                {
                    Key = "low",
                    Kind = "Number",
                    Value = "0",
                },
                new
                {
                    Key = "mid",
                    Kind = "Number",
                    Value = "50",
                },
                new
                {
                    Key = "high",
                    Kind = "Number",
                    Value = "100",
                },
            },
        };

        HttpResponseMessage create = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        FlagResponse fetched = await GetFlagAsync("num-flag");
        fetched.Variations.Should().HaveCount(3);
        fetched.Variations.Select(v => v.Value).Should().Equal("0", "50", "100");
        fetched.Variations.Should().AllSatisfy(v => v.Kind.Should().Be("Number"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithStringVariations_RoundTripsAsync()
    {
        var payload = new
        {
            Name = "str-flag",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new[]
            {
                new
                {
                    Key = "control",
                    Kind = "String",
                    Value = "\"control\"",
                },
                new
                {
                    Key = "treatment",
                    Kind = "String",
                    Value = "\"red-button\"",
                },
            },
        };

        HttpResponseMessage create = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        FlagResponse fetched = await GetFlagAsync("str-flag");
        fetched.Variations[1].Value.Should().Be("\"red-button\"");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithJsonVariations_RoundTripsAsync()
    {
        var payload = new
        {
            Name = "json-flag",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new[]
            {
                new
                {
                    Key = "variant-a",
                    Kind = "Json",
                    Value = "{\"theme\":\"dark\"}",
                },
                new
                {
                    Key = "variant-b",
                    Kind = "Json",
                    Value = "{\"theme\":\"light\"}",
                },
            },
        };

        HttpResponseMessage create = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        FlagResponse fetched = await GetFlagAsync("json-flag");
        fetched.Variations[0].Value.Should().Be("{\"theme\":\"dark\"}");
        fetched.Variations[1].Kind.Should().Be("Json");
    }

    // -------- AC-11: PUT semantics --------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_VariationsNull_PreservesExistingMenuAsync()
    {
        await CreateDefaultBooleanFlagAsync("preserve-variations");

        var updatePayload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = (object?)null,
        };

        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/preserve-variations?environment=Development",
            updatePayload,
            JsonOptions
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        FlagResponse fetched = await GetFlagAsync("preserve-variations");
        fetched.Variations.Should().HaveCount(2);
        fetched.Variations[0].Key.Should().Be("off");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_VariationsEmpty_Returns400Async()
    {
        await CreateDefaultBooleanFlagAsync("empty-variations");

        var updatePayload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = Array.Empty<object>(),
        };

        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/empty-variations?environment=Development",
            updatePayload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = await ReadValidationProblemDetailsAsync(response);
        problem.Errors.Should().ContainKey("Variations");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_VariationsPopulated_AtomicReplacementAsync()
    {
        await CreateDefaultBooleanFlagAsync("replace-variations");

        var updatePayload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new[]
            {
                new
                {
                    Key = "alpha",
                    Kind = "Number",
                    Value = "1",
                },
                new
                {
                    Key = "bravo",
                    Kind = "Number",
                    Value = "2",
                },
                new
                {
                    Key = "charlie",
                    Kind = "Number",
                    Value = "3",
                },
            },
        };

        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/replace-variations?environment=Development",
            updatePayload,
            JsonOptions
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        FlagResponse fetched = await GetFlagAsync("replace-variations");
        fetched.Variations.Should().HaveCount(3);
        fetched.Variations[2].Key.Should().Be("charlie");
        fetched.Variations.Should().AllSatisfy(v => v.Kind.Should().Be("Number"));
    }

    // -------- AC-13: invalid variation payloads return 400 ProblemDetails --------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithMissingVariations_Returns400Async()
    {
        var payload = new
        {
            Name = "missing-variations",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            // No Variations field at all.
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = await ReadValidationProblemDetailsAsync(response);
        problem.Errors.Should().ContainKey("Variations");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithMixedKinds_Returns400Async()
    {
        var payload = new
        {
            Name = "mixed-kinds",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new object[]
            {
                new
                {
                    Key = "off",
                    Kind = "Boolean",
                    Value = "false",
                },
                new
                {
                    Key = "count",
                    Kind = "Number",
                    Value = "42",
                },
            },
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ReadValidationProblemDetailsAsync(response);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_WithUnknownKind_Returns400Async()
    {
        var payload = new
        {
            Name = "unknown-kind",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new[]
            {
                new
                {
                    Key = "x",
                    Kind = "Object",
                    Value = "{}",
                },
            },
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ReadValidationProblemDetailsAsync(response);
    }

    // -------- AC-12: variations always present on every flag response --------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllFlags_AlwaysIncludesVariationsAsync()
    {
        await CreateDefaultBooleanFlagAsync("getall-variations");

        HttpResponseMessage response = await Client.GetAsync("/api/flags?environment=Development");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<FlagResponse> body = (
            await response.Content.ReadFromJsonAsync<List<FlagResponse>>(JsonOptions)
        )!;
        body.Should().AllSatisfy(f => f.Variations.Should().NotBeEmpty());
    }

    private async Task CreateDefaultBooleanFlagAsync(string name)
    {
        var payload = new
        {
            Name = name,
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
            Variations = new[]
            {
                new
                {
                    Key = "off",
                    Kind = "Boolean",
                    Value = "false",
                },
                new
                {
                    Key = "on",
                    Kind = "Boolean",
                    Value = "true",
                },
            },
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
