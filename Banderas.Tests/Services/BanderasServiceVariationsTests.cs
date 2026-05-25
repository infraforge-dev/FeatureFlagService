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
using Banderas.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Banderas.Tests.Services;

/// <summary>
/// Unit-level coverage for AC-8 / AC-15: <see cref="BanderasService"/> wires
/// <see cref="VariationRequest"/> through to <see cref="Flag.Variations"/> on
/// create/update, and <c>AnalyzeFlagsAsync</c> sanitizes variation key and value.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BanderasServiceVariationsTests
{
    private readonly CapturingRepository _repo;
    private readonly RecordingPromptSanitizer _sanitizer;
    private readonly CapturingAiFlagAnalyzer _aiAnalyzer;
    private readonly BanderasService _service;

    public BanderasServiceVariationsTests()
    {
        _repo = new CapturingRepository();
        _sanitizer = new RecordingPromptSanitizer();
        _aiAnalyzer = new CapturingAiFlagAnalyzer();

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
            _sanitizer,
            _aiAnalyzer,
            configFactory
        );
    }

    private static IReadOnlyList<VariationRequest> DefaultMenu() =>
        [new("off", "Boolean", "false"), new("on", "Boolean", "true")];

    private static CreateFlagRequest NewCreateRequest() =>
        new("test-flag", EnvironmentType.Development, true, RolloutStrategy.None, null)
        {
            Variations = DefaultMenu(),
        };

    private static UpdateFlagRequest NewUpdateRequest() => new(true, RolloutStrategy.None, null);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateFlagAsync_WithMenu_PersistsVariationsInOrderAsync()
    {
        // Three-Number menu — unique values, single Kind, valid keys.
        CreateFlagRequest request = NewCreateRequest() with
        {
            Variations =
            [
                new VariationRequest("first", "Number", "1"),
                new VariationRequest("second", "Number", "2"),
                new VariationRequest("third", "Number", "3"),
            ],
        };

        await _service.CreateFlagAsync(request);

        _repo.AddedFlag.Should().NotBeNull();
        _repo.AddedFlag!.Variations.Should().HaveCount(3);
        _repo.AddedFlag.Variations[0].Key.Should().Be("first");
        _repo.AddedFlag.Variations[2].Key.Should().Be("third");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_WhenVariationsNull_PreservesExistingMenuAsync()
    {
        _repo.ExistingFlag = new Flag(
            "f",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            new StrategyConfig(RolloutStrategy.None, "{}"),
            variations: FlagBuilder.DefaultVariations()
        );

        UpdateFlagRequest request = NewUpdateRequest(); // Variations defaults to null

        await _service.UpdateFlagAsync("f", EnvironmentType.Development, request);

        _repo.ExistingFlag.Variations.Should().HaveCount(2);
        _repo.ExistingFlag.Variations[0].Key.Should().Be("off");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_WhenVariationsPopulated_ReplacesMenuAsync()
    {
        _repo.ExistingFlag = new Flag(
            "f",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            new StrategyConfig(RolloutStrategy.None, "{}"),
            variations: FlagBuilder.DefaultVariations()
        );

        UpdateFlagRequest request = NewUpdateRequest() with
        {
            Variations =
            [
                new VariationRequest("low", "Number", "0"),
                new VariationRequest("mid", "Number", "50"),
                new VariationRequest("high", "Number", "100"),
            ],
        };

        await _service.UpdateFlagAsync("f", EnvironmentType.Development, request);

        _repo.ExistingFlag.Variations.Should().HaveCount(3);
        _repo.ExistingFlag.Variations[1].Key.Should().Be("mid");
        _repo.ExistingFlag.Variations[1].Kind.Should().Be(VariationKind.Number);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFlagAsync_FlushesAllMutations_InASingleSaveChangesAsync()
    {
        _repo.ExistingFlag = new Flag(
            "f",
            EnvironmentType.Development,
            true,
            RolloutStrategy.None,
            new StrategyConfig(RolloutStrategy.None, "{}"),
            variations: FlagBuilder.DefaultVariations()
        );

        UpdateFlagRequest request = NewUpdateRequest() with
        {
            Description = "new desc",
            Tags = ["a"],
            Variations =
            [
                new VariationRequest("a", "Number", "1"),
                new VariationRequest("b", "Number", "2"),
            ],
        };

        await _service.UpdateFlagAsync("f", EnvironmentType.Development, request);

        _repo.SaveChangesCallCount.Should().Be(1);
        _repo.ExistingFlag.Description.Should().Be("new desc");
        _repo.ExistingFlag.Tags.Should().BeEquivalentTo(["a"]);
        _repo.ExistingFlag.Variations.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeFlagsAsync_SanitizesVariationKeyAndValueAsync()
    {
        _repo.AllFlags =
        [
            new Flag(
                "flag-a",
                EnvironmentType.Development,
                true,
                RolloutStrategy.None,
                new StrategyConfig(RolloutStrategy.None, "{}"),
                variations:
                [
                    new Variation("on", VariationKind.Boolean, "true"),
                    new Variation("off", VariationKind.Boolean, "false"),
                ]
            ),
        ];

        await _service.AnalyzeFlagsAsync(new FlagHealthRequest());

        // Both keys ("on", "off") and both raw JSON-encoded values ("true", "false")
        // should have gone through the sanitizer.
        _sanitizer.SanitizedInputs.Should().Contain("on");
        _sanitizer.SanitizedInputs.Should().Contain("off");
        _sanitizer.SanitizedInputs.Should().Contain("true");
        _sanitizer.SanitizedInputs.Should().Contain("false");
    }

    // -------- Stubs --------

    private sealed class CapturingRepository : IBanderasRepository
    {
        public Flag? AddedFlag { get; private set; }
        public Flag? ExistingFlag { get; set; }
        public IReadOnlyList<Flag> AllFlags { get; set; } = [];
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

        public Task AddAsync(Flag flag, CancellationToken ct = default)
        {
            AddedFlag = flag;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Flag>> GetAllAsync(
            EnvironmentType? environment = null,
            CancellationToken ct = default
        ) => Task.FromResult(AllFlags);

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPromptSanitizer : IPromptSanitizer
    {
        public List<string> SanitizedInputs { get; } = [];

        public string Sanitize(string input)
        {
            SanitizedInputs.Add(input);
            return input;
        }
    }

    private sealed class CapturingAiFlagAnalyzer : IAiFlagAnalyzer
    {
        public IReadOnlyList<FlagResponse>? CapturedFlags { get; private set; }

        public Task<FlagHealthAnalysisResponse> AnalyzeAsync(
            IReadOnlyList<FlagResponse> flags,
            int stalenessThresholdDays,
            CancellationToken cancellationToken = default
        )
        {
            CapturedFlags = flags;
            return Task.FromResult(
                new FlagHealthAnalysisResponse
                {
                    Summary = "ok",
                    AnalyzedAt = DateTimeOffset.UtcNow,
                    StalenessThresholdDays = stalenessThresholdDays,
                    Flags = flags
                        .Select(f => new FlagAssessment
                        {
                            Name = f.Name,
                            Status = "Healthy",
                            Reason = "r",
                            Recommendation = "rec",
                        })
                        .ToList(),
                }
            );
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
}
