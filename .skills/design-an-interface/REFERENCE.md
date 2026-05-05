# Design an Interface — .NET Reference

## Endpoint style decision

| Use minimal API when | Use controllers when |
|---|---|
| New greenfield project | Existing controller-based codebase |
| Simple CRUD or thin proxy | Complex action filters needed |
| You want less ceremony | Team prefers explicit structure |
| .NET 7+ | Any .NET version |

---

## Option templates

### Option A — Minimal API + TypedResults

Clean, modern, explicit return types. Best for new projects.

```csharp
app.MapPost("/api/v1/flags", async (
    CreateFlagRequest request,
    IValidator<CreateFlagRequest> validator,
    IMediator mediator) =>
{
    var validation = await validator.ValidateAsync(request);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var result = await mediator.Send(new CreateFlagCommand(request.Name, request.Environment));

    return result.IsSuccess
        ? TypedResults.Created($"/api/v1/flags/{result.Value.Id}", result.Value)
        : Results.Problem(result.Error, statusCode: 409);
})
.WithName("CreateFlag")
.WithOpenApi();
```

**Tradeoff:** Less ceremony, great for small surfaces. Harder to apply cross-cutting concerns (auth, logging) without endpoint filters.

---

### Option B — Controller + MediatR

Familiar, consistent with most enterprise .NET codebases. Easy to add filters.

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/flags")]
[ApiVersion("1.0")]
public class FlagsController : ControllerBase
{
    private readonly IMediator _mediator;
    public FlagsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(FlagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateFlagRequest request)
    {
        var result = await _mediator.Send(new CreateFlagCommand(request.Name, request.Environment));

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : Problem(result.Error, statusCode: 409);
    }
}
```

**Tradeoff:** More boilerplate, but `[ProducesResponseType]` gives you free OpenAPI docs and the class is easy to filter at the controller level.

---

### Option C — Minimal API + endpoint filter for validation

Separates validation from handler logic entirely. Keeps handlers pure.

```csharp
app.MapPost("/api/v1/flags", async (CreateFlagRequest request, IMediator mediator) =>
{
    var result = await mediator.Send(new CreateFlagCommand(request.Name, request.Environment));
    return TypedResults.Created($"/api/v1/flags/{result.Id}", result);
})
.AddEndpointFilter<ValidationFilter<CreateFlagRequest>>();

// Reusable filter — register once, apply everywhere
public class ValidationFilter<T> : IEndpointFilter
{
    private readonly IValidator<T> _validator;
    public ValidationFilter(IValidator<T> validator) => _validator = validator;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var model = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (model is not null)
        {
            var result = await _validator.ValidateAsync(model);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary());
        }
        return await next(ctx);
    }
}
```

**Tradeoff:** Cleanest handler code, most reusable validation. Higher upfront infrastructure cost.

---

## ProblemDetails — standard error shape

Always use `ProblemDetails` (RFC 7807). Never return plain strings for errors.

```csharp
// Register in Program.cs
builder.Services.AddProblemDetails();

// In a handler
return Results.Problem(
    title: "Flag already exists",
    detail: $"A flag named '{request.Name}' already exists in '{request.Environment}'.",
    statusCode: 409,
    type: "https://your-api.com/errors/duplicate-flag"
);
```

Response shape:
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

## Versioning

```csharp
// NuGet: Asp.Versioning.Mvc or Asp.Versioning.Http (minimal APIs)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true; // adds api-supported-versions header
});
```

| Strategy | URL example | When to use |
|---|---|---|
| URL segment | `/api/v1/flags` | Public APIs, most common |
| Query string | `/api/flags?api-version=1.0` | When URL cleanliness matters less |
| Header | `api-version: 1.0` | Internal APIs, avoids URL pollution |

---

## Validation placement

| Where | How | Best for |
|---|---|---|
| Endpoint / filter | FluentValidation + `IValidator<T>` | Input shape, required fields, formats |
| Domain layer | Guard clauses in entity/aggregate | Business rules, invariants |
| Both | Endpoint validates shape, domain validates rules | Most .NET APIs — belt and suspenders |

Never put business rule validation in the controller/endpoint. A `FeatureFlag` that enforces "name must be unique within an environment" belongs in the domain, not in a request validator.

---

## Request/response type conventions

```csharp
// Request: named after the action
public record CreateFlagRequest(string Name, string Environment, string? Description);

// Response: always a DTO, never expose your domain entity directly
public record FlagDto(Guid Id, string Name, string Environment, bool IsEnabled, DateTimeOffset CreatedAt);
```

Map entities → DTOs in the application layer. Keep domain types out of the HTTP response.

---

## Suggested test cases for any new endpoint

Always propose these two at minimum:

```csharp
// Happy path
[Fact]
public async Task CreateFlag_WithValidRequest_Returns201WithLocation()

// Validation failure
[Fact]
public async Task CreateFlag_WithMissingName_Returns400WithProblemDetails()
```

Add domain conflict case if relevant:
```csharp
[Fact]
public async Task CreateFlag_WhenNameAlreadyExists_Returns409WithProblemDetails()
```
# Design an Interface — .NET Reference

## Endpoint style decision

| Use minimal API when | Use controllers when |
|---|---|
| New greenfield project | Existing controller-based codebase |
| Simple CRUD or thin proxy | Complex action filters needed |
| You want less ceremony | Team prefers explicit structure |
| .NET 7+ | Any .NET version |

---

## Option templates

### Option A — Minimal API + TypedResults

Clean, modern, explicit return types. Best for new projects.

```csharp
app.MapPost("/api/v1/flags", async (
    CreateFlagRequest request,
    IValidator<CreateFlagRequest> validator,
    IFlagService flagService) =>
{
    var validation = await validator.ValidateAsync(request);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var result = await flagService.CreateAsync(request.Name, request.Environment);

    return result.IsSuccess
        ? TypedResults.Created($"/api/v1/flags/{result.Value.Id}", result.Value.ToDto())
        : Results.Problem(result.Error, statusCode: 409);
})
.WithName("CreateFlag")
.WithOpenApi();
```

**Tradeoff:** Less ceremony, great for small surfaces. Harder to apply cross-cutting concerns (auth, logging) without endpoint filters.

---

### Option B — Controller + direct service injection

Familiar, consistent with most enterprise .NET codebases. Easy to add filters and attributes.

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/flags")]
[ApiVersion("1.0")]
public class FlagsController : ControllerBase
{
    private readonly IFlagService _flagService;
    public FlagsController(IFlagService flagService) => _flagService = flagService;

    [HttpPost]
    [ProducesResponseType(typeof(FlagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateFlagRequest request)
    {
        var result = await _flagService.CreateAsync(request.Name, request.Environment);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value.ToDto())
            : Problem(result.Error, statusCode: 409);
    }
}
```

**Tradeoff:** More boilerplate, but `[ProducesResponseType]` gives you free OpenAPI docs and filters apply cleanly at the controller level.

---

### Option C — Minimal API + endpoint filter for validation

Separates validation from handler logic entirely. Keeps handlers focused on the happy path.

```csharp
app.MapPost("/api/v1/flags", async (CreateFlagRequest request, IFlagService flagService) =>
{
    var result = await flagService.CreateAsync(request.Name, request.Environment);
    return TypedResults.Created($"/api/v1/flags/{result.Id}", result.ToDto());
})
.AddEndpointFilter<ValidationFilter<CreateFlagRequest>>();

// Reusable filter — register once, apply to any endpoint
public class ValidationFilter<T> : IEndpointFilter
{
    private readonly IValidator<T> _validator;
    public ValidationFilter(IValidator<T> validator) => _validator = validator;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var model = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (model is not null)
        {
            var result = await _validator.ValidateAsync(model);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary());
        }
        return await next(ctx);
    }
}
```

**Tradeoff:** Cleanest handler code, most reusable validation. Higher upfront infrastructure cost.

---

## ProblemDetails — standard error shape

Always use `ProblemDetails` (RFC 7807). Never return plain strings for errors.

```csharp
// Register in Program.cs
builder.Services.AddProblemDetails();

// In a handler
return Results.Problem(
    title: "Flag already exists",
    detail: $"A flag named '{request.Name}' already exists in '{request.Environment}'.",
    statusCode: 409,
    type: "https://your-api.com/errors/duplicate-flag"
);
```

Response shape:
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

## Versioning

```csharp
// NuGet: Asp.Versioning.Mvc or Asp.Versioning.Http (minimal APIs)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});
```

| Strategy | URL example | When to use |
|---|---|---|
| URL segment | `/api/v1/flags` | Public APIs, most common |
| Query string | `/api/flags?api-version=1.0` | When URL cleanliness matters less |
| Header | `api-version: 1.0` | Internal APIs, avoids URL pollution |

---

## Validation placement

| Where | How | Best for |
|---|---|---|
| Endpoint / filter | FluentValidation + `IValidator<T>` | Input shape, required fields, formats |
| Domain layer | Guard clauses in entity/aggregate | Business rules, invariants |
| Both | Endpoint validates shape, domain validates rules | Most .NET APIs — belt and suspenders |

Never put business rule validation in the controller/endpoint. A `FeatureFlag` that enforces "name must be unique within an environment" belongs in the domain, not in a request validator.

---

## Request/response type conventions

```csharp
// Request: named after the action
public record CreateFlagRequest(string Name, string Environment, string? Description);

// Response: always a DTO, never expose your domain entity directly
public record FlagDto(Guid Id, string Name, string Environment, bool IsEnabled, DateTimeOffset CreatedAt);
```

Map entities → DTOs in the application/service layer. Keep domain types out of the HTTP response.

---

## Suggested test cases for any new endpoint

Always propose these two at minimum:

```csharp
// Happy path
[Fact]
public async Task CreateFlag_WithValidRequest_Returns201WithLocation()

// Validation failure
[Fact]
public async Task CreateFlag_WithMissingName_Returns400WithProblemDetails()
```

Add domain conflict case if relevant:
```csharp
[Fact]
public async Task CreateFlag_WhenNameAlreadyExists_Returns409WithProblemDetails()
```
