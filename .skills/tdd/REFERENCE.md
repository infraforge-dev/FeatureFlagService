# TDD Reference — .NET Stack

## Tooling

| Layer | Tools |
|---|---|
| Unit tests | xUnit · Moq · FluentAssertions |
| Integration tests | WebApplicationFactory · xUnit |
| Database tests | TestContainers (Postgres/SQL Server) |
| Coverage | Coverlet + `dotnet test --collect:"XPlat Code Coverage"` |

---

## Unit test — domain logic

Use for: entities, value objects, domain services, application handlers.

```csharp
// FeatureFlag_Enable_WhenAlreadyArchived_ThrowsDomainException.cs
public class FeatureFlagTests
{
    [Fact]
    public void Enable_WhenFlagIsArchived_ThrowsDomainException()
    {
        // Arrange
        var flag = FeatureFlag.Create("dark-mode", "UI");
        flag.Archive();

        // Act
        var act = () => flag.Enable();

        // Assert
        act.Should().Throw<DomainException>()
           .WithMessage("*archived*");
    }
}
```

**Rules:**
- No I/O, no database, no HTTP. Pure logic only.
- Mock external dependencies with Moq. Only mock what you own.
- One `[Fact]` per behaviour. Use `[Theory]` + `[InlineData]` for data-driven cases.

---

## Integration test — HTTP layer

Use for: API endpoints, middleware behaviour, auth flows.

```csharp
public class CreateFlagEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CreateFlagEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Swap real DB for in-memory or TestContainers
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(o =>
                    o.UseInMemoryDatabase("test"));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task CreateFlag_WithValidPayload_Returns201()
    {
        // Arrange
        var payload = new { name = "dark-mode", environment = "production" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

**Rules:**
- Test the HTTP contract: status codes, response shape, headers.
- Don't assert on implementation details inside the handler.
- Use `IClassFixture` to share the factory across tests in a class (expensive to spin up).

---

## Database test — EF Core with TestContainers

Use for: repository queries, EF Core projections, migration smoke tests.

```csharp
public class FeatureFlagRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _context = new AppDbContext(options);
        await _context.Database.MigrateAsync();
    }

    [Fact]
    public async Task GetActiveFlags_ReturnsOnlyEnabledFlags()
    {
        // Arrange
        _context.Flags.AddRange(
            FeatureFlag.Create("enabled-flag", "prod").Tap(f => f.Enable()),
            FeatureFlag.Create("disabled-flag", "prod")
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Flags.Where(f => f.IsEnabled).ToListAsync();

        // Assert
        result.Should().ContainSingle(f => f.Name == "enabled-flag");
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
```

---

## Naming convention

```
MethodOrBehaviour_Scenario_ExpectedOutcome

Enable_WhenFlagIsArchived_ThrowsDomainException
CreateFlag_WithValidPayload_Returns201
GetActiveFlags_WhenNoneExist_ReturnsEmptyList
```

---

## Test directory structure

Mirror the source structure under a sibling `.Tests` project:

```
src/
  Banderas.Domain/
  Banderas.Application/
  Banderas.Api/
tests/
  Banderas.Domain.Tests/
  Banderas.Application.Tests/
  Banderas.Api.IntegrationTests/
```

---

## FluentAssertions quick reference

```csharp
result.Should().Be(expected);
result.Should().BeEquivalentTo(expected);         // deep equality
result.Should().NotBeNull();
list.Should().HaveCount(3);
list.Should().ContainSingle(x => x.Id == id);
act.Should().Throw<DomainException>();
act.Should().NotThrow();
await act.Should().ThrowAsync<Exception>();
```

---

## Moq quick reference

```csharp
var repo = new Mock<IFeatureFlagRepository>();

// Setup
repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(flag);
repo.Setup(r => r.SaveAsync(It.IsAny<FeatureFlag>())).Returns(Task.CompletedTask);

// Verify
repo.Verify(r => r.SaveAsync(It.Is<FeatureFlag>(f => f.Name == "dark-mode")), Times.Once);
```

**Only mock what you own.** Don't mock `HttpClient`, `DbContext`, or third-party types directly — wrap them in interfaces you control.
