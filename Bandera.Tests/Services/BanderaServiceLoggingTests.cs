using Bandera.Application.Evaluation;
using Bandera.Application.Services;
using Bandera.Application.Strategies;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Exceptions;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Bandera.Tests.Services;

[Trait("Category", "Unit")]
public sealed class BanderaServiceLoggingTests
{
    private readonly TestBanderaRepository _repo;
    private readonly FeatureEvaluator _evaluator;
    private readonly FakeLogger<BanderaService> _fakeLogger;
    private readonly BanderaService _service;

    public BanderaServiceLoggingTests()
    {
        _repo = new TestBanderaRepository();
        _evaluator = new FeatureEvaluator(new IRolloutStrategy[] { new NoneStrategy() });
        _fakeLogger = new FakeLogger<BanderaService>();
        _service = new BanderaService(_repo, _evaluator, _fakeLogger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IsEnabledAsync_DisabledFlag_LogsFlagDisabledReasonAsync()
    {
        _repo.FlagToReturn = new Flag(
            "my-flag",
            EnvironmentType.Development,
            isEnabled: false,
            RolloutStrategy.None,
            null
        );

        FeatureEvaluationContext context = new("user-1", [], EnvironmentType.Development);

        bool isEnabled = await _service.IsEnabledAsync("my-flag", context);

        FakeLogRecord record = _fakeLogger.LatestRecord;

        Assert.False(isEnabled);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("FlagDisabled", record.GetStructuredStateValue("Reason")?.ToString());
        Assert.Null(record.GetStructuredStateValue("StrategyType"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IsEnabledAsync_EnabledFlag_LogsStrategyEvaluatedReasonAsync()
    {
        _repo.FlagToReturn = new Flag(
            "my-flag",
            EnvironmentType.Development,
            isEnabled: true,
            RolloutStrategy.None,
            null
        );

        FeatureEvaluationContext context = new("user-1", [], EnvironmentType.Development);

        bool isEnabled = await _service.IsEnabledAsync("my-flag", context);

        FakeLogRecord record = _fakeLogger.LatestRecord;

        Assert.True(isEnabled);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("StrategyEvaluated", record.GetStructuredStateValue("Reason")?.ToString());
        Assert.Equal("None", record.GetStructuredStateValue("StrategyType")?.ToString());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IsEnabledAsync_AnyOutcome_LogsHashedUserIdNotRawAsync()
    {
        _repo.FlagToReturn = new Flag(
            "my-flag",
            EnvironmentType.Development,
            isEnabled: false,
            RolloutStrategy.None,
            null
        );

        const string RawUserId = "user-abc-123";
        FeatureEvaluationContext context = new(RawUserId, [], EnvironmentType.Development);

        await _service.IsEnabledAsync("my-flag", context);

        FakeLogRecord record = _fakeLogger.LatestRecord;
        string? loggedUserId = record.GetStructuredStateValue("UserId")?.ToString();

        Assert.NotNull(loggedUserId);
        Assert.DoesNotContain(RawUserId, record.Message);
        Assert.NotEqual(RawUserId, loggedUserId);
        Assert.Matches("^[0-9a-f]{8}$", loggedUserId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task IsEnabledAsync_FlagNotFound_LogsWarningBeforeExceptionAsync()
    {
        _repo.FlagToReturn = null;

        FeatureEvaluationContext context = new("user-1", [], EnvironmentType.Development);

        await Assert.ThrowsAsync<FlagNotFoundException>(() =>
            _service.IsEnabledAsync("missing-flag", context)
        );

        FakeLogRecord record = _fakeLogger.LatestRecord;

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("missing-flag", record.Message);
    }

    private sealed class TestBanderaRepository : IBanderaRepository
    {
        public Flag? FlagToReturn { get; set; }

        public Task<Flag?> GetByNameAsync(
            string name,
            EnvironmentType environment,
            CancellationToken ct = default
        ) => Task.FromResult(FlagToReturn);

        public Task<bool> ExistsAsync(
            string name,
            EnvironmentType environment,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task<IReadOnlyList<Flag>> GetAllAsync(
            EnvironmentType environment,
            CancellationToken ct = default
        ) => throw new NotSupportedException();

        public Task AddAsync(Flag flag, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
