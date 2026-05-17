using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Banderas.Domain.Enums;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Banderas.Tests.Integration;

/// <summary>
/// Pins the JSON wire shape of every API response.
/// These tests parse raw JsonDocument — not typed deserialization — so that field renames,
/// casing changes, and enum-format changes are caught before they break SDK consumers.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ContractTests : IntegrationTestBase
{
    public ContractTests(BanderasApiFactory factory)
        : base(factory) { }

    // -------------------------------------------------------------------------
    // AC-1: FlagResponse shape — POST create
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_ResponseShape_ContainsAllExpectedFieldsAsync()
    {
        // Arrange
        var payload = new
        {
            Name = "contract-create-flag",
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

        // Assert — status
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — wire shape
        using JsonDocument doc = await ReadRawJsonAsync(response);
        JsonElement root = doc.RootElement;

        AssertFlagResponseShape(root);

        // description must be present and null (not absent)
        root.TryGetProperty("description", out JsonElement description)
            .Should()
            .BeTrue("FlagResponse must always include 'description'");
        description.ValueKind.Should().Be(JsonValueKind.Null);

        // tags must be present and empty array (not absent)
        root.TryGetProperty("tags", out JsonElement tags)
            .Should()
            .BeTrue("FlagResponse must always include 'tags'");
        tags.ValueKind.Should().Be(JsonValueKind.Array);
        tags.GetArrayLength().Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // AC-2: FlagResponse shape — GET single
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFlagByName_ResponseShape_ContainsAllExpectedFieldsAsync()
    {
        // Arrange
        await CreateFlagAsync("contract-get-flag");

        // Act
        HttpResponseMessage response = await Client.GetAsync(
            "/api/flags/contract-get-flag?environment=Development"
        );

        // Assert — status and content-type
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // Assert — wire shape
        using JsonDocument doc = await ReadRawJsonAsync(response);
        AssertFlagResponseShape(doc.RootElement);
        AssertOptionalMetadataFields(doc.RootElement);
    }

    // -------------------------------------------------------------------------
    // AC-3: FlagResponse[] shape — GET all
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllFlags_ResponseShape_IsArrayWithCorrectElementShapeAsync()
    {
        // Arrange
        await CreateFlagAsync("contract-list-flag");

        // Act
        HttpResponseMessage response = await Client.GetAsync("/api/flags?environment=Development");

        // Assert — status and content-type
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // Assert — wire shape
        using JsonDocument doc = await ReadRawJsonAsync(response);
        JsonElement root = doc.RootElement;
        root.ValueKind.Should().Be(JsonValueKind.Array);
        root.GetArrayLength().Should().BeGreaterThan(0);

        JsonElement firstElement = root[0];
        AssertFlagResponseShape(firstElement);
        AssertOptionalMetadataFields(firstElement);
    }

    // -------------------------------------------------------------------------
    // AC-4: EvaluationResponse shape
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Evaluate_ResponseShape_ContainsIsEnabledAsBooleanAsync()
    {
        // Arrange
        await CreateFlagAsync("contract-eval-flag");
        var request = new
        {
            FlagName = "contract-eval-flag",
            UserId = "user-contract",
            UserRoles = Array.Empty<string>(),
            Environment = EnvironmentType.Development,
        };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/evaluate",
            request,
            JsonOptions
        );

        // Assert — status and content-type
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // Assert — wire shape
        using JsonDocument doc = await ReadRawJsonAsync(response);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("isEnabled", out JsonElement isEnabled)
            .Should()
            .BeTrue("EvaluationResponse must contain 'isEnabled'");
        isEnabled
            .ValueKind.Should()
            .BeOneOf([JsonValueKind.True, JsonValueKind.False], "isEnabled must be a JSON boolean");
    }

    // -------------------------------------------------------------------------
    // AC-5: FlagHealthAnalysisResponse shape
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeFlags_ResponseShape_ContainsAllExpectedFieldsAsync()
    {
        // Arrange
        await CreateFlagAsync("contract-health-flag");
        var request = new { StalenessThresholdDays = (int?)null };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/flags/health",
            request,
            JsonOptions
        );

        // Assert — status and content-type
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // Assert — top-level wire shape
        using JsonDocument doc = await ReadRawJsonAsync(response);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("summary", out JsonElement summary)
            .Should()
            .BeTrue("FlagHealthAnalysisResponse must contain 'summary'");
        summary.ValueKind.Should().Be(JsonValueKind.String);

        root.TryGetProperty("flags", out JsonElement flags)
            .Should()
            .BeTrue("FlagHealthAnalysisResponse must contain 'flags'");
        flags.ValueKind.Should().Be(JsonValueKind.Array);

        root.TryGetProperty("analyzedAt", out JsonElement analyzedAt)
            .Should()
            .BeTrue("FlagHealthAnalysisResponse must contain 'analyzedAt'");
        analyzedAt.ValueKind.Should().Be(JsonValueKind.String);

        root.TryGetProperty("stalenessThresholdDays", out JsonElement stalenessThresholdDays)
            .Should()
            .BeTrue("FlagHealthAnalysisResponse must contain 'stalenessThresholdDays'");
        stalenessThresholdDays.ValueKind.Should().Be(JsonValueKind.Number);

        // Assert — nested FlagAssessment shape (at least one flag)
        flags.GetArrayLength().Should().BeGreaterThan(0);
        JsonElement assessment = flags[0];

        assessment
            .TryGetProperty("name", out JsonElement assessmentName)
            .Should()
            .BeTrue("FlagAssessment must contain 'name'");
        assessmentName.ValueKind.Should().Be(JsonValueKind.String);

        assessment
            .TryGetProperty("status", out JsonElement assessmentStatus)
            .Should()
            .BeTrue("FlagAssessment must contain 'status'");
        assessmentStatus.ValueKind.Should().Be(JsonValueKind.String);

        assessment
            .TryGetProperty("reason", out JsonElement assessmentReason)
            .Should()
            .BeTrue("FlagAssessment must contain 'reason'");
        assessmentReason.ValueKind.Should().Be(JsonValueKind.String);

        assessment
            .TryGetProperty("recommendation", out JsonElement assessmentRecommendation)
            .Should()
            .BeTrue("FlagAssessment must contain 'recommendation'");
        assessmentRecommendation.ValueKind.Should().Be(JsonValueKind.String);
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts the 9 positional fields present on every FlagResponse.
    /// Enum fields (environment, strategyType) are asserted as strings — not integers.
    /// </summary>
    private static void AssertFlagResponseShape(JsonElement element)
    {
        element
            .TryGetProperty("id", out JsonElement id)
            .Should()
            .BeTrue("FlagResponse must contain 'id'");
        id.ValueKind.Should().Be(JsonValueKind.String);

        element
            .TryGetProperty("name", out JsonElement name)
            .Should()
            .BeTrue("FlagResponse must contain 'name'");
        name.ValueKind.Should().Be(JsonValueKind.String);

        element
            .TryGetProperty("environment", out JsonElement environment)
            .Should()
            .BeTrue("FlagResponse must contain 'environment'");
        environment
            .ValueKind.Should()
            .Be(JsonValueKind.String, "EnvironmentType must serialize as a string, not an integer");

        element
            .TryGetProperty("isEnabled", out JsonElement isEnabled)
            .Should()
            .BeTrue("FlagResponse must contain 'isEnabled'");
        isEnabled
            .ValueKind.Should()
            .BeOneOf([JsonValueKind.True, JsonValueKind.False], "isEnabled must be a boolean");

        element
            .TryGetProperty("isArchived", out JsonElement isArchived)
            .Should()
            .BeTrue("FlagResponse must contain 'isArchived'");
        isArchived
            .ValueKind.Should()
            .BeOneOf([JsonValueKind.True, JsonValueKind.False], "isArchived must be a boolean");

        element
            .TryGetProperty("strategyType", out JsonElement strategyType)
            .Should()
            .BeTrue("FlagResponse must contain 'strategyType'");
        strategyType
            .ValueKind.Should()
            .Be(JsonValueKind.String, "RolloutStrategy must serialize as a string, not an integer");

        element
            .TryGetProperty("strategyConfig", out _)
            .Should()
            .BeTrue("FlagResponse must contain 'strategyConfig'");

        element
            .TryGetProperty("createdAt", out JsonElement createdAt)
            .Should()
            .BeTrue("FlagResponse must contain 'createdAt'");
        createdAt.ValueKind.Should().Be(JsonValueKind.String);

        element
            .TryGetProperty("updatedAt", out JsonElement updatedAt)
            .Should()
            .BeTrue("FlagResponse must contain 'updatedAt'");
        updatedAt.ValueKind.Should().Be(JsonValueKind.String);
    }

    /// <summary>
    /// Asserts the 2 init-only optional metadata fields are always present in the JSON,
    /// even when their values are null / empty array.
    /// </summary>
    private static void AssertOptionalMetadataFields(JsonElement element)
    {
        element
            .TryGetProperty("description", out _)
            .Should()
            .BeTrue("FlagResponse must always include 'description' (even when null)");

        element
            .TryGetProperty("tags", out JsonElement tags)
            .Should()
            .BeTrue("FlagResponse must always include 'tags' (even when empty)");
        tags.ValueKind.Should().Be(JsonValueKind.Array);
    }

    private async Task<bool> CreateFlagAsync(string name)
    {
        var payload = new
        {
            Name = name,
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
        return response.StatusCode == HttpStatusCode.Created;
    }
}
