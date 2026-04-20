using Banderas.Application.AI;
using Banderas.Application.DTOs;
using Banderas.Application.Evaluation;
using Banderas.Application.Exceptions;
using Banderas.Application.Services;
using Banderas.Application.Strategies;
using Banderas.Application.Telemetry;
using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Banderas.Tests.AI;

[Trait("Category", "Unit")]
public sealed class BanderasServiceAnalysisTests
{
    private readonly StubRepository _repo = new();
    private readonly CapturingPromptSanitizer _sanitizer = new();
    private readonly StubAiFlagAnalyzer _analyzer = new();
    private readonly BanderasService _service;

    public BanderasServiceAnalysisTests()
    {
        FeatureEvaluator evaluator = new(new IRolloutStrategy[] { new NoneStrategy() });
        _service = new BanderasService(
            _repo,
            evaluator,
            NullLogger<BanderasService>.Instance,
            new NullTelemetryService(),
            _sanitizer,
            _analyzer);
    }

    [Fact]
    public async Task AnalyzeFlagsAsync_NoThresholdSupplied_UsesDefaultThresholdAsync()
    {
        FlagHealthRequest request = new();

        await _service.AnalyzeFlagsAsync(request);

        Assert.Equal(30, _analyzer.CapturedThreshold);
    }

    [Fact]
    public async Task AnalyzeFlagsAsync_ThresholdSupplied_UsesCallerThresholdAsync()
    {
        FlagHealthRequest request = new() { StalenessThresholdDays = 7 };

        await _service.AnalyzeFlagsAsync(request);

        Assert.Equal(7, _analyzer.CapturedThreshold);
    }

    [Fact]
    public async Task AnalyzeFlagsAsync_NullStrategyConfig_DoesNotThrowAsync()
    {
        _repo.Flags =
        [
            new Flag("flag-no-config", EnvironmentType.Development, true, RolloutStrategy.None, null)
        ];

        FlagHealthAnalysisResponse response = await _service.AnalyzeFlagsAsync(new FlagHealthRequest());

        Assert.NotNull(response);
    }

    [Fact]
    public async Task AnalyzeFlagsAsync_SanitizesNameAndStrategyConfigAsync()
    {
        _repo.Flags =
        [
            new Flag("flag-a", EnvironmentType.Development, true, RolloutStrategy.None, "{}")
        ];

        await _service.AnalyzeFlagsAsync(new FlagHealthRequest());

        Assert.Contains("flag-a", _sanitizer.SanitizedInputs);
        Assert.Contains("{}", _sanitizer.SanitizedInputs);
    }

    [Fact]
    public async Task AnalyzeFlagsAsync_AnalyzerThrows_PropagatesAiExceptionAsync()
    {
        _analyzer.ShouldThrow = true;

        await Assert.ThrowsAsync<AiAnalysisUnavailableException>(
            () => _service.AnalyzeFlagsAsync(new FlagHealthRequest()));
    }

    // --- Test doubles ---

    private sealed class StubRepository : IBanderasRepository
    {
        public List<Flag> Flags { get; set; } = [];

        public Task<IReadOnlyList<Flag>> GetAllAsync(
            EnvironmentType? environment = null,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Flag>>(Flags);

        public Task<Flag?> GetByNameAsync(string name, EnvironmentType environment, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string name, EnvironmentType environment, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task AddAsync(Flag flag, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class CapturingPromptSanitizer : IPromptSanitizer
    {
        public List<string> SanitizedInputs { get; } = [];

        public string Sanitize(string input)
        {
            SanitizedInputs.Add(input);
            return input;
        }
    }

    private sealed class StubAiFlagAnalyzer : IAiFlagAnalyzer
    {
        public bool ShouldThrow { get; set; }
        public int CapturedThreshold { get; private set; }

        public Task<FlagHealthAnalysisResponse> AnalyzeAsync(
            IReadOnlyList<FlagResponse> flags,
            int stalenessThresholdDays,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new AiAnalysisUnavailableException("Stubbed failure.");
            }

            CapturedThreshold = stalenessThresholdDays;
            return Task.FromResult(new FlagHealthAnalysisResponse
            {
                Summary = "All good.",
                Flags = [],
                AnalyzedAt = DateTimeOffset.UtcNow,
                StalenessThresholdDays = stalenessThresholdDays
            });
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
