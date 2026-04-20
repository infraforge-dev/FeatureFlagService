using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Banderas.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FlagEndpointTests : IntegrationTestBase
{
    private const string InvalidRouteName = "invalid%20name%21";

    public FlagEndpointTests(BanderasApiFactory factory)
        : base(factory) { }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_ValidRequest_Returns201WithLocationHeaderAsync()
    {
        // Arrange
        var payload = new
        {
            Name = "feature-one",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
        };
        DateTime before = DateTime.UtcNow;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );
        DateTime after = DateTime.UtcNow;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("feature-one");

        FlagResponse? body = await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be("feature-one");
        body.Environment.Should().Be(EnvironmentType.Development);
        body.IsEnabled.Should().BeTrue();
        body.StrategyType.Should().Be(RolloutStrategy.None);
        body.StrategyConfig.Should().Be("{}");
        body.Id.Should().NotBe(Guid.Empty);
        body.CreatedAt.Should().BeOnOrAfter(before);
        body.CreatedAt.Should().BeOnOrBefore(after);
        body.UpdatedAt.Should().BeOnOrAfter(before);
        body.UpdatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_NoneStrategyNullConfig_Returns201Async()
    {
        // Arrange
        var payload = new
        {
            Name = "none-null-config",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        FlagResponse? body = await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.StrategyConfig.Should().Be("{}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_InvalidName_Returns400WithValidationErrorsAsync()
    {
        // Arrange
        var payload = new
        {
            Name = "invalid flag!",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails body = await ReadValidationProblemDetailsAsync(response);
        body.Errors.Should().ContainKey("Name");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_DuplicateNameAndEnvironment_Returns409Async()
    {
        // Arrange
        await CreateFlagAsync(name: "duplicate-flag");
        var payload = new
        {
            Name = "duplicate-flag",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.None,
            StrategyConfig = (string?)null,
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.Conflict);
        body.Detail.Should().Contain("already exists");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_InvalidPercentageConfig_Returns400Async()
    {
        // Arrange
        var payload = new
        {
            Name = "invalid-percentage",
            Environment = EnvironmentType.Development,
            IsEnabled = true,
            StrategyType = RolloutStrategy.Percentage,
            StrategyConfig = """{"roles": ["Admin"]}""",
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails body = await ReadValidationProblemDetailsAsync(response);
        body.Errors.Should().ContainKey("StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllFlags_WithFlags_Returns200WithListAsync()
    {
        // Arrange
        await CreateFlagAsync(name: "flag-a");
        await CreateFlagAsync(name: "flag-b");

        // Act
        HttpResponseMessage response = await Client.GetAsync("/api/flags?environment=Development");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FlagResponse[]? body = await response.Content.ReadFromJsonAsync<FlagResponse[]>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body.Should().HaveCount(2);
        body!.Select(flag => flag.Name).Should().BeEquivalentTo(["flag-a", "flag-b"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllFlags_NoFlags_Returns200EmptyArrayAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.GetAsync("/api/flags?environment=Development");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FlagResponse[]? body = await response.Content.ReadFromJsonAsync<FlagResponse[]>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllFlags_FiltersByEnvironment_ReturnsOnlyMatchingAsync()
    {
        // Arrange
        await CreateFlagAsync(name: "dev-flag", environment: EnvironmentType.Development);
        await CreateFlagAsync(name: "stage-flag", environment: EnvironmentType.Staging);

        // Act
        HttpResponseMessage response = await Client.GetAsync("/api/flags?environment=Development");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FlagResponse[]? body = await response.Content.ReadFromJsonAsync<FlagResponse[]>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body.Should().HaveCount(1);
        body![0].Environment.Should().Be(EnvironmentType.Development);
        body[0].Name.Should().Be("dev-flag");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllFlags_ExcludesArchivedFlagsAsync()
    {
        // Arrange
        await CreateFlagAsync(name: "archived-flag");
        HttpResponseMessage archiveResponse = await Client.DeleteAsync(
            "/api/flags/archived-flag?environment=Development"
        );
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await Client.GetAsync("/api/flags?environment=Development");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FlagResponse[]? body = await response.Content.ReadFromJsonAsync<FlagResponse[]>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlagByName_Exists_Returns200WithCorrectBodyAsync()
    {
        // Arrange
        FlagResponse created = await CreateFlagAsync(name: "by-name-flag");

        // Act
        HttpResponseMessage response = await Client.GetAsync(
            "/api/flags/by-name-flag?environment=Development"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FlagResponse? body = await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be(created.Name);
        body.Environment.Should().Be(created.Environment);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlagByName_NotFound_Returns404ProblemDetailsAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.GetAsync(
            "/api/flags/nonexistent?environment=Development"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.NotFound);
        body.Detail.Should().Contain("No feature flag with name 'nonexistent' was found.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlagByName_InvalidRouteName_Returns400ProblemDetailsAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.GetAsync(
            $"/api/flags/{InvalidRouteName}?environment=Development"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        body.Detail.Should()
            .Contain("Flag name may only contain letters, numbers, hyphens, and underscores.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_ValidRequest_Returns204Async()
    {
        // Arrange
        await CreateFlagAsync(name: "update-flag");
        var payload = new
        {
            IsEnabled = false,
            StrategyType = RolloutStrategy.Percentage,
            StrategyConfig = """{"percentage": 50}""",
        };

        // Act
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/update-flag?environment=Development",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        FlagResponse updated = await GetFlagAsync("update-flag");
        updated.IsEnabled.Should().BeFalse();
        updated.StrategyType.Should().Be(RolloutStrategy.Percentage);
        AssertPercentageConfig(updated.StrategyConfig!, 50);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_NotFound_Returns404Async()
    {
        // Arrange
        var payload = new
        {
            IsEnabled = false,
            StrategyType = RolloutStrategy.Percentage,
            StrategyConfig = """{"percentage": 50}""",
        };

        // Act
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/nonexistent?environment=Development",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.NotFound);
        body.Detail.Should().Contain("No feature flag with name 'nonexistent' was found.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_InvalidStrategyConfig_Returns400Async()
    {
        // Arrange
        await CreateFlagAsync(name: "update-invalid-config");
        var payload = new
        {
            IsEnabled = true,
            StrategyType = RolloutStrategy.RoleBased,
            StrategyConfig = """{"percentage": 50}""",
        };

        // Act
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/update-invalid-config?environment=Development",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails body = await ReadValidationProblemDetailsAsync(response);
        body.Errors.Should().ContainKey("StrategyConfig");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchiveFlag_Exists_Returns204AndExcludedFromGetAllAsync()
    {
        // Arrange
        await CreateFlagAsync(name: "archive-flag");

        // Act
        HttpResponseMessage response = await Client.DeleteAsync(
            "/api/flags/archive-flag?environment=Development"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage getAllResponse = await Client.GetAsync(
            "/api/flags?environment=Development"
        );
        FlagResponse[]? body = await getAllResponse.Content.ReadFromJsonAsync<FlagResponse[]>(
            JsonOptions
        );
        body.Should().NotBeNull();
        body.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchiveFlag_AllowsNameReuse_ReturnsCreatedOnRecreateAsync()
    {
        // Arrange
        FlagResponse original = await CreateFlagAsync(name: "reusable-flag");
        HttpResponseMessage archiveResponse = await Client.DeleteAsync(
            "/api/flags/reusable-flag?environment=Development"
        );
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        FlagResponse recreated = await CreateFlagAsync(name: "reusable-flag");

        // Assert
        recreated.Id.Should().NotBe(original.Id);
        recreated.Name.Should().Be("reusable-flag");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllFlags_InvalidEnvironment_Returns400ProblemDetailsAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.GetAsync("/api/flags?environment=None");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        body.Detail.Should().Contain("A valid environment must be specified");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlagByName_InvalidEnvironment_Returns400ProblemDetailsAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.GetAsync(
            "/api/flags/test-flag?environment=None"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        body.Detail.Should().Contain("A valid environment must be specified");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_InvalidRouteName_Returns400ProblemDetailsAsync()
    {
        // Arrange
        var payload = new
        {
            IsEnabled = false,
            StrategyType = RolloutStrategy.Percentage,
            StrategyConfig = """{"percentage": 50}""",
        };

        // Act
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            $"/api/flags/{InvalidRouteName}?environment=Development",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        body.Detail.Should()
            .Contain("Flag name may only contain letters, numbers, hyphens, and underscores.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateFlag_InvalidEnvironment_Returns400ProblemDetailsAsync()
    {
        // Arrange
        var payload = new
        {
            IsEnabled = false,
            StrategyType = RolloutStrategy.Percentage,
            StrategyConfig = """{"percentage": 50}""",
        };

        // Act
        HttpResponseMessage response = await Client.PutAsJsonAsync(
            "/api/flags/test-flag?environment=None",
            payload,
            JsonOptions
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        body.Detail.Should().Contain("A valid environment must be specified");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchiveFlag_NotFound_Returns404ProblemDetailsAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.DeleteAsync(
            "/api/flags/nonexistent?environment=Development"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.NotFound);
        body.Detail.Should().Contain("No feature flag with name 'nonexistent' was found.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchiveFlag_InvalidRouteName_Returns400ProblemDetailsAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.DeleteAsync(
            $"/api/flags/{InvalidRouteName}?environment=Development"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        body.Detail.Should()
            .Contain("Flag name may only contain letters, numbers, hyphens, and underscores.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ArchiveFlag_InvalidEnvironment_Returns400ProblemDetailsAsync()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await Client.DeleteAsync(
            "/api/flags/test-flag?environment=None"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ProblemDetails body = await ReadProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        body.Detail.Should().Contain("A valid environment must be specified");
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

    private async Task<FlagResponse> GetFlagAsync(
        string name,
        EnvironmentType environment = EnvironmentType.Development
    )
    {
        HttpResponseMessage response = await Client.GetAsync(
            $"/api/flags/{name}?environment={environment}"
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        FlagResponse? body = await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions);
        body.Should().NotBeNull();
        return body!;
    }

    private static void AssertPercentageConfig(string strategyConfig, int expectedPercentage)
    {
        using JsonDocument document = JsonDocument.Parse(strategyConfig);
        document.RootElement.GetProperty("percentage").GetInt32().Should().Be(expectedPercentage);
    }
}
