# Persistence & Controllers — Implementation Spec

**Branch:** `feature/persistence-and-controllers`
**Phase:** 0 — Foundation (Completion)
**Status:** Ready for implementation
**Revision:** v2 — updated after Senior Engineer code review
**Implementation notes:** `docs/Decisions/persistence-and-controllers/implementation-notes.md`

[Pull Request #24](https://github.com/amodelandme/Bandera/pull/24)

---

## 1. Purpose of This Document

This spec covers the final Phase 0 work: the persistence layer (EF Core + Postgres),
async interface updates, DTOs, controllers, and Swagger wiring.

When this session is complete, Phase 0 is done — the API will be running, connected
to a real database, and fully documented via Swagger.

> **SCOPE**
> This spec covers: `docker-compose.yml`, `BanderaDbContext`, `FlagConfiguration`,
> `BanderaRepository`, async updates to `IBanderaRepository` and
> `IBanderaService`, DTOs, `BanderasController`, `EvaluationController`,
> Swagger wiring, and the OpenApi package upgrade.
>
> It does **not** cover: authentication, validation beyond basic null checks,
> integration tests, or caching. Those are Phase 1+.

### v2 Changes Summary

Five issues were raised in code review and addressed in this revision:

| # | Issue | Resolution |
|---|---|---|
| 1 | Unique index breaks soft delete | Fixed — partial index with `HasFilter` |
| 2 | No CancellationToken in async stack | Fixed — added throughout all async signatures |
| 3 | Double `UpdatedAt` in `UpdateFlagAsync` | Fixed — added `Flag.Update()` atomic method |
| 4 | `EF Core Design` package missing `PrivateAssets` | Fixed — correct package declaration |
| 5 | Migrations fragile without design-time factory | Fixed — added `IDesignTimeDbContextFactory` |
| 6 | Evaluation returns 200 for unknown flags | Fixed — returns 404 for unknown flag, 200 for known disabled flag |
| 7 | `appsettings.Development.json` not in `.gitignore` | Deliberate — committed with explanatory comment |

---

## 2. Architecture Context

Clean Architecture dependency rules remain unchanged. Review before writing any code.

| Layer | Responsibility |
|---|---|
| `Bandera.Domain` | Entities, enums, value objects, interfaces. Zero outward dependencies. |
| `Bandera.Application` | Use cases, service interfaces, evaluator, strategies, DTOs. Depends on Domain only. |
| `Bandera.Infrastructure` | EF Core, DbContext, repositories. Depends on Domain + Application. |
| `Bandera.Api` | Controllers, Program.cs, DI wiring. Depends on Application + Infrastructure. |
| `Bandera.Tests` | Unit tests. Depends on Domain + Application. |

> **RULE**
> `BanderaDbContext` and `FlagConfiguration` live in **Infrastructure**.
> DTOs live in **Application**.
> Controllers live in **Api**.
> Domain entities are never returned directly from controllers — always map to DTOs.

---

## 3. NuGet Packages

### Infrastructure project — add these packages:

```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />

<!-- PrivateAssets="all" is required — this is build-time tooling only.         -->
<!-- It must not flow as a transitive dependency to consumers of Infrastructure. -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.5">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

> **NOTE on versions:** Use the latest stable versions available at time of implementation.
> Run `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL` and let NuGet resolve
> the best version for `net10.0`.

### Api project — upgrade existing package:

```xml
<!-- Change this: -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.3" />

<!-- To this (resolves KI-004): -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.5" />
```

> Use the latest available `10.x` version. Run
> `dotnet add package Microsoft.AspNetCore.OpenApi` in the Api project to let NuGet
> resolve the correct version.

---

## 4. Docker Compose

**File location:** `docker-compose.yml` (repo root)

Create this file. It provides a local Postgres instance for development.

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: featureflags
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

**Usage:**
```bash
docker compose up -d    # start in background
docker compose down     # stop
```

---

## 5. Configuration

### appsettings.Development.json

Add the connection string. Replace the entire file contents.

> **NOTE — This file is intentionally committed to the repository.**
> These are non-sensitive local development defaults that only work against
> a local Docker container (`postgres/postgres`). Committing this file means
> any developer who clones the repo gets a working dev setup immediately
> without any manual configuration.
>
> This is a deliberate choice for a portfolio/open-source project. In a
> production codebase with real credentials, this file would be gitignored
> and each developer would maintain their own local copy. Production secrets
> always go in environment variables or Azure Key Vault — never in any
> appsettings file.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    // Local Docker Postgres — safe to commit, non-sensitive dev defaults only.
    // See docs/decisions/persistence-and-controllers.md for the full rationale.
    // Production connection strings are never stored here — use environment
    // variables or Azure Key Vault for real deployments.
    "DefaultConnection": "Host=localhost;Port=5432;Database=featureflags;Username=postgres;Password=postgres"
  }
}
```

### appsettings.json

Add a placeholder only — no real credentials:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

---

## 6. Domain Layer Update — Flag.Update()

**File:** `Bandera.Domain/Entities/Flag.cs`

Add one new method to the `Flag` entity. Do not modify any existing methods.

```csharp
/// <summary>
/// Atomically updates the enabled state and rollout strategy in a single
/// operation, setting UpdatedAt exactly once.
/// </summary>
public void Update(bool isEnabled, RolloutStrategy strategyType, string strategyConfig)
{
    IsEnabled = isEnabled;
    StrategyType = strategyType;
    StrategyConfig = strategyConfig ?? "{}";
    UpdatedAt = DateTime.UtcNow;
}
```

> **WHY this matters**
> The previous approach called `SetEnabled()` then `UpdateStrategy()` separately.
> Each method sets `UpdatedAt = DateTime.UtcNow`. In practice the timestamps are
> microseconds apart and no bug occurs — but it signals the domain model lacks an
> atomic update path. A single `Update()` method sets everything once, in one
> consistent operation. This is cleaner domain modeling.

---

## 7. Async Interface Updates

The following existing interfaces and implementations must be updated to async.
All async methods accept a `CancellationToken` parameter.

> **WHY CancellationToken matters**
> If a client disconnects mid-request, without a CancellationToken the database
> query keeps running to completion, the thread stays occupied, and the result
> is thrown away. The token is passed from ASP.NET Core through every layer down
> into EF Core. When the client disconnects, EF Core aborts the in-flight query.
> Under load this prevents thread pool exhaustion.
>
> Use `CancellationToken ct = default` as the parameter — the `default` value
> means "no cancellation requested", which keeps all existing callers working
> without changes.

### 7.1 IBanderaRepository (Domain)

**File:** `Bandera.Domain/Interfaces/IBanderaRepository.cs`

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;

namespace Bandera.Domain.Interfaces;

public interface IBanderaRepository
{
    Task<Flag?> GetByNameAsync(string name, EnvironmentType environment,
        CancellationToken ct = default);
    Task<IReadOnlyList<Flag>> GetAllAsync(EnvironmentType environment,
        CancellationToken ct = default);
    Task AddAsync(Flag flag, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 7.2 IBanderaService (Application)

**File:** `Bandera.Application/Interfaces/IBanderaService.cs`

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Interfaces;

public interface IBanderaService
{
    Task<Flag> GetFlagAsync(string name, EnvironmentType environment,
        CancellationToken ct = default);
    Task<bool> IsEnabledAsync(string flagName, FeatureEvaluationContext context,
        CancellationToken ct = default);
    Task<IReadOnlyList<Flag>> GetAllFlagsAsync(EnvironmentType environment,
        CancellationToken ct = default);
    Task<Flag> CreateFlagAsync(Flag flag, CancellationToken ct = default);
    Task UpdateFlagAsync(string name, EnvironmentType environment, bool isEnabled,
        RolloutStrategy strategyType, string strategyConfig,
        CancellationToken ct = default);
    Task ArchiveFlagAsync(string name, EnvironmentType environment,
        CancellationToken ct = default);
}
```

### 7.3 Bandera (Application)

**File:** `Bandera.Application/Services/BanderaService.cs`

Full replacement:

```csharp
using Bandera.Application.Evaluation;
using Bandera.Application.Interfaces;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Services;

public sealed class BanderaService : IBanderaService
{
    private readonly IBanderaRepository _repository;
    private readonly FeatureEvaluator _evaluator;

    public BanderaService(IBanderaRepository repository, FeatureEvaluator evaluator)
    {
        _repository = repository;
        _evaluator = evaluator;
    }

    public async Task<Flag> GetFlagAsync(string name, EnvironmentType environment,
        CancellationToken ct = default)
    {
        return await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");
    }

    public async Task<bool> IsEnabledAsync(string flagName, FeatureEvaluationContext context,
        CancellationToken ct = default)
    {
        var flag = await _repository.GetByNameAsync(flagName, context.Environment, ct);

        if (flag is null)
            throw new KeyNotFoundException($"Flag '{flagName}' not found in {context.Environment}.");

        if (!flag.IsEnabled)
            return false;

        return _evaluator.Evaluate(flag, context);
    }

    public async Task<IReadOnlyList<Flag>> GetAllFlagsAsync(EnvironmentType environment,
        CancellationToken ct = default)
    {
        return await _repository.GetAllAsync(environment, ct);
    }

    public async Task<Flag> CreateFlagAsync(Flag flag, CancellationToken ct = default)
    {
        await _repository.AddAsync(flag, ct);
        await _repository.SaveChangesAsync(ct);
        return flag;
    }

    public async Task UpdateFlagAsync(string name, EnvironmentType environment,
        bool isEnabled, RolloutStrategy strategyType, string strategyConfig,
        CancellationToken ct = default)
    {
        var flag = await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        // Single atomic update — sets UpdatedAt exactly once
        flag.Update(isEnabled, strategyType, strategyConfig);
        await _repository.SaveChangesAsync(ct);
    }

    public async Task ArchiveFlagAsync(string name, EnvironmentType environment,
        CancellationToken ct = default)
    {
        var flag = await _repository.GetByNameAsync(name, environment, ct)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        flag.Archive();
        await _repository.SaveChangesAsync(ct);
    }
}
```

---

## 8. DTOs

**Location:** `Bandera.Application/DTOs/`

Create four DTO files and one mapping helper.

### 8.1 CreateFlagRequest.cs

```csharp
using Bandera.Domain.Enums;

namespace Bandera.Application.DTOs;

public sealed record CreateFlagRequest(
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string StrategyConfig
);
```

### 8.2 UpdateFlagRequest.cs

```csharp
using Bandera.Domain.Enums;

namespace Bandera.Application.DTOs;

public sealed record UpdateFlagRequest(
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string StrategyConfig
);
```

### 8.3 FlagResponse.cs

```csharp
using Bandera.Domain.Enums;

namespace Bandera.Application.DTOs;

public sealed record FlagResponse(
    Guid Id,
    string Name,
    EnvironmentType Environment,
    bool IsEnabled,
    bool IsArchived,
    RolloutStrategy StrategyType,
    string StrategyConfig,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

### 8.4 EvaluationRequest.cs

```csharp
using Bandera.Domain.Enums;

namespace Bandera.Application.DTOs;

public sealed record EvaluationRequest(
    string FlagName,
    string UserId,
    IEnumerable<string> UserRoles,
    EnvironmentType Environment
);
```

### 8.5 FlagMappings.cs

```csharp
using Bandera.Domain.Entities;

namespace Bandera.Application.DTOs;

public static class FlagMappings
{
    public static FlagResponse ToResponse(this Flag flag) =>
        new(
            flag.Id,
            flag.Name,
            flag.Environment,
            flag.IsEnabled,
            flag.IsArchived,
            flag.StrategyType,
            flag.StrategyConfig,
            flag.CreatedAt,
            flag.UpdatedAt
        );
}
```

---

## 9. Infrastructure Layer

### 9.1 FlagConfiguration

**File:** `Bandera.Infrastructure/Persistence/FlagConfiguration.cs`

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bandera.Infrastructure.Persistence;

public sealed class FlagConfiguration : IEntityTypeConfiguration<Flag>
{
    public void Configure(EntityTypeBuilder<Flag> builder)
    {
        builder.ToTable("flags");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Environment)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(f => f.StrategyType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(f => f.StrategyConfig)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(f => f.IsEnabled)
            .IsRequired();

        builder.Property(f => f.IsArchived)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .IsRequired();

        builder.Property(f => f.ArchivedAt)
            .IsRequired(false);

        // Partial unique index — only enforces uniqueness on active (non-archived) flags.
        // Without HasFilter, archiving a flag and recreating it with the same name would
        // throw a unique constraint violation because the archived row still occupies
        // the index slot. HasFilter restricts the index to rows where IsArchived = false,
        // so archived flags are invisible to the constraint.
        // This is a PostgreSQL-specific feature.
        builder.HasIndex(f => new { f.Name, f.Environment })
            .IsUnique()
            .HasFilter("\"IsArchived\" = false");
    }
}
```

### 9.2 BanderaDbContext

**File:** `Bandera.Infrastructure/Persistence/BanderaDbContext.cs`

```csharp
using Bandera.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bandera.Infrastructure.Persistence;

public sealed class BanderaDbContext : DbContext
{
    public BanderaDbContext(DbContextOptions<BanderaDbContext> options)
        : base(options) { }

    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BanderaDbContext).Assembly);
    }
}
```

### 9.3 BanderaDbContextFactory

**File:** `Bandera.Infrastructure/Persistence/BanderaDbContextFactory.cs`

> **WHY this exists**
> `dotnet ef` needs to construct a `DbContext` at design time to generate migrations.
> Without this factory, it tries to spin up the full ASP.NET Core host and read
> the connection string from `appsettings.Development.json` — which requires
> `ASPNETCORE_ENVIRONMENT=Development` to be set. If it isn't, the migrations command
> fails with a confusing error. This factory gives the tooling a deterministic,
> environment-independent path to construct the context. The hardcoded connection
> string here is intentional — it is only ever used by `dotnet ef` tooling,
> never at runtime.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bandera.Infrastructure.Persistence;

public sealed class BanderaDbContextFactory
    : IDesignTimeDbContextFactory<BanderaDbContext>
{
    public BanderaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BanderaDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=featureflags;Username=postgres;Password=postgres")
            .Options;

        return new BanderaDbContext(options);
    }
}
```

### 9.4 BanderaRepository

**File:** `Bandera.Infrastructure/Persistence/BanderaRepository.cs`

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Bandera.Infrastructure.Persistence;

public sealed class BanderaRepository : IBanderaRepository
{
    private readonly BanderaDbContext _context;

    public BanderaRepository(BanderaDbContext context)
    {
        _context = context;
    }

    public async Task<Flag?> GetByNameAsync(string name, EnvironmentType environment,
        CancellationToken ct = default)
    {
        return await _context.Flags
            .Where(f => f.Name == name
                     && f.Environment == environment
                     && !f.IsArchived)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Flag>> GetAllAsync(EnvironmentType environment,
        CancellationToken ct = default)
    {
        return await _context.Flags
            .Where(f => f.Environment == environment && !f.IsArchived)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Flag flag, CancellationToken ct = default)
    {
        await _context.Flags.AddAsync(flag, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
```

### 9.5 Updated AddInfrastructure()

**File:** `Bandera.Infrastructure/DependencyInjection.cs`

Replace the stub:

```csharp
using Bandera.Domain.Interfaces;
using Bandera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bandera.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BanderaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IBanderaRepository, BanderaRepository>();

        return services;
    }
}
```

---

## 10. Controllers

**Location:** `Bandera.Api/Controllers/`

### 10.1 BanderasController

```csharp
using Bandera.Application.DTOs;
using Bandera.Application.Interfaces;
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Bandera.Api.Controllers;

[ApiController]
[Route("api/flags")]
public sealed class BanderasController : ControllerBase
{
    private readonly IBanderaService _service;

    public BanderasController(IBanderaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] EnvironmentType environment,
        CancellationToken ct)
    {
        var flags = await _service.GetAllFlagsAsync(environment, ct);
        return Ok(flags.Select(f => f.ToResponse()));
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetByName(
        string name,
        [FromQuery] EnvironmentType environment,
        CancellationToken ct)
    {
        try
        {
            var flag = await _service.GetFlagAsync(name, environment, ct);
            return Ok(flag.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateFlagRequest request,
        CancellationToken ct)
    {
        var flag = new Flag(
            request.Name,
            request.Environment,
            request.IsEnabled,
            request.StrategyType,
            request.StrategyConfig);

        var created = await _service.CreateFlagAsync(flag, ct);
        return CreatedAtAction(
            nameof(GetByName),
            new { name = created.Name, environment = created.Environment },
            created.ToResponse());
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(
        string name,
        [FromQuery] EnvironmentType environment,
        [FromBody] UpdateFlagRequest request,
        CancellationToken ct)
    {
        try
        {
            await _service.UpdateFlagAsync(
                name,
                environment,
                request.IsEnabled,
                request.StrategyType,
                request.StrategyConfig,
                ct);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Archive(
        string name,
        [FromQuery] EnvironmentType environment,
        CancellationToken ct)
    {
        try
        {
            await _service.ArchiveFlagAsync(name, environment, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
```

### 10.2 EvaluationController

> **DESIGN DECISION — 404 vs 200 for unknown flags**
> This endpoint returns 404 when the requested flag does not exist, and
> `{ "isEnabled": false }` when the flag exists but is disabled. This distinction
> is intentional and important for debuggability. A caller who typos a flag name
> gets a 404 immediately — not a silent `false` that looks identical to a disabled
> flag. The fail-closed philosophy still applies to evaluation logic (bad strategy
> config = false), but a missing flag is a configuration error the caller should
> know about.

```csharp
using Bandera.Application.DTOs;
using Bandera.Application.Interfaces;
using Bandera.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Bandera.Api.Controllers;

[ApiController]
[Route("api/evaluate")]
public sealed class EvaluationController : ControllerBase
{
    private readonly IBanderaService _service;

    public EvaluationController(IBanderaService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Evaluate(
        [FromBody] EvaluationRequest request,
        CancellationToken ct)
    {
        try
        {
            var context = new FeatureEvaluationContext(
                request.UserId,
                request.UserRoles,
                request.Environment);

            var isEnabled = await _service.IsEnabledAsync(request.FlagName, context, ct);
            return Ok(new { isEnabled });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { error = e.Message });
        }
    }
}
```

---

## 11. Swagger / OpenAPI Wiring

**File:** `Bandera.Api/Program.cs`

Replace the entire file:

```csharp
using Bandera.Application;
using Bandera.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings in JSON responses ("Production" not 3).
        // Matches how EF Core stores enums in the database — consistent
        // string representation throughout the stack.
        options.JsonSerializerOptions.Converters
            .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Redirect root to OpenAPI docs for convenience during development
    app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## 12. Migrations

After the build passes, run migrations to create the database schema.

```bash
# Add the initial migration
dotnet ef migrations add InitialCreate \
  --project Bandera.Infrastructure \
  --startup-project Bandera.Api

# Apply the migration to the database
dotnet ef database update \
  --project Bandera.Infrastructure \
  --startup-project Bandera.Api
```

> **PREREQUISITE:** Docker Compose must be running before applying the migration.
> Run `docker compose up -d` first.

> **NOTE:** The `IDesignTimeDbContextFactory` in Infrastructure means migrations
> will succeed regardless of whether `ASPNETCORE_ENVIRONMENT` is set. This is
> by design — the factory provides a deterministic path for design-time tooling.

> **IMPORTANT:** If `dotnet ef` is not found, install the tool globally:
> `dotnet tool install --global dotnet-ef`
> Then restore: `dotnet tool restore`

---

## 13. Folder Structure After Implementation

```
Bandera.Application/
  DTOs/
    CreateFlagRequest.cs      ← NEW
    UpdateFlagRequest.cs      ← NEW
    FlagResponse.cs           ← NEW
    EvaluationRequest.cs      ← NEW
    FlagMappings.cs           ← NEW
  Interfaces/
    IBanderaService.cs    ← UPDATED (async + CancellationToken)
  Services/
    BanderaService.cs     ← UPDATED (async + CancellationToken + throws on missing flag)

Bandera.Domain/
  Entities/
    Flag.cs                   ← UPDATED (new Update() method)
  Interfaces/
    IBanderaRepository.cs ← UPDATED (async + CancellationToken)

Bandera.Infrastructure/
  Persistence/
    BanderaDbContext.cs         ← NEW
    BanderaDbContextFactory.cs  ← NEW
    FlagConfiguration.cs            ← NEW
    BanderaRepository.cs        ← NEW
  DependencyInjection.cs            ← UPDATED (real wiring)

Bandera.Api/
  Controllers/
    BanderasController.cs ← NEW
    EvaluationController.cs   ← NEW
  Program.cs                  ← UPDATED
  appsettings.json            ← UPDATED (placeholder connection string)
  appsettings.Development.json ← UPDATED (local dev connection string, committed intentionally)

docker-compose.yml            ← NEW (repo root)
```

---

## 14. Verification Steps

Run these in order after implementation:

```bash
# 1. Start Postgres
docker compose up -d

# 2. Build the solution
dotnet build Bandera.sln
# Expected: 0 errors, 0 warnings

# 3. Run existing tests
dotnet test Bandera.sln
# Expected: 8/8 passing

# 4. Apply migrations
dotnet ef database update \
  --project Bandera.Infrastructure \
  --startup-project Bandera.Api

# 5. Run the API
dotnet run --project Bandera.Api

# 6. Open Swagger
# Navigate to: http://localhost:5227/openapi/v1.json
```

---

## 15. Design Decisions — Quick Reference

| Decision | Rationale |
|---|---|
| **PostgreSQL over SQL Server** | Free, open source, native `jsonb` support for strategy config, Azure-ready via Flexible Server, growing fast in .NET ecosystem |
| **`jsonb` column for StrategyConfig** | Enables future queries inside the JSON (e.g. "all flags with percentage > 50"). No cost to set up now, significant value later |
| **Enums stored as strings** | Human-readable database, safe against enum reordering, easier debugging. One-line EF Core value converter |
| **Partial unique index on Name + Environment** | Scoped to non-archived flags only. Allows archiving and recreating a flag with the same name. Without `HasFilter`, the archived row would cause a constraint violation on recreation |
| **CancellationToken throughout async stack** | Client disconnects abort in-flight database queries. Prevents thread pool exhaustion under load |
| **Flag.Update() atomic method** | Sets `UpdatedAt` exactly once. Calling `SetEnabled` then `UpdateStrategy` separately sets it twice — microseconds apart, no bug today, but a domain modeling smell |
| **Async repository and service** | Database calls are I/O-bound. Async releases the thread to handle other requests while waiting for Postgres. Expected pattern in all modern .NET APIs |
| **Fluent API over data annotations** | Keeps domain entities clean. All database concerns live in Infrastructure where they belong |
| **DTOs in Application layer** | Reusable across multiple entry points (API, CLI, background workers). Controllers map domain entities to DTOs — never expose entities directly |
| **POST for evaluation endpoint** | User context contains sensitive data that should not appear in query strings or server logs. POST body is more secure, flexible, and extensible |
| **404 for unknown flags in evaluation** | Distinguishes "flag is off" from "flag doesn't exist." Silent 200 false for a typo is a debugging nightmare. Missing flag is a configuration error the caller should know about |
| **Soft delete via Archive()** | Flags are configuration history. Hard deleting a flag loses the record of what was deployed. Archive preserves the record while hiding it from active queries |
| **IDesignTimeDbContextFactory** | Gives `dotnet ef` a deterministic path to construct the DbContext for migrations — independent of environment variables. Without it, migrations fail with cryptic errors when `ASPNETCORE_ENVIRONMENT` is not set |
| **PrivateAssets on EF Core Design package** | Build-time tooling only. Must not flow as a transitive dependency to consumers of Infrastructure |
| **appsettings.Development.json committed** | Non-sensitive local Docker defaults. Any developer who clones the repo gets a working dev setup immediately. Production credentials never go in any appsettings file |
| **docker-compose.yml at repo root** | Any developer who clones the repo can run `docker compose up -d` and have a working database in seconds. No manual Postgres installation required |

---

## 16. DO NOT

- Do not expose domain entities directly from controllers — always map to DTOs
- Do not return `IQueryable` from the repository — return concrete types only
- Do not add authentication or authorization — Phase 3 concern
- Do not add FluentValidation yet — Phase 1 concern (KI-003)
- Do not modify `FeatureEvaluator` or any strategy — evaluation engine is complete
- Do not modify `FeatureEvaluationContext.cs`
- Do not commit real production credentials anywhere in the codebase
- Do not remove `PrivateAssets` from the EF Core Design package reference

---

## 17. Instructions for Claude Code

Read the following before writing any code:

- `CLAUDE.md`
- `docs/roadmap.md`
- `docs/architecture.md`
- `docs/current-state.md`
- This file

Then implement in this order:

1. Add NuGet packages to Infrastructure and Api projects (with correct `PrivateAssets`)
2. Create `docker-compose.yml` at repo root
3. Update `appsettings.json` and `appsettings.Development.json`
4. Add `Flag.Update()` method to `Flag.cs` in Domain
5. Update `IBanderaRepository` — async signatures + `CancellationToken` + two new methods
6. Update `IBanderaService` — async signatures + `CancellationToken` + new methods
7. Update `Bandera` — async implementation + `CancellationToken` + throws on missing flag
8. Create DTOs in `Application/DTOs/`
9. Create `FlagConfiguration` in Infrastructure — partial unique index with `HasFilter`
10. Create `BanderaDbContext` in Infrastructure
11. Create `BanderaDbContextFactory` in Infrastructure
12. Create `BanderaRepository` in Infrastructure — thread `CancellationToken` into all EF Core calls
13. Update `AddInfrastructure()` with real wiring
14. Create `BanderasController` in Api — `CancellationToken` on all actions
15. Create `EvaluationController` in Api — returns 404 for unknown flags
16. Update `Program.cs`
17. Run `dotnet build Bandera.sln` — confirm 0 errors
18. Run `dotnet test Bandera.sln` — confirm 8/8 passing
19. Start Docker, run migrations, start API, verify Swagger loads

---

*Bandera | feature/persistence-and-controllers | Phase 0 Completion | v2*
