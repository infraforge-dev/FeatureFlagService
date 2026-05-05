# dotnet-api-design — Reference

Five must-haves. Each section has two lenses:

- **What to look for** — audit mode pass/fail criteria
- **Default to generate** — greenfield scaffold starting point

---

## 1. Versioning

### What to look for

**✅ Pass** — all three must be true:
- `Asp.Versioning.Http` or `Asp.Versioning.Mvc` in `.csproj`
- `AddApiVersioning(...)` called in `Program.cs`
- At least one route has a version applied (URL segment, attribute, or versioned route group)

**⚠️ Partial** — package present but not registered in DI, or registered but no routes are versioned.

**❌ Fail** — no versioning package, no version in any route.

**Anti-pattern:**
```csharp
// No versioning — this route can never evolve without a breaking change
app.MapGet("/api/flags", GetAllFlags);
```

### Default to generate

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true; // adds api-supported-versions response header
});

// URL segment (default recommendation)
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

var v1 = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(1);

v1.MapGet("/flags", GetAllFlags);
```

| Strategy | Example | When to use |
|---|---|---|
| URL segment | `/api/v1/flags` | Public APIs — explicit, easy to test |
| Query string | `/api/flags?api-version=1.0` | When URL cleanliness matters |
| Header | `api-version: 1.0` | Internal APIs |

---

## 2. ProblemDetails / global error shape

### What to look for

**✅ Pass** — all three must be true:
- `AddProblemDetails()` registered in `Program.cs`
- `UseExceptionHandler()` or equivalent global handler in the middleware pipeline
- Error responses in endpoints use `Results.Problem(...)` or `TypedResults.Problem(...)` — not raw strings or anonymous objects

**⚠️ Partial** — `AddProblemDetails()` registered but some endpoints still return raw strings, or the exception handler is missing so unhandled exceptions reach clients as 500 HTML.

**❌ Fail** — no `AddProblemDetails()`, errors are plain strings or bare status codes.

**Anti-pattern:**
```csharp
// Never return raw strings for errors
return Results.BadRequest("Name is required");

// Never forward exception messages to clients
return Results.Problem(ex.Message);
```

### Default to generate

```csharp
// Program.cs
builder.Services.AddProblemDetails();

app.UseExceptionHandler();   // catches unhandled exceptions, returns ProblemDetails
app.UseStatusCodePages();    // converts bare 404/405 into ProblemDetails

// In an endpoint handler
return Results.Problem(
    title: "Flag already exists",
    detail: $"A flag named '{request.Name}' already exists in '{request.Environment}'.",
    statusCode: StatusCodes.Status409Conflict,
    type: "https://your-api.com/errors/duplicate-flag"
);
```

RFC 7807 shape clients receive:
```json
{
  "type": "https://your-api.com/errors/duplicate-flag",
  "title": "Flag already exists",
  "status": 409,
  "detail": "A flag named 'dark-mode' already exists in 'production'.",
  "traceId": "00-abc123..."
}
```

---

## 3. Validation placement

### What to look for

**✅ Pass** — both layers are present and doing the right job:
- Input shape validation (required fields, lengths, formats) happens at the endpoint or pipeline layer
- Business rule validation (uniqueness, state transitions, domain invariants) happens in the domain layer
- Domain guards are not duplicating HTTP input checks; endpoint validators are not encoding business rules

**⚠️ Partial** — validation exists in only one layer. Common pattern: FluentValidation installed and wired, but domain objects have no guards. Or the opposite: rich domain guards, but raw unvalidated input reaches them from the endpoint.

**❌ Fail** — no validation at either layer. Raw input reaches domain objects unchecked.

**Anti-pattern:**
```csharp
// Business rule sitting in the endpoint — wrong layer
app.MapPost("/api/v1/flags", async (CreateFlagRequest request, IFlagService service) =>
{
    if (await service.ExistsAsync(request.Name))   // domain rule — move to domain
        return Results.Conflict();
    ...
});

// Input shape check sitting in the domain — wrong layer
public class FeatureFlag
{
    public FeatureFlag(string name)
    {
        if (string.IsNullOrWhiteSpace(name))        // input check — move to endpoint
            throw new ArgumentException("Name required");
    }
}
```

### Default to generate

```csharp
// Input validation at the endpoint layer
public class CreateFlagRequestValidator : AbstractValidator<CreateFlagRequest>
{
    public CreateFlagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Environment).NotEmpty();
    }
}

// Wire FluentValidation into the endpoint filter pipeline
app.MapPost("/api/v1/flags", CreateFlagHandler)
   .AddEndpointFilter<ValidationFilter<CreateFlagRequest>>();

// Business rule stays in the domain
public class FeatureFlag
{
    public static Result<FeatureFlag> Create(string name, string environment)
    {
        // Only domain invariants here — not input shape
        if (name.Contains(' '))
            return Result.Failure<FeatureFlag>("Flag names cannot contain spaces.");

        return Result.Success(new FeatureFlag(name, environment));
    }
}
```

**Rule of thumb:** If the validation error message would make sense in a UI form, it belongs at the endpoint. If it only makes sense to a developer reading your domain model, it belongs in the domain.

---

## 4. DTO conventions

### What to look for

**✅ Pass** — all of the following:
- Request types are named `*Request`, `*Command`, or `*Query` (any convention — but consistent)
- Response types are named `*Response`, `*Dto`, or `*Summary`
- No domain entity types appear in endpoint method signatures or HTTP response bodies
- Entity → DTO mapping happens in the application or service layer, not inside the endpoint handler

**⚠️ Partial** — DTOs exist for some endpoints but others return raw entities, or mapping logic lives inside the endpoint handler.

**❌ Fail** — domain entities returned directly from endpoints. No dedicated request/response types.

**Anti-pattern:**
```csharp
// Naked entity in the response — exposes your entire domain model
app.MapGet("/api/v1/flags/{id}", async (Guid id, IFlagRepository repo) =>
{
    var flag = await repo.GetByIdAsync(id);
    return Results.Ok(flag);   // FeatureFlag entity — wrong
});
```

### Default to generate

```csharp
// Request record
public record CreateFlagRequest(
    string Name,
    string Environment,
    string? Description
);

// Response DTO — only what the caller needs to know
public record FlagDto(
    Guid Id,
    string Name,
    string Environment,
    bool IsEnabled,
    DateTimeOffset CreatedAt
);

// Extension method keeps mapping out of the endpoint
public static class FlagMappingExtensions
{
    public static FlagDto ToDto(this FeatureFlag flag) =>
        new(flag.Id, flag.Name, flag.Environment, flag.IsEnabled, flag.CreatedAt);
}

// Endpoint — domain entity never crosses the HTTP boundary
app.MapGet("/api/v1/flags/{id}", async (Guid id, IFlagService service) =>
{
    var flag = await service.GetByIdAsync(id);
    return flag is null
        ? Results.Problem("Flag not found", statusCode: 404)
        : Results.Ok(flag.ToDto());
});
```

---

## 5. OpenAPI documentation

### What to look for

**✅ Pass** — all of the following:
- `Swashbuckle.AspNetCore` or `Microsoft.AspNetCore.OpenApi` (+ `Scalar`) in `.csproj`
- OpenAPI middleware registered and mapped in `Program.cs`
- Endpoints include response metadata: `.WithName()` + `.WithOpenApi()` for minimal APIs, or `[ProducesResponseType]` for controllers

**⚠️ Partial** — Swagger/OpenAPI registered but endpoints have no metadata. The generated spec exists but is empty or misleading.

**❌ Fail** — no OpenAPI package, no generated docs.

**Anti-pattern:**
```csharp
// Swagger registered but endpoint has no metadata — generated docs are useless
app.MapPost("/api/v1/flags", CreateFlag);
```

### Default to generate

```csharp
// Program.cs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoint with full metadata
app.MapPost("/api/v1/flags", CreateFlag)
   .WithName("CreateFlag")
   .WithSummary("Create a new feature flag")
   .WithDescription("Creates a flag in the specified environment. Names must be unique per environment.")
   .WithOpenApi()
   .Produces<FlagDto>(StatusCodes.Status201Created)
   .ProducesProblem(StatusCodes.Status400BadRequest)
   .ProducesProblem(StatusCodes.Status409Conflict);
```

---

## Greenfield scaffolds

### Minimal API — all five must-haves wired

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// 2. ProblemDetails
builder.Services.AddProblemDetails();

// 3. Validation (FluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// 5. OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ... register your own services here

var app = builder.Build();

// 2. Global error handling
app.UseExceptionHandler();
app.UseStatusCodePages();

// 5. Swagger UI (dev only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Versioned route group
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

var v1 = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(1);

// 4. Add endpoints here — always return DTOs, never entities
// v1.MapGet("/your-resource", ...)
//    .WithName("...")
//    .WithOpenApi()
//    .Produces<YourDto>(200);

app.Run();
```

### Controller-based — all five must-haves wired

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Versioning + 2. ProblemDetails + controllers
builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>(); // wire validation globally
    })
    .AddApiExplorer();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddProblemDetails();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// 5. OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
```

```csharp
// Controller skeleton with all five concerns present
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class FlagsController : ControllerBase
{
    private readonly IFlagService _service;
    public FlagsController(IFlagService service) => _service = service;

    // 5. Response metadata for OpenAPI
    [HttpPost]
    [ProducesResponseType(typeof(FlagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateFlagRequest request)
    {
        // 3. Input validation handled by filter — not here
        var result = await _service.CreateAsync(request.Name, request.Environment);

        // 2. ProblemDetails for errors
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value.ToDto())
            : Problem(result.Error, statusCode: StatusCodes.Status409Conflict);
    }
}
```

---

## Nice-to-haves (flag, don't drill)

The audit surfaces these if absent — one line each in the report. They do not block the score.

| Concern | Signal to look for |
|---|---|
| Authentication / authorization | `AddAuthentication()`, `[Authorize]`, or auth middleware in pipeline |
| Rate limiting | `AddRateLimiter()` in .NET 7+ or a third-party package |
| Health checks | `AddHealthChecks()` + `/health` endpoint mapped |
| Structured logging | Serilog, NLog, or `builder.Host.UseSerilog(...)` |
| CORS | `AddCors()` + `UseCors()` with an explicit policy |
