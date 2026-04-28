using System.Globalization;
using System.Text.Json;
using Banderas.Application.DTOs;
using Banderas.Application.Exceptions;
using Banderas.Domain.Enums;
using Banderas.Infrastructure.AI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Banderas.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class AiFlagAnalyzerValidationTests
{
    private static readonly IReadOnlyList<FlagResponse> InputFlags =
    [
        CreateFlag("checkout-v2"),
        CreateFlag("search-ranking"),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnalyzeAsync_WhenSummaryIsMissingOrEmpty_ThrowsUnavailableAsync(
        string? summary
    )
    {
        AiFlagAnalyzer analyzer = CreateAnalyzer(
            JsonSerializer.Serialize(CreateResponse(summary: summary!))
        );

        Func<Task> act = () => analyzer.AnalyzeAsync(InputFlags, stalenessThresholdDays: 30);

        await act.Should().ThrowAsync<AiAnalysisUnavailableException>().WithMessage("*summary*");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFlagsListIsEmpty_ThrowsUnavailableAsync()
    {
        AiFlagAnalyzer analyzer = CreateAnalyzer(
            JsonSerializer.Serialize(CreateResponse(flags: []))
        );

        Func<Task> act = () => analyzer.AnalyzeAsync(InputFlags, stalenessThresholdDays: 30);

        await act.Should().ThrowAsync<AiAnalysisUnavailableException>().WithMessage("*flags list*");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenResponseOmitsAFlag_ThrowsUnavailableWithMissingFlagAsync()
    {
        AiFlagAnalyzer analyzer = CreateAnalyzer(
            JsonSerializer.Serialize(
                CreateResponse(flags: [CreateAssessment("checkout-v2", "Healthy")])
            )
        );

        Func<Task> act = () => analyzer.AnalyzeAsync(InputFlags, stalenessThresholdDays: 30);

        await act.Should()
            .ThrowAsync<AiAnalysisUnavailableException>()
            .WithMessage("*search-ranking*");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenStatusIsInvalid_ThrowsUnavailableWithBadStatusAsync()
    {
        AiFlagAnalyzer analyzer = CreateAnalyzer(
            JsonSerializer.Serialize(
                CreateResponse(
                    flags:
                    [
                        CreateAssessment("checkout-v2", "Healthy"),
                        CreateAssessment("search-ranking", "Unknown"),
                    ]
                )
            )
        );

        Func<Task> act = () => analyzer.AnalyzeAsync(InputFlags, stalenessThresholdDays: 30);

        await act.Should().ThrowAsync<AiAnalysisUnavailableException>().WithMessage("*Unknown*");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenResponseIsValid_ReturnsResponseUnchangedAsync()
    {
        FlagHealthAnalysisResponse expected = CreateResponse();
        AiFlagAnalyzer analyzer = CreateAnalyzer(JsonSerializer.Serialize(expected));

        FlagHealthAnalysisResponse actual = await analyzer.AnalyzeAsync(
            InputFlags,
            stalenessThresholdDays: 30
        );

        actual.Should().BeEquivalentTo(expected);
    }

    private static AiFlagAnalyzer CreateAnalyzer(string json)
    {
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(new StubChatCompletionService(json));

        return new AiFlagAnalyzer(builder.Build(), NullLogger<AiFlagAnalyzer>.Instance);
    }

    private static FlagHealthAnalysisResponse CreateResponse(
        string summary = "Two flags analyzed.",
        List<FlagAssessment>? flags = null
    ) =>
        new()
        {
            Summary = summary,
            Flags =
                flags
                ??
                [
                    CreateAssessment("checkout-v2", "Healthy"),
                    CreateAssessment("search-ranking", "NeedsReview"),
                ],
            AnalyzedAt = DateTimeOffset.Parse("2026-04-28T12:00:00Z", CultureInfo.InvariantCulture),
            StalenessThresholdDays = 30,
        };

    private static FlagAssessment CreateAssessment(string name, string status) =>
        new()
        {
            Name = name,
            Status = status,
            Reason = "Reason.",
            Recommendation = "Recommendation.",
        };

    private static FlagResponse CreateFlag(string name) =>
        new(
            Guid.NewGuid(),
            name,
            EnvironmentType.Development,
            IsEnabled: true,
            IsArchived: false,
            RolloutStrategy.None,
            StrategyConfig: "{}",
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

    private sealed class StubChatCompletionService(string json) : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } =
            new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult<IReadOnlyList<ChatMessageContent>>([
                new ChatMessageContent(AuthorRole.Assistant, json),
            ]);

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
