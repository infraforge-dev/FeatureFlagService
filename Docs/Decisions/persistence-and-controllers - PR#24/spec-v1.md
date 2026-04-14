# Persistence & Controllers — Implementation Spec

**Branch:** `feature/persistence-and-controllers`
**Phase:** 0 — Foundation (Completion)
**Status:** Ready for implementation

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
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.5" />
```

> **NOTE on versions:** Use the latest stable versions available at time of implementation.
> Npgsql `9.x` targets EF Core `9.x`. If EF Core `10.x` has a compatible Npgsql release,
> prefer that. Run `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL` and let NuGet
> resolve the best version for `net10.0`.

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

Add the connection string. Replace the entire file contents:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
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

## 6. Async Interface Updates

The following existing interfaces and implementations must be updated to async.
This is a breaking change — update all callers in the same pass.

### 6.1 IBanderaRepository (Domain)

**File:** `Bandera.Domain/Interfaces/IBanderaRepository.cs`

Replace the current synchronous signatures:

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;

namespace Bandera.Domain.Interfaces;

public interface IBanderaRepository
{
    Task<Flag?> GetByNameAsync(string name, EnvironmentType environment);
    Task<IReadOnlyList<Flag>> GetAllAsync(EnvironmentType environment);
    Task AddAsync(Flag flag);
    Task SaveChangesAsync();
}
```

> **NOTE — Two new methods added**
> `AddAsync` and `SaveChangesAsync` are required for the CRUD endpoints.
> The repository owns persistence — controllers must never touch DbContext directly.

### 6.2 IBanderaService (Application)

**File:** `Bandera.Application/Interfaces/IBanderaService.cs`

```csharp
using Bandera.Domain.Entities;
using Bandera.Domain.Enums;
using Bandera.Domain.ValueObjects;

namespace Bandera.Application.Interfaces;

public interface IBanderaService
{
    Task<Flag> GetFlagAsync(string name, EnvironmentType environment);
    Task<bool> IsEnabledAsync(string flagName, FeatureEvaluationContext context);
    Task<IReadOnlyList<Flag>> GetAllFlagsAsync(EnvironmentType environment);
    Task<Flag> CreateFlagAsync(Flag flag);
    Task UpdateFlagAsync(string name, EnvironmentType environment, bool isEnabled,
        RolloutStrategy strategyType, string strategyConfig);
    Task ArchiveFlagAsync(string name, EnvironmentType environment);
}
```

### 6.3 Bandera (Application)

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

    public async Task<Flag> GetFlagAsync(string name, EnvironmentType environment)
    {
        return await _repository.GetByNameAsync(name, environment)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");
    }

    public async Task<bool> IsEnabledAsync(string flagName, FeatureEvaluationContext context)
    {
        var flag = await _repository.GetByNameAsync(flagName, context.Environment);

        if (flag is null || !flag.IsEnabled)
            return false;

        return _evaluator.Evaluate(flag, context);
    }

    public async Task<IReadOnlyList<Flag>> GetAllFlagsAsync(EnvironmentType environment)
    {
        return await _repository.GetAllAsync(environment);
    }

    public async Task<Flag> CreateFlagAsync(Flag flag)
    {
        await _repository.AddAsync(flag);
        await _repository.SaveChangesAsync();
        return flag;
    }

    public async Task UpdateFlagAsync(string name, EnvironmentType environment,
        bool isEnabled, RolloutStrategy strategyType, string strategyConfig)
    {
        var flag = await _repository.GetByNameAsync(name, environment)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        flag.SetEnabled(isEnabled);
        flag.UpdateStrategy(strategyType, strategyConfig);
        await _repository.SaveChangesAsync();
    }

    public async Task ArchiveFlagAsync(string name, EnvironmentType environment)
    {
        var flag = await _repository.GetByNameAsync(name, environment)
            ?? throw new KeyNotFoundException($"Flag '{name}' not found in {environment}.");

        flag.Archive();
        await _repository.SaveChangesAsync();
    }
}
```

---

## 7. DTOs

**Location:** `Bandera.Application/DTOs/`

Create four files.

### 7.1 CreateFlagRequest.cs

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

### 7.2 UpdateFlagRequest.cs

```csharp
using Bandera.Domain.Enums;

namespace Bandera.Application.DTOs;

public sealed record UpdateFlagRequest(
    bool IsEnabled,
    RolloutStrategy StrategyType,
    string StrategyConfig
);
```

### 7.3 FlagResponse.cs

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

### 7.4 EvaluationRequest.cs

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

### 7.5 Mapping Helper

Add a static mapping extension in `Bandera.Application/DTOs/FlagMappings.cs`:

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

## 8. Infrastructure Layer

### 8.1 FlagConfiguration

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

        // A flag name must be unique within an environment
        builder.HasIndex(f => new { f.Name, f.Environment })
            .IsUnique();
    }
}
```

### 8.2 BanderaDbContext

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

### 8.3 BanderaRepository

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

    public async Task<Flag?> GetByNameAsync(string name, EnvironmentType environment)
    {
        return await _context.Flags
            .Where(f => f.Name == name
                     && f.Environment == environment
                     && !f.IsArchived)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Flag>> GetAllAsync(EnvironmentType environment)
    {
        return await _context.Flags
            .Where(f => f.Environment == environment && !f.IsArchived)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task AddAsync(Flag flag)
    {
        await _context.Flags.AddAsync(flag);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
```

### 8.4 Updated AddInfrastructure()

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

## 9. Controllers

**Location:** `Bandera.Api/Controllers/`

### 9.1 BanderasController

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
    public async Task<IActionResult> GetAll([FromQuery] EnvironmentType environment)
    {
        var flags = await _service.GetAllFlagsAsync(environment);
        return Ok(flags.Select(f => f.ToResponse()));
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetByName(
        string name,
        [FromQuery] EnvironmentType environment)
    {
        try
        {
            var flag = await _service.GetFlagAsync(name, environment);
            return Ok(flag.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFlagRequest request)
    {
        var flag = new Flag(
            request.Name,
            request.Environment,
            request.IsEnabled,
            request.StrategyType,
            request.StrategyConfig);

        var created = await _service.CreateFlagAsync(flag);
        return CreatedAtAction(
            nameof(GetByName),
            new { name = created.Name, environment = created.Environment },
            created.ToResponse());
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(
        string name,
        [FromQuery] EnvironmentType environment,
        [FromBody] UpdateFlagRequest request)
    {
        try
        {
            await _service.UpdateFlagAsync(
                name,
                environment,
                request.IsEnabled,
                request.StrategyType,
                request.StrategyConfig);

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
        [FromQuery] EnvironmentType environment)
    {
        try
        {
            await _service.ArchiveFlagAsync(name, environment);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
```

### 9.2 EvaluationController

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
    public async Task<IActionResult> Evaluate([FromBody] EvaluationRequest request)
    {
        var context = new FeatureEvaluationContext(
            request.UserId,
            request.UserRoles,
            request.Environment);

        var isEnabled = await _service.IsEnabledAsync(request.FlagName, context);
        return Ok(new { isEnabled });
    }
}
```

---

## 10. Swagger / OpenAPI Wiring

**File:** `Bandera.Api/Program.cs`

Replace the entire file:

```csharp
using Bandera.Application;
using Bandera.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings in JSON responses
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

    // Redirect root to OpenAPI docs for convenience
    app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

> **NOTE — JsonStringEnumConverter**
> This tells the JSON serializer to write enums as strings (`"Production"`) instead of
> integers (`3`) in API responses. This must match how EF Core stores them. Consistent
> string representation across the API and the database is a deliberate choice.

---

## 11. Migrations

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

> **IMPORTANT:** If `dotnet ef` is not found, install the tool globally:
> `dotnet tool install --global dotnet-ef`
> Then restore: `dotnet tool restore`

---

## 12. Folder Structure After Implementation

```
Bandera.Application/
  DTOs/
    CreateFlagRequest.cs      ← NEW
    UpdateFlagRequest.cs      ← NEW
    FlagResponse.cs           ← NEW
    EvaluationRequest.cs      ← NEW
    FlagMappings.cs           ← NEW
  Interfaces/
    IBanderaService.cs    ← UPDATED (async)
  Services/
    BanderaService.cs     ← UPDATED (async, new methods)

Bandera.Domain/
  Interfaces/
    IBanderaRepository.cs ← UPDATED (async, AddAsync + SaveChangesAsync)

Bandera.Infrastructure/
  Persistence/
    BanderaDbContext.cs   ← NEW
    FlagConfiguration.cs      ← NEW
    BanderaRepository.cs  ← NEW
  DependencyInjection.cs      ← UPDATED (real wiring)

Bandera.Api/
  Controllers/
    BanderasController.cs ← NEW
    EvaluationController.cs   ← NEW
  Program.cs                  ← UPDATED
  appsettings.json            ← UPDATED (placeholder connection string)
  appsettings.Development.json ← UPDATED (real connection string)

docker-compose.yml            ← NEW (repo root)
```

---

## 13. Verification Steps

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

## 14. Design Decisions — Quick Reference

| Decision | Rationale |
|---|---|
| **PostgreSQL over SQL Server** | Free, open source, native `jsonb` support for strategy config, Azure-ready via Flexible Server, growing fast in .NET ecosystem |
| **`jsonb` column for StrategyConfig** | Enables future queries inside the JSON (e.g. "all flags with percentage > 50"). No cost to set up now, significant value later |
| **Enums stored as strings** | Human-readable database, safe against enum reordering, easier debugging. One-line EF Core value converter |
| **Composite unique index on Name + Environment** | Enforced at the database level. A flag name is unique per environment — you can have `dark-mode` in Dev and Prod but not two in Prod |
| **Async repository and service** | Database calls are I/O-bound. Async releases the thread to handle other requests while waiting for Postgres. Expected pattern in all modern .NET APIs |
| **Fluent API over data annotations** | Keeps domain entities clean. All database concerns live in Infrastructure where they belong |
| **DTOs in Application layer** | Reusable across multiple entry points (API, CLI, background workers). Controllers map domain entities to DTOs — never expose entities directly |
| **POST for evaluation endpoint** | User context contains sensitive data that should not appear in query strings or server logs. POST body is more secure, flexible, and extensible |
| **Soft delete via Archive()** | Flags are configuration history. Hard deleting a flag loses the record of what was deployed. Archive preserves the record while hiding it from active queries |
| **docker-compose.yml at repo root** | Any developer who clones the repo can run `docker compose up -d` and have a working database in seconds. No manual Postgres installation required |

---

## 15. DO NOT

- Do not expose domain entities directly from controllers — always map to DTOs
- Do not return `IQueryable` from the repository — return concrete types only
- Do not add authentication or authorization — Phase 3 concern
- Do not add FluentValidation yet — Phase 1 concern (KI-003)
- Do not modify `FeatureEvaluator` or any strategy — evaluation engine is complete
- Do not modify `Flag.cs` or `FeatureEvaluationContext.cs`
- Do not commit real credentials — `appsettings.Development.json` uses local dev values only

---

## 16. Instructions for Claude Code

Read the following before writing any code:

- `CLAUDE.md`
- `docs/roadmap.md`
- `docs/architecture.md`
- `docs/current-state.md`
- This file

Then implement in this order:

1. Add NuGet packages to Infrastructure and Api projects
2. Create `docker-compose.yml` at repo root
3. Update `appsettings.json` and `appsettings.Development.json`
4. Update `IBanderaRepository` — async signatures + two new methods
5. Update `IBanderaService` — async signatures + new methods
6. Update `Bandera` — async implementation
7. Create DTOs in `Application/DTOs/`
8. Create `FlagConfiguration` in Infrastructure
9. Create `BanderaDbContext` in Infrastructure
10. Create `BanderaRepository` in Infrastructure
11. Update `AddInfrastructure()` with real wiring
12. Create `BanderasController` in Api
13. Create `EvaluationController` in Api
14. Update `Program.cs`
15. Run `dotnet build Bandera.sln` — confirm 0 errors
16. Run `dotnet test Bandera.sln` — confirm 8/8 passing
17. Start Docker, run migrations, start API, verify Swagger loads

---

*Bandera | feature/persistence-and-controllers | Phase 0 Completion*
