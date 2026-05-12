using Banderas.Application.AI;
using Banderas.Application.DTOs;
using Banderas.Application.Evaluation;
using Banderas.Application.Services;
using Banderas.Application.Strategies;
using Banderas.Application.Telemetry;
using Banderas.Application.Validators;
using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Banderas.Tests.Services;

[Trait("Category", "Unit")]
public sealed class BanderasServiceMetadataTests
{
    private readonly CapturingRepository _repo;
    private readonly BanderasService _service;

    public BanderasServiceMetadataTests()
    {
        _repo = new CapturingRepository();
        FeatureEvaluator evaluator = new([new NoneStrategy()]);
        StrategyConfigFactory configFactory = new([
            new NoneConfigValidator(),
            new PercentageConfigValidator(),
            new RoleBasedConfigValidator(),
        ]);
        _service = new BanderasService(
            _repo,
            evaluator,
            NullLogger<BanderasService>.Instance,
            new NullTelemetryService(),
            new IdentityPromptSanitizer(),
            new ThrowingAiFlagAnalyzer(),
            configFactory
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateFlagAsync_NormalizesTags_TrimLowercaseDedupeAsync()
    {
        CreateFlagRequest request = NewCreateRequest() with
        {
            Tags = ["Checkout", " checkout ", "CHECKOUT", "Release-Q2"],
        };

        await _service.CreateFlagAsync(request);

        _repo.AddedFlag.Should().NotBeNull();
        _repo.AddedFlag!.Tags.Should().BeEquivalentTo(["checkout", "release-q2"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateFlagAsync_WithDescriptionContainingControlChars_StripsThemAsync()
    {
        CreateFlagRequest request = NewCreateRequest() with
        {
            Description = "  Controls checkout v2  ",
        };

        await _service.CreateFlagAsync(request);

        _repo.AddedFlag!.Description.Should().Be("Controls checkout v2");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateFlagAsync_WithEmptyDescription_StoresAsNullAsync()
    {
        CreateFlagRequest request = NewCreateRequest() with { Description = "" };

        await _service.CreateFlagAsync(request);

        _repo.AddedFlag!.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateFlagAsync_WithNullTags_StoresEmptyListAsync()
    {
        CreateFlagRequest request = NewCreateRequest() with { Tags = null };

        await _service.CreateFlagAsync(request);

        _repo.AddedFlag!.Tags.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_WhenTagsIsNull_PreservesExistingTagsAsync()
    {
        _repo.ExistingFlag = NewFlag(tags: ["a", "b"]);
        UpdateFlagRequest request = NewUpdateRequest() with { Tags = null };

        await _service.UpdateFlagAsync("test-flag", EnvironmentType.Development, request);

        _repo.ExistingFlag!.Tags.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_WhenTagsIsEmpty_ClearsTagsAsync()
    {
        _repo.ExistingFlag = NewFlag(tags: ["a", "b"]);
        UpdateFlagRequest request = NewUpdateRequest() with { Tags = [] };

        await _service.UpdateFlagAsync("test-flag", EnvironmentType.Development, request);

        _repo.ExistingFlag!.Tags.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_WhenDescriptionIsEmptyString_ClearsDescriptionAsync()
    {
        _repo.ExistingFlag = NewFlag(description: "old text");
        UpdateFlagRequest request = NewUpdateRequest() with { Description = "" };

        await _service.UpdateFlagAsync("test-flag", EnvironmentType.Development, request);

        _repo.ExistingFlag!.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_WhenDescriptionIsNull_PreservesExistingDescriptionAsync()
    {
        _repo.ExistingFlag = NewFlag(description: "old text");
        UpdateFlagRequest request = NewUpdateRequest() with { Description = null };

        await _service.UpdateFlagAsync("test-flag", EnvironmentType.Development, request);

        _repo.ExistingFlag!.Description.Should().Be("old text");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_WhenTagsProvided_NormalizesAndReplacesAsync()
    {
        _repo.ExistingFlag = NewFlag(tags: ["old"]);
        UpdateFlagRequest request = NewUpdateRequest() with
        {
            Tags = ["New-Tag", "new-tag", "OTHER"],
        };

        await _service.UpdateFlagAsync("test-flag", EnvironmentType.Development, request);

        _repo.ExistingFlag!.Tags.Should().BeEquivalentTo(["new-tag", "other"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_AppliesReconfigureAndMetadataInOneSaveChangesAsync()
    {
        _repo.ExistingFlag = NewFlag();
        UpdateFlagRequest request = NewUpdateRequest() with
        {
            IsEnabled = false,
            Tags = ["new"],
            Description = "new desc",
        };

        await _service.UpdateFlagAsync("test-flag", EnvironmentType.Development, request);

        _repo.SaveChangesCallCount.Should().Be(1);
        _repo.ExistingFlag!.IsEnabled.Should().BeFalse();
        _repo.ExistingFlag.Description.Should().Be("new desc");
        _repo.ExistingFlag.Tags.Should().BeEquivalentTo(["new"]);
    }

    private static CreateFlagRequest NewCreateRequest() =>
        new("test-flag", EnvironmentType.Development, true, RolloutStrategy.None, null);

    private static UpdateFlagRequest NewUpdateRequest() => new(true, RolloutStrategy.None, null);

    private static Flag NewFlag(string? description = null, IReadOnlyList<string>? tags = null) =>
        new(
            "test-flag",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            new StrategyConfig(RolloutStrategy.None, "{}"),
            description,
            tags
        );

    private sealed class CapturingRepository : IBanderasRepository
    {
        public Flag? AddedFlag { get; private set; }
        public Flag? ExistingFlag { get; set; }
        public int SaveChangesCallCount { get; private set; }

        public Task<Flag?> GetByNameAsync(
            string name,
            EnvironmentType environment,
            CancellationToken ct = default
        ) => Task.FromResult(ExistingFlag);

        public Task<bool> ExistsAsync(
            string name,
            EnvironmentType environment,
            CancellationToken ct = default
        ) => Task.FromResult(false);

        public Task<IReadOnlyList<Flag>> GetAllAsync(
            EnvironmentType? environment = null,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<Flag>>([]);

        public Task AddAsync(Flag flag, CancellationToken ct = default)
        {
            AddedFlag = flag;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NullTelemetryService : ITelemetryService
    {
        public void TrackEvaluation(
            string flagName,
            bool result,
            RolloutStrategy strategy,
            EnvironmentType environment
        ) { }
    }

    private sealed class IdentityPromptSanitizer : IPromptSanitizer
    {
        public string Sanitize(string input) => input;
    }

    private sealed class ThrowingAiFlagAnalyzer : IAiFlagAnalyzer
    {
        public Task<FlagHealthAnalysisResponse> AnalyzeAsync(
            IReadOnlyList<FlagResponse> flags,
            int stalenessThresholdDays,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
