using Banderas.Domain.Entities;
using Banderas.Domain.Enums;
using Banderas.Domain.Exceptions;
using Banderas.Domain.Interfaces;
using Banderas.Domain.ValueObjects;
using Banderas.Infrastructure.Persistence;
using Banderas.Tests.Integration.Fixtures;
using FluentAssertions;
using FluentAssertions.Specialized;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Banderas.Tests.Integration;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FlagConcurrencyTokenTests : IntegrationTestBase
{
    private readonly BanderasApiFactory _factory;

    public FlagConcurrencyTokenTests(BanderasApiFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentUpdate_SecondSave_ThrowsFlagConcurrencyExceptionAsync()
    {
        // Arrange — seed a single flag we can race against.
        Guid flagId;
        using (IServiceScope seedScope = _factory.Services.CreateScope())
        {
            BanderasDbContext db =
                seedScope.ServiceProvider.GetRequiredService<BanderasDbContext>();
            Flag seed = new Flag(
                "concurrency-token-test",
                EnvironmentType.Development,
                isEnabled: false,
                RolloutStrategy.None,
                strategyConfig: StrategyConfig.Create(RolloutStrategy.None, "{}"),
                variations:
                [
                    new("off", VariationKind.Boolean, "false"),
                    new("on", VariationKind.Boolean, "true"),
                ]
            );
            db.Flags.Add(seed);
            await db.SaveChangesAsync();
            flagId = seed.Id;
        }

        // Act — two scopes load the same flag, both mutate, both save.
        using IServiceScope scopeA = _factory.Services.CreateScope();
        using IServiceScope scopeB = _factory.Services.CreateScope();

        BanderasDbContext dbA = scopeA.ServiceProvider.GetRequiredService<BanderasDbContext>();
        BanderasDbContext dbB = scopeB.ServiceProvider.GetRequiredService<BanderasDbContext>();
        IBanderasRepository repoB =
            scopeB.ServiceProvider.GetRequiredService<IBanderasRepository>();

        Flag flagA = (await dbA.Flags.FindAsync(flagId))!;
        Flag flagB = (await dbB.Flags.FindAsync(flagId))!;

        flagA.Reconfigure(isEnabled: true, RolloutStrategy.None, flagA.StrategyConfig);
        await dbA.SaveChangesAsync();

        flagB.UpdateName("renamed-by-loser");

        // Assert — repository surfaces concurrency conflict as a domain exception.
        Func<Task> act = async () => await repoB.SaveChangesAsync();
        ExceptionAssertions<FlagConcurrencyException> thrown = await act.Should()
            .ThrowAsync<FlagConcurrencyException>();
        thrown.Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }
}
