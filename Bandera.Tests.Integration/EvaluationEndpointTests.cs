using System.Net;
using System.Net.Http.Json;
using Bandera.Application.DTOs;
using Bandera.Domain.Enums;
using Bandera.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Bandera.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class EvaluationEndpointTests : IntegrationTestBase
{
    public EvaluationEndpointTests(BanderaApiFactory factory)
        : base(factory) { }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_EnabledNoneStrategy_ReturnsTrueAsync()
    {
        // Arrange
        await CreateFlagAsync(name: "enabled-none-flag");
        var request = new EvaluationRequest(
            "enabled-none-flag",
            "user-1",
            [],
            EnvironmentType.Development
        );

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        EvaluationResponse? body = await response.Content.ReadFromJsonAsync<EvaluationResponse>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_DisabledFlag_ReturnsFalseAsync()
    {
        // Arrange
        await CreateFlagAsync(name: "disabled-flag", isEnabled: false);
        var request = new EvaluationRequest(
            "disabled-flag",
            "user-1",
            [],
            EnvironmentType.Development
        );

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        EvaluationResponse? body = await response.Content.ReadFromJsonAsync<EvaluationResponse>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_PercentageStrategy_ReturnsDeterministicResultAsync()
    {
        // Arrange
        await CreateFlagAsync(
            name: "percentage-flag",
            strategyType: RolloutStrategy.Percentage,
            strategyConfig: """{"percentage": 50}"""
        );
        var request = new EvaluationRequest(
            "percentage-flag",
            "deterministic-user",
            [],
            EnvironmentType.Development
        );

        // Act
        HttpResponseMessage firstResponse = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );
        HttpResponseMessage secondResponse = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        EvaluationResponse? firstBody =
            await firstResponse.Content.ReadFromJsonAsync<EvaluationResponse>(JsonOptions);
        EvaluationResponse? secondBody =
            await secondResponse.Content.ReadFromJsonAsync<EvaluationResponse>(JsonOptions);

        firstBody.Should().NotBeNull();
        secondBody.Should().NotBeNull();
        firstBody!.IsEnabled.Should().Be(secondBody!.IsEnabled);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_RoleStrategy_MatchingRole_ReturnsTrueAsync()
    {
        // Arrange
        await CreateFlagAsync(
            name: "role-match-flag",
            strategyType: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin"]}"""
        );
        var request = new EvaluationRequest(
            "role-match-flag",
            "user-1",
            ["Admin"],
            EnvironmentType.Development
        );

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        EvaluationResponse? body = await response.Content.ReadFromJsonAsync<EvaluationResponse>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_RoleStrategy_NoMatchingRole_ReturnsFalseAsync()
    {
        // Arrange
        await CreateFlagAsync(
            name: "role-no-match-flag",
            strategyType: RolloutStrategy.RoleBased,
            strategyConfig: """{"roles": ["Admin"]}"""
        );
        var request = new EvaluationRequest(
            "role-no-match-flag",
            "user-1",
            ["Viewer"],
            EnvironmentType.Development
        );

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        EvaluationResponse? body = await response.Content.ReadFromJsonAsync<EvaluationResponse>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_FlagNotFound_Returns404Async()
    {
        // Arrange
        var request = new EvaluationRequest(
            "missing-flag",
            "user-1",
            [],
            EnvironmentType.Development
        );

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.NotFound);
        body.Detail.Should().Contain("No feature flag with name 'missing-flag' was found.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_MissingUserId_Returns400Async()
    {
        // Arrange
        await CreateFlagAsync(name: "missing-user-id-flag");
        var request = new EvaluationRequest(
            "missing-user-id-flag",
            "",
            [],
            EnvironmentType.Development
        );

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails body = await ReadValidationProblemDetailsAsync(response);
        body.Errors.Should().ContainKey("UserId");
    }

    private async Task<FlagResponse> CreateFlagAsync(
        string name = "test-flag",
        EnvironmentType environment = EnvironmentType.Development,
        bool isEnabled = true,
        RolloutStrategy strategyType = RolloutStrategy.None,
        string? strategyConfig = null
    )
    {
        var payload = new
        {
            Name = name,
            Environment = environment,
            IsEnabled = isEnabled,
            StrategyType = strategyType,
            StrategyConfig = strategyConfig,
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FlagResponse? body = await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions);
        body.Should().NotBeNull();
        return body!;
    }
}
