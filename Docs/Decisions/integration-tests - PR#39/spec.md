# Specification: Integration Tests — Phase 1

**Document:** `Docs/Decisions/integration-tests - PR#39/spec.md`
**Status:** Ready for Implementation
**Branch:** `test/integration-tests`
**Phase:** 1 — Testing & Developer Experience
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-07

---

## Table of Contents

- [User Story](#user-story)
- [Goals and Non-Goals](#goals-and-non-goals)
- [Design Decisions](#design-decisions)
- [Test Project Setup](#test-project-setup)
- [Folder Structure](#folder-structure)
- [Conventions](#conventions)
- [Fixture: BanderasApiFactory](#fixture-featureflagapifactory)
- [Fixture: IntegrationTestBase](#fixture-integrationtestbase)
- [FlagEndpointTests](#flagendpointtests)
- [EvaluationEndpointTests](#evaluationendpointtests)
- [CI Pipeline Changes](#ci-pipeline-changes)
- [Changes to Existing Files](#changes-to-existing-files)
- [Acceptance Criteria](#acceptance-criteria)
- [Out of Scope](#out-of-scope)
- [Learning Opportunities](#learning-opportunities)
- [Instructions for Claude Code](#instructions-for-claude-code)

---

## User Story

> As a developer on Banderas, I want integration tests that exercise
> every API endpoint against a real Postgres database so that I can verify the
> full HTTP pipeline — routing, validation, serialization, persistence, error
> handling, and response shape — and catch regressions that unit tests cannot.

---

## Goals and Non-Goals

**Goals:**
- Test all 6 API endpoints through the full HTTP pipeline
- Use a real Postgres database (not an in-memory provider)
- Verify HTTP status codes, Content-Type headers, and response body structure
- Verify `ProblemDetails` shape on all error paths
- Run in CI via a new `integration-test` job
- Work identically in the devcontainer and on GitHub Actions

**Non-Goals:**
- Do not test individual strategies, validators, or the evaluator — unit tests already cover those
- Do not introduce authentication or authorization — Phase 3
- Do not add seed data — separate Phase 1 task
- Do not test concurrency or load — Phase 6
- Do not modify any existing unit test

---

## Design Decisions

### DD-1 — Separate project (`Banderas.Tests.Integration`)

The existing `Banderas.Tests` references only Application and Domain. Integration
tests require `Banderas.Api` (for `WebApplicationFactory<Program>`), which
transitively pulls in Infrastructure, EF Core, and Npgsql. Adding these to the unit
test project would blur the dependency boundary and slow down unit test builds.

A separate project keeps unit tests fast and dependency-lean.

**Trade-off:** One more project in the solution. Acceptable for clean separation.

---

### DD-2 — Testcontainers over CI service container

`Testcontainers.PostgreSql` spins up a real Postgres Docker container
programmatically. It works identically on local dev (devcontainer has
Docker-outside-of-Docker) and CI (`ubuntu-latest` has Docker pre-installed).

The alternative — a `services:` block in `ci.yml` — couples test infrastructure
to CI configuration and does not work locally without manual Docker setup.

Testcontainers is self-contained: the test project owns its database lifecycle.

**Trade-off:** Slightly slower first-run (image pull). Mitigated by Docker layer
caching on subsequent runs.

---

### DD-3 — `DELETE FROM flags` over Respawn or transaction rollback

The schema has one table (`flags`). A single `DELETE FROM flags` statement between
tests is sub-millisecond and resets state completely.

Transaction rollback was rejected because it would mask commit-level bugs — the
TOCTOU `DbUpdateException` catch in `SaveChangesAsync` only fires on a real
commit. Respawn was rejected as overkill for a single-table schema.

**Trade-off:** Tests must not run in parallel within a collection (shared DB
state). xUnit collections are sequential by default, so this is free.

---

### DD-4 — `public partial class Program { }`

`WebApplicationFactory<T>` requires the entry point's `Program` type to be
accessible. .NET top-level statements generate an implicit `internal` class.
Adding `public partial class Program { }` at the end of `Program.cs` is the
documented Microsoft approach. No `InternalsVisibleTo` needed.

---

### DD-5 — Why not `UseInMemoryDatabase`

EF Core's in-memory provider does not support Postgres-specific features used by
this project:

- `jsonb` column type for `StrategyConfig`
- Partial unique index (`HasFilter("\"IsArchived\" = false")`)
- `PostgresException` with SqlState `23505` caught by the TOCTOU handler

Using an in-memory provider would make the uniqueness constraint, the
archive-allows-reuse behavior, and the `StrategyConfig` storage silently
untested. The tests would pass but prove nothing about the actual production
data path.

---

### DD-6 — HTTPS test clients over redirect-following or middleware removal

**Finding from spec review:** the API pipeline includes `UseHttpsRedirection()`.
`WebApplicationFactory` clients default to `http://localhost`, which means tests
will see `307/308` redirects unless the client is configured deliberately.

**Options considered:**

1. Remove or conditionally disable `UseHttpsRedirection()` in the test host.
   - **Pros:** simplest test setup
   - **Cons:** integration tests would skip a real production middleware and stop
     exercising the deployed pipeline shape
2. Keep `http://localhost` and allow the client to auto-follow redirects.
   - **Pros:** minimal code in test setup
   - **Cons:** hides unexpected redirect behavior and makes failed assertions
     harder to diagnose because the client silently mutates the request flow
3. Create all test clients with `BaseAddress = https://localhost` and
   `AllowAutoRedirect = false`.
   - **Pros:** exercises the real middleware pipeline, avoids false redirect
     failures, and still surfaces any accidental future redirects immediately
   - **Cons:** slightly more explicit client setup

**Chosen solution:** Option 3.

Integration tests must create HTTPS clients explicitly and disable auto-redirects.
This keeps production middleware intact while ensuring the tests are precise: if
any endpoint starts redirecting unexpectedly, the tests fail on the redirect
instead of masking it.

---

### DD-7 — Treat `StrategyConfig: null` as a wire-contract case for `None`

**Finding from spec review:** the intended HTTP behavior for `RolloutStrategy.None`
is that `strategyConfig: null` is accepted and normalized to `"{}"` by the domain
entity. However, the request DTO constructor currently exposes `StrategyConfig`
as a non-nullable `string`, which makes typed test helpers awkward and pushes
them toward `null!`.

**Options considered:**

1. Rewrite the spec to forbid `null` and send `""` for `None`.
   - **Pros:** aligns with the DTO signature
   - **Cons:** changes the intended API contract and is risky with the `jsonb`
     column because empty string is not valid JSON
2. Change the production DTOs to `string?` in this PR.
   - **Pros:** aligns C# nullability with the wire contract
   - **Cons:** broadens this phase into an API contract refactor across
     production code, which is larger than the goal of adding integration tests
3. Keep the current runtime behavior and treat `strategyConfig: null` as an
   HTTP wire-contract case in the integration suite by sending anonymous/raw
   JSON when null must be represented.
   - **Pros:** preserves the intended API behavior, avoids broad production code
     changes, and removes repeated `null!` from typed helper code
   - **Cons:** the helper is a little less compile-time-coupled to the DTO type

**Chosen solution:** Option 3.

For this phase, integration tests will exercise the HTTP contract directly.
Helpers that need to send `strategyConfig: null` must post anonymous/raw JSON
payloads rather than instantiate `CreateFlagRequest` or `UpdateFlagRequest` with
`null!`. This localizes the DTO nullability mismatch to test infrastructure and
keeps the suite focused on real request/response behavior. DTO nullability
cleanup can be considered in a separate follow-up PR if desired.

---

### DD-8 — Expand error-path coverage and allow a targeted query-environment fix

**Finding from spec review:** the original spec promised broad `ProblemDetails`
coverage but missed several important cases:

- Invalid `environment` query values on `GET /api/flags`, `GET /api/flags/{name}`,
  `PUT /api/flags/{name}`, and `DELETE /api/flags/{name}`
- Invalid route-name coverage on `PUT` and `DELETE`
- `DELETE` not-found coverage
- Some listed error tests only asserted status code and not the response body shape

There is also a real implementation gap today: the flag endpoints accept
query-bound `EnvironmentType` values without explicit controller validation,
while body DTOs do validate environment values.

**Options considered:**

1. Keep the original spec narrow and ignore the missing cases.
   - **Pros:** smallest implementation scope
   - **Cons:** contradicts the stated goals and leaves a real API-boundary bug
     untested
2. Add the new tests but still forbid production code changes outside `Program.cs`.
   - **Pros:** preserves the original scope wording
   - **Cons:** not implementable if the new tests expose the current environment
     validation gap
3. Expand the spec slightly to allow a narrow controller-layer fix in
   `Banderas.Api/Controllers/BanderasController.cs`, then add the missing
   integration tests so the documented API contract is actually enforced.
   - **Pros:** makes the spec internally consistent, locks down the full API
     boundary, and keeps the production change tightly scoped
   - **Cons:** widens Phase 1 by one small production-code change

**Chosen solution:** Option 3.

This spec now explicitly permits a targeted controller-layer validation fix for
the `environment` query parameter in `BanderasController`. No Application,
Domain, or Infrastructure changes are allowed for this fix. The integration suite
will be expanded to cover the missing error paths and will require all `400`,
`404`, and `409` responses in scope to assert `application/problem+json` plus the
expected `ProblemDetails` or `ValidationProblemDetails` shape.

---

## Test Project Setup

### NuGet Packages

Create `Banderas.Tests.Integration/Banderas.Tests.Integration.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.9.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Banderas.Api\Banderas.Api.csproj" />
  </ItemGroup>
</Project>
```

> **NOTE:** `Microsoft.AspNetCore.Mvc.Testing` and `Testcontainers.PostgreSql` do
> not specify a version — use the latest stable at time of implementation. Do not
> pin to a specific version unless a compatibility issue is discovered.

> **NOTE:** The project references `Banderas.Api` only. This transitively
> brings Application, Domain, and Infrastructure. Do not add direct references
> to lower-layer projects.

### Solution Registration

Add `Banderas.Tests.Integration` to `Banderas.sln`. Use:

```bash
dotnet sln Banderas.sln add Banderas.Tests.Integration/Banderas.Tests.Integration.csproj
```

---

## Folder Structure

Create the following files. Do not create any other files or folders.

```
Banderas.Tests.Integration/
├── Banderas.Tests.Integration.csproj       ← NEW
├── Fixtures/
│   ├── BanderasApiFactory.cs               ← NEW
│   ├── IntegrationTestCollection.cs           ← NEW
│   └── IntegrationTestBase.cs                 ← NEW
├── FlagEndpointTests.cs                       ← NEW
└── EvaluationEndpointTests.cs                 ← NEW
```

---

## Conventions

### Trait decoration

Every test class and every test method must carry `[Trait("Category", "Integration")]`.
The CI `build-test` job excludes this trait. The new `integration-test` job
includes it.

```csharp
[Trait("Category", "Integration")]
public sealed class FlagEndpointTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateFlag_ValidRequest_Returns201WithLocationHeader() { ... }
}
```

### Naming convention — `MethodName_StateUnderTest_ExpectedBehavior`

```
CreateFlag_ValidRequest_Returns201WithLocationHeader
GetAllFlags_NoFlags_Returns200EmptyArray
Evaluate_DisabledFlag_ReturnsFalse
```

### Arrange-Act-Assert (AAA)

Every test body follows three clearly separated sections:

```csharp
// Arrange
var request = new CreateFlagRequest(
    "dark-mode",
    EnvironmentType.Development,
    true,
    RolloutStrategy.Percentage,
    """{"percentage": 50}"""
);

// Act
var response = await Client.PostAsJsonAsync("/api/flags", request, JsonOptions);

// Assert
response.StatusCode.Should().Be(HttpStatusCode.Created);
```

### FluentAssertions usage

Use FluentAssertions for all assertions. Do not use `Assert.True`, `Assert.Equal`,
or any other xUnit built-in assertion method.

### Async throughout

All test methods are `async Task`. All HTTP calls use `await`. Do not use
`.Result` or `.GetAwaiter().GetResult()`.

---

## Fixture: BanderasApiFactory

**File:** `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs`

A custom `WebApplicationFactory<Program>` that manages a Testcontainers
Postgres instance and wires it into the ASP.NET Core host.

### Behavior

1. Implements `IAsyncLifetime`
2. `InitializeAsync`:
   - Starts a `PostgreSqlContainer` (image: `postgres:16` — matching `docker-compose.yml`)
   - Calls `base.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false })`
     once to trigger host startup after the container is ready
3. `ConfigureWebHost` override:
   - Removes the existing `DbContextOptions<BanderasDbContext>` registration
   - Registers a new `AddDbContext<BanderasDbContext>` pointing at the
     Testcontainer's connection string
   - Creates a scope, resolves `BanderasDbContext`, calls `Database.MigrateAsync()`
     to apply EF Core migrations
4. `DisposeAsync`:
   - Disposes the Postgres container

### Implementation guidance

```csharp
using DotNet.Testcontainers.Builders;
using Banderas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Banderas.Tests.Integration.Fixtures;

public sealed class BanderasApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the app's BanderasDbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<BanderasDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            // Register with Testcontainer connection string
            services.AddDbContext<BanderasDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _ = base.CreateClient(ClientOptions);

        // Apply migrations once after container starts
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BanderasDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

> **NOTE FOR CLAUDE CODE:** The above is guidance, not a copy-paste template.
> Check the actual Testcontainers.PostgreSql API at implementation time. The
> builder API may differ slightly between versions. Use the latest documented
> approach.

> **IMPORTANT:** All test clients must target `https://localhost` and must set
> `AllowAutoRedirect = false`. Do not rely on HTTP-to-HTTPS redirect-following in
> assertions.

> **IMPORTANT:** The `ConfigureWebHost` override must also remove any existing
> `BanderasDbContext` service registration (not just `DbContextOptions`).
> Check for both. If only `DbContextOptions` is removed but `BanderasDbContext`
> itself is also registered as a service type, remove that too. Test by running
> a single test — if it fails with a connection error to `Host=postgres`, the
> replacement did not take effect.

---

## Fixture: IntegrationTestCollection

**File:** `Banderas.Tests.Integration/Fixtures/IntegrationTestCollection.cs`

Defines an xUnit collection that shares a single `BanderasApiFactory`
across all test classes. This means one Postgres container for the entire
test suite.

```csharp
namespace Banderas.Tests.Integration.Fixtures;

[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<BanderasApiFactory>;
```

Both test classes must be decorated with `[Collection("Integration")]`.

---

## Fixture: IntegrationTestBase

**File:** `Banderas.Tests.Integration/Fixtures/IntegrationTestBase.cs`

Base class for all integration test classes. Provides an `HttpClient`,
JSON serialization options matching the API, and per-test database cleanup.

### Behavior

1. Receives `BanderasApiFactory` via constructor injection (xUnit collection fixture)
2. Implements `IAsyncLifetime`
3. `InitializeAsync`: Runs `DELETE FROM flags` via a scoped `BanderasDbContext`
4. `DisposeAsync`: No-op
5. Exposes:
   - `Client` — `HttpClient` from the factory, created with
     `BaseAddress = https://localhost` and `AllowAutoRedirect = false`
   - `JsonOptions` — `JsonSerializerOptions` with `JsonStringEnumConverter` and
     `PropertyNameCaseInsensitive = true`

### Implementation guidance

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Banderas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Banderas.Tests.Integration.Fixtures;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected HttpClient Client { get; }

    protected static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly BanderasApiFactory _factory;

    protected IntegrationTestBase(BanderasApiFactory factory)
    {
        _factory = factory;
        Client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BanderasDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM flags");
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

> **NOTE:** `DELETE FROM flags` uses the table name from `FlagConfiguration`
> (`builder.ToTable("flags")`). If the table name changes, this must be
> updated. This is a deliberate coupling to the database schema — acceptable
> for test infrastructure.

> **NOTE:** `ExecuteSqlRawAsync("DELETE FROM flags")` is safe here — no user
> input is involved. This is the one acceptable use of raw SQL in the project.

---

## FlagEndpointTests

**File:** `Banderas.Tests.Integration/FlagEndpointTests.cs`

Tests all 5 `api/flags` endpoints: POST (create), GET all, GET by name,
PUT (update), DELETE (archive).

### Helper: CreateFlagAsync

All tests that need a flag in the database should call a private helper method
that sends `POST /api/flags` and asserts `201`. This avoids duplicating the
setup logic and ensures test independence — every test creates its own data.

Because the HTTP contract for `StrategyType: None` allows `strategyConfig: null`,
this helper intentionally posts JSON payloads rather than constructing
`CreateFlagRequest` directly. That avoids repeated `null!` usage in test code and
keeps the integration suite focused on the real wire contract.

```csharp
private async Task<FlagResponse> CreateFlagAsync(
    string name = "test-flag",
    EnvironmentType environment = EnvironmentType.Development,
    bool isEnabled = true,
    RolloutStrategy strategyType = RolloutStrategy.None,
    string? strategyConfig = null)
{
    var payload = new
    {
        Name = name,
        Environment = environment,
        IsEnabled = isEnabled,
        StrategyType = strategyType,
        StrategyConfig = strategyConfig,
    };

    var response = await Client.PostAsync(
        "/api/flags",
        JsonContent.Create(payload, options: JsonOptions)
    );
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var body = await response.Content.ReadFromJsonAsync<FlagResponse>(JsonOptions);
    return body!;
}
```

### Tests to implement

**Error assertion rule for this section:** every `400`, `404`, and `409` test
must assert:

- `Content-Type` is `application/problem+json`
- Body deserializes to `ProblemDetails` or `ValidationProblemDetails` as appropriate
- `Status` matches the HTTP status code
- `Detail` or `Errors` contains the expected signal for that path

**FE-1 — Create flag with valid request returns 201 with Location header**

```
CreateFlag_ValidRequest_Returns201WithLocationHeader
```
POST `/api/flags` with a valid `CreateFlagRequest` (name: `"feature-one"`,
environment: `Development`, enabled: `true`, strategy: `None`, config: `null`).
Assert:
- Status code is `201 Created`
- `Location` header is present and contains the flag name
- Response body deserializes to `FlagResponse` with matching `Name`, `Environment`,
  `IsEnabled`, `StrategyType`
- Response body `StrategyConfig` is `"{}"`
- `Id` is a non-empty `Guid`
- `CreatedAt` and `UpdatedAt` fall within a captured `before/after` UTC window

---

**FE-2 — Create flag with None strategy and null config returns 201**

```
CreateFlag_NoneStrategyNullConfig_Returns201
```
POST with `StrategyType: None`, `StrategyConfig: null`.
Assert status code `201`. Assert response body `StrategyConfig` is `"{}"`.

> This verifies the wire contract for `strategyConfig: null` and the domain's
> `?? "{}"` normalization fallback.

---

**FE-3 — Create flag with invalid name returns 400 with validation errors**

```
CreateFlag_InvalidName_Returns400WithValidationErrors
```
POST with `Name: "invalid flag!"` (contains space and exclamation mark).
Assert:
- Status code is `400`
- Content-Type is `application/problem+json`
- Response body deserializes to `ValidationProblemDetails`
- `Errors` dictionary contains key `"Name"`

---

**FE-4 — Create duplicate flag returns 409**

```
CreateFlag_DuplicateNameAndEnvironment_Returns409
```
Create a flag via `CreateFlagAsync`. POST again with the same name and environment.
Assert:
- Status code is `409`
- Content-Type is `application/problem+json`
- Response body contains `"already exists"` in the `Detail` field

---

**FE-5 — Create flag with invalid Percentage config returns 400**

```
CreateFlag_InvalidPercentageConfig_Returns400
```
POST with `StrategyType: Percentage`, `StrategyConfig: """{"roles": ["Admin"]}"""`
(wrong shape for Percentage).
Assert status code `400`. Assert error on `"StrategyConfig"`.

---

**FE-6 — Get all flags returns 200 with list**

```
GetAllFlags_WithFlags_Returns200WithList
```
Create 2 flags via `CreateFlagAsync` with different names, same environment.
GET `/api/flags?environment=Development`.
Assert:
- Status code is `200`
- Response body deserializes to `FlagResponse[]` with length 2

---

**FE-7 — Get all flags with no data returns 200 with empty array**

```
GetAllFlags_NoFlags_Returns200EmptyArray
```
GET `/api/flags?environment=Development` without creating any flags.
Assert:
- Status code is `200`
- Response body is an empty array

---

**FE-8 — Get all flags filters by environment**

```
GetAllFlags_FiltersByEnvironment_ReturnsOnlyMatching
```
Create a flag in `Development` and a flag in `Staging`.
GET `/api/flags?environment=Development`.
Assert:
- Response contains 1 flag
- That flag's environment is `Development`

---

**FE-9 — Get all flags excludes archived flags**

```
GetAllFlags_ExcludesArchivedFlags
```
Create a flag. Archive it via `DELETE /api/flags/{name}?environment=Development`.
GET `/api/flags?environment=Development`.
Assert response is an empty array.

---

**FE-10 — Get flag by name returns 200 with correct body**

```
GetFlagByName_Exists_Returns200WithCorrectBody
```
Create a flag via `CreateFlagAsync`. GET `/api/flags/{name}?environment=Development`.
Assert:
- Status code is `200`
- Response body `Name` matches
- Response body `Environment` matches

---

**FE-11 — Get flag by name returns 404 when not found**

```
GetFlagByName_NotFound_Returns404ProblemDetails
```
GET `/api/flags/nonexistent?environment=Development`.
Assert:
- Status code is `404`
- Content-Type is `application/problem+json`
- Response body is a `ProblemDetails` with `Status == 404`

---

**FE-12 — Get flag by name returns 400 for invalid route parameter**

```
GetFlagByName_InvalidRouteName_Returns400ProblemDetails
```
GET `/api/flags/invalid%20name!?environment=Development`.
Assert:
- Status code is `400`
- Content-Type is `application/problem+json`

> This exercises the `RouteParameterGuard.ValidateName` path.

---

**FE-13 — Update flag returns 204**

```
UpdateFlag_ValidRequest_Returns204
```
Create a flag (enabled, None strategy). PUT `/api/flags/{name}?environment=Development`
with `UpdateFlagRequest(IsEnabled: false, StrategyType: Percentage, StrategyConfig: """{"percentage": 50}""")`.
Assert status code `204`.
GET the flag and verify it reflects the updated values.

---

**FE-14 — Update flag returns 404 when not found**

```
UpdateFlag_NotFound_Returns404
```
PUT `/api/flags/nonexistent?environment=Development` with a valid body.
Assert:
- Status code is `404`
- Content-Type is `application/problem+json`

---

**FE-15 — Update flag with invalid config returns 400**

```
UpdateFlag_InvalidStrategyConfig_Returns400
```
Create a flag. PUT with `StrategyType: RoleBased`, `StrategyConfig: """{"percentage": 50}"""`
(wrong shape for RoleBased).
Assert status code `400`.

---

**FE-16 — Archive flag returns 204 and excludes from get all**

```
ArchiveFlag_Exists_Returns204AndExcludedFromGetAll
```
Create a flag. DELETE `/api/flags/{name}?environment=Development`.
Assert status code `204`.
GET `/api/flags?environment=Development`.
Assert the archived flag is not in the list.

---

**FE-17 — Archive allows name reuse**

```
ArchiveFlag_AllowsNameReuse_ReturnsCreatedOnRecreate
```
Create a flag. Archive it. Create a new flag with the same name and environment.
Assert status code `201` on the second create.

> This verifies the partial unique index behavior — `HasFilter("\"IsArchived\" = false")`
> allows archived flags to coexist with active flags of the same name.

---

**FE-18 — Get all flags with invalid environment returns 400**

```
GetAllFlags_InvalidEnvironment_Returns400ProblemDetails
```
GET `/api/flags?environment=None`.
Assert:
- Status code is `400`
- Body is `ProblemDetails` with `Status == 400`
- `Detail` explains that a valid environment is required

---

**FE-19 — Get flag by name with invalid environment returns 400**

```
GetFlagByName_InvalidEnvironment_Returns400ProblemDetails
```
GET `/api/flags/test-flag?environment=None`.
Assert:
- Status code is `400`
- Body is `ProblemDetails` with `Status == 400`
- `Detail` explains that a valid environment is required

---

**FE-20 — Update flag with invalid route parameter returns 400**

```
UpdateFlag_InvalidRouteName_Returns400ProblemDetails
```
PUT `/api/flags/invalid%20name!?environment=Development` with a valid body.
Assert:
- Status code is `400`
- Body is `ProblemDetails` with `Status == 400`
- `Detail` contains the allowlist validation message

---

**FE-21 — Update flag with invalid environment returns 400**

```
UpdateFlag_InvalidEnvironment_Returns400ProblemDetails
```
PUT `/api/flags/test-flag?environment=None` with a valid body.
Assert:
- Status code is `400`
- Body is `ProblemDetails` with `Status == 400`
- `Detail` explains that a valid environment is required

---

**FE-22 — Archive flag returns 404 when not found**

```
ArchiveFlag_NotFound_Returns404ProblemDetails
```
DELETE `/api/flags/nonexistent?environment=Development`.
Assert:
- Status code is `404`
- Body is `ProblemDetails` with `Status == 404`
- `Detail` contains the missing-flag message

---

**FE-23 — Archive flag with invalid route parameter returns 400**

```
ArchiveFlag_InvalidRouteName_Returns400ProblemDetails
```
DELETE `/api/flags/invalid%20name!?environment=Development`.
Assert:
- Status code is `400`
- Body is `ProblemDetails` with `Status == 400`
- `Detail` contains the allowlist validation message

---

**FE-24 — Archive flag with invalid environment returns 400**

```
ArchiveFlag_InvalidEnvironment_Returns400ProblemDetails
```
DELETE `/api/flags/test-flag?environment=None`.
Assert:
- Status code is `400`
- Body is `ProblemDetails` with `Status == 400`
- `Detail` explains that a valid environment is required

---

## EvaluationEndpointTests

**File:** `Banderas.Tests.Integration/EvaluationEndpointTests.cs`

Tests the `POST /api/evaluate` endpoint.

### Helper: CreateFlagAsync

Use the same private helper pattern as `FlagEndpointTests`.

### Tests to implement

**Error assertion rule for this section:** every `400` and `404` test must assert
`Content-Type: application/problem+json`, deserialize the response body, and
verify `Status` plus the relevant `Detail` or `Errors` content.

**EV-1 — Enabled flag with None strategy returns true**

```
Evaluate_EnabledNoneStrategy_ReturnsTrue
```
Create an enabled flag with `RolloutStrategy.None`.
POST `/api/evaluate` with matching flag name, userId, and environment.
Assert:
- Status code is `200`
- Response body `IsEnabled` is `true`

---

**EV-2 — Disabled flag returns false**

```
Evaluate_DisabledFlag_ReturnsFalse
```
Create a disabled flag (`isEnabled: false`).
POST `/api/evaluate`.
Assert response `IsEnabled` is `false`.

---

**EV-3 — Percentage strategy returns deterministic result**

```
Evaluate_PercentageStrategy_ReturnsDeterministicResult
```
Create a flag with `RolloutStrategy.Percentage`, config: `"""{"percentage": 50}"""`.
POST `/api/evaluate` twice with the same userId.
Assert both responses return the same `IsEnabled` value.

> This is the HTTP-level equivalent of PS-7 from the unit tests. It verifies
> determinism survives the full pipeline including sanitization.

---

**EV-4 — Role strategy with matching role returns true**

```
Evaluate_RoleStrategy_MatchingRole_ReturnsTrue
```
Create a flag with `RolloutStrategy.RoleBased`, config: `"""{"roles": ["Admin"]}"""`.
POST `/api/evaluate` with `UserRoles: ["Admin"]`.
Assert response `IsEnabled` is `true`.

---

**EV-5 — Role strategy with no matching role returns false**

```
Evaluate_RoleStrategy_NoMatchingRole_ReturnsFalse
```
Create a flag with `RolloutStrategy.RoleBased`, config: `"""{"roles": ["Admin"]}"""`.
POST `/api/evaluate` with `UserRoles: ["Viewer"]`.
Assert response `IsEnabled` is `false`.

---

**EV-6 — Evaluate non-existent flag returns 404**

```
Evaluate_FlagNotFound_Returns404
```
POST `/api/evaluate` with a flag name that does not exist.
Assert:
- Status code is `404`
- Content-Type is `application/problem+json`

---

**EV-7 — Evaluate with missing UserId returns 400**

```
Evaluate_MissingUserId_Returns400
```
POST `/api/evaluate` with `UserId: ""`.
Assert:
- Status code is `400`
- Content-Type is `application/problem+json`
- `Errors` dictionary contains key `"UserId"`

---

## CI Pipeline Changes

### New job: `integration-test`

Add a new job to `.github/workflows/ci.yml` that runs integration tests.
This job runs in parallel with `lint-format` and `build-test` — no `needs:`
dependency.

```yaml
  # ============================================================
  # Job 3: Integration Tests
  # Runs integration tests against a real Postgres database via
  # Testcontainers. Requires Docker — available on ubuntu-latest.
  # Runs in parallel with lint-format and build-test.
  # ============================================================
  integration-test:
    name: Integration Tests
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Restore packages
        run: dotnet restore Banderas.sln

      - name: Run integration tests
        run: >
          dotnet test Banderas.sln
          --no-restore
          --filter "Category=Integration"
          --logger "console;verbosity=normal"
```

### Update `ai-review` dependency

The `ai-review` job must wait for integration tests to pass:

```yaml
  ai-review:
    needs: [lint-format, build-test, integration-test]
```

---

## Changes to Existing Files

### `Banderas.Api/Program.cs`

Add to the very end of the file, after `app.Run();`:

```csharp
public partial class Program { }
```

This exposes the `Program` type to `WebApplicationFactory<Program>` in the
test project. It is the documented Microsoft approach for integration testing
with top-level statements. It does not change runtime behavior.

---

### `Banderas.Api/Controllers/BanderasController.cs`

Add a narrow controller-layer validation guard for the `environment` query
parameter on `GetAllAsync`, `GetByNameAsync`, `UpdateAsync`, and `ArchiveAsync`.
If `environment == EnvironmentType.None`, return the same `400`-class behavior
used elsewhere in the API boundary (`ProblemDetails` via the global exception
middleware or equivalent controller-level rejection).

This is the only production-code scope expansion beyond `Program.cs`, and it is
allowed because the integration spec now explicitly requires end-to-end coverage
for invalid query-environment handling on all flag endpoints.

---

### `Banderas.sln`

Add the new project:

```bash
dotnet sln Banderas.sln add Banderas.Tests.Integration/Banderas.Tests.Integration.csproj
```

---

### `.github/workflows/ci.yml`

See [CI Pipeline Changes](#ci-pipeline-changes) for the full diff.

---

## Acceptance Criteria

The implementation is complete when **all** of the following are true:

- [ ] `Banderas.Tests.Integration/Banderas.Tests.Integration.csproj` exists with correct packages and project reference
- [ ] `Banderas.Tests.Integration` is registered in `Banderas.sln`
- [ ] `BanderasApiFactory.cs` starts a Testcontainers Postgres, applies EF Core migrations, and boots the host with an HTTPS client configuration
- [ ] `IntegrationTestCollection.cs` defines the shared `"Integration"` collection
- [ ] `IntegrationTestBase.cs` provides an HTTPS `HttpClient`, `JsonOptions`, and per-test `DELETE FROM flags` cleanup
- [ ] `Program.cs` ends with `public partial class Program { }`
- [ ] `BanderasController.cs` rejects invalid query `environment` values with `400` `ProblemDetails`
- [ ] Every test class and method carries `[Trait("Category", "Integration")]`
- [ ] Every test class is decorated with `[Collection("Integration")]`
- [ ] All assertions use FluentAssertions — no `Assert.*` calls
- [ ] All test methods follow the `MethodName_StateUnderTest_ExpectedBehavior` naming convention
- [ ] `FlagEndpointTests`: all 24 tests pass
- [ ] `EvaluationEndpointTests`: all 7 tests pass
- [ ] `dotnet test Banderas.sln --filter "Category=Integration"` exits 0
- [ ] `dotnet test Banderas.sln --filter "Category!=Integration"` still passes (existing unit tests unbroken)
- [ ] `dotnet build Banderas.sln -p:TreatWarningsAsErrors=true` exits 0
- [ ] `dotnet csharpier check .` exits 0
- [ ] `ci.yml` contains `integration-test` job with `--filter "Category=Integration"`
- [ ] `ai-review` job has `needs: [lint-format, build-test, integration-test]`
- [ ] Error responses assert `Content-Type: application/problem+json`
- [ ] Error responses also assert the expected `ProblemDetails` or `ValidationProblemDetails` body shape

**Total expected test count: 31 integration tests.**

---

## Out of Scope

The following are explicitly **not** part of this PR:

- Unit tests (already complete in PR #38)
- Authentication or authorization testing (Phase 3)
- Rate limiting testing (Phase 3)
- Load or performance testing (Phase 6)
- Seed data for local development (separate Phase 1 task)
- `.http` smoke test file (separate Phase 1 task)
- Evaluation decision logging (separate Phase 1 task)
- Changes to any production code other than `Program.cs` and the targeted
  `BanderasController.cs` query-environment validation fix described in
  DD-8
- Any changes to `Banderas.Tests/` (existing unit test project)

---

## Learning Opportunities

**1. WebApplicationFactory — the in-process test server**

`WebApplicationFactory<Program>` hosts the entire ASP.NET Core app in-process.
HTTP requests from the test's `HttpClient` go through the full middleware
pipeline — routing, model binding, validation, exception middleware,
serialization — without a TCP socket. This means integration tests run at
near-unit-test speed while exercising the real HTTP pipeline.

The key extension point is `ConfigureWebHost`, which lets you override DI
registrations (like swapping the database) without changing production code.

**2. Testcontainers — disposable infrastructure**

Testcontainers manages Docker containers as test fixtures. The lifecycle is:
start container → run tests → destroy container. No manual Docker commands,
no orphaned containers, no port conflicts. The connection string is dynamically
generated — tests never hardcode ports or credentials.

This pattern eliminates "works on my machine" test infrastructure problems.
The same test runs identically on a developer's laptop and in CI.

**3. xUnit collection fixtures — shared expensive resources**

xUnit creates test class instances per-test-method by default. Without a
collection fixture, each test class would spin up its own Postgres container.
The `[CollectionDefinition]` + `ICollectionFixture<T>` pattern shares a single
container across all classes in the collection, amortizing the ~5-second
startup cost.

The tradeoff: tests in the same collection run sequentially and share state.
The `DELETE FROM flags` cleanup in `IntegrationTestBase` handles the shared
state concern.

---

## Instructions for Claude Code

Read the following files in order before writing any code:

1. `CLAUDE.md`
2. `Docs/roadmap.md`
3. `Docs/current-state.md`
4. `Docs/architecture.md`
5. This file

Then implement in this order:

1. Add `public partial class Program { }` to the end of `Banderas.Api/Program.cs`
2. Add the targeted query-`environment` validation fix to `Banderas.Api/Controllers/BanderasController.cs`
3. Create `Banderas.Tests.Integration/Banderas.Tests.Integration.csproj`
4. Run `dotnet sln Banderas.sln add Banderas.Tests.Integration/Banderas.Tests.Integration.csproj`
5. Create `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs`
6. Create `Banderas.Tests.Integration/Fixtures/IntegrationTestCollection.cs`
7. Create `Banderas.Tests.Integration/Fixtures/IntegrationTestBase.cs`
8. Create `Banderas.Tests.Integration/FlagEndpointTests.cs`
9. Create `Banderas.Tests.Integration/EvaluationEndpointTests.cs`
10. Run `dotnet build Banderas.sln -p:TreatWarningsAsErrors=true` — fix all warnings
11. Run `dotnet test Banderas.sln --filter "Category=Integration"` — all 31 tests must pass
12. Run `dotnet test Banderas.sln --filter "Category!=Integration"` — existing unit tests must still pass
13. Run `dotnet csharpier format .`
14. Update `.github/workflows/ci.yml` — add `integration-test` job, update `ai-review` needs

**DO NOT:**
- Modify any file in `Banderas.Domain/`, `Banderas.Application/`, or `Banderas.Infrastructure/`
- Modify any file in `Banderas.Api/` other than `Program.cs` and the targeted
  `BanderasController.cs` query-environment validation fix described in DD-8
- Modify any file in `Banderas.Tests/` (existing unit test project)
- Use `Assert.*` — use FluentAssertions only
- Use `UseInMemoryDatabase` — use Testcontainers with real Postgres only
- Add `try/catch` blocks anywhere in the test project
- Hardcode ports or connection strings — use Testcontainer's dynamic connection string
- Use `FluentValidation.AspNetCore` or any package not listed in this spec

---

*Banderas | test/integration-tests | Phase 1*
