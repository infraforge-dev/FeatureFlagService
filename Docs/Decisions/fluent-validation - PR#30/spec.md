# Spec: FluentValidation on Request DTOs

**Branch:** `feature/fluent-validation-dtos`  
**Closes:** KI-003  
**Layer:** `Banderas.Application` (validators + sanitizer) + `Banderas.Api` (wiring)  
**Related ADR:** `adr-input-security-model.md`

---

## Table of Contents

- [Context](#context)
- [Packages to Add](#packages-to-add)
- [New Files to Create](#new-files-to-create)
  - [1. InputSanitizer.cs](#1-inputsanitizercs)
  - [2. CreateFlagRequestValidator.cs](#2-createflagrequestvalidatorcs)
  - [3. UpdateFlagRequestValidator.cs](#3-updateflagrequestvalidatorcs)
  - [4. EvaluationRequestValidator.cs](#4-evaluationrequestvalidatorcs)
- [Files to Modify](#files-to-modify)
- [Acceptance Criteria](#acceptance-criteria)
- [What NOT to Do](#what-not-to-do)
- [Folder Structure After This Change](#folder-structure-after-this-change)

---

## Context

The three request DTOs currently have no validation or sanitization. A caller can
send an empty `Name`, a `None` environment, a malformed `StrategyConfig` JSON string,
or a `UserId` padded with whitespace — and the request reaches the database before
anything fails.

This spec adds two things:

1. **Input sanitization** — a shared `InputSanitizer` helper that trims whitespace
   and strips control characters from string inputs before they are validated or used.

2. **Input validation** — `AbstractValidator<T>` implementations for all three request
   DTOs, wired into the ASP.NET Core pipeline via auto-validation so invalid requests
   are rejected at the HTTP boundary with a structured `400 Bad Request`.

### Important: Why a Shared Sanitizer, Not Just Validator-Level Sanitization

FluentValidation v12 removed `.Transform()`. The v12 pattern for sanitization-aware
validation is to validate structural constraints on the raw property and run the cleaned
value through `Must()` for rules where sanitization changes the outcome:

```csharp
RuleFor(x => x.Name)
    .NotEmpty().WithMessage("Flag name is required.")
    .MaximumLength(100).WithMessage("Flag name must not exceed 100 characters.")
    .Must(name => Regex.IsMatch(
        InputSanitizer.Clean(name) ?? string.Empty,
        @"^[a-zA-Z0-9\-_]+$"))
    .WithMessage("Flag name may only contain letters, numbers, hyphens, and underscores.");
```

`NotEmpty` and `MaximumLength` run on the raw value — a 101-character string with spaces
is still too long after trimming, and whitespace-only strings are treated as empty by
FluentValidation. The `Must()` lambda runs the sanitized value through the regex — the
only check where sanitization actually changes the outcome.

**Note:** `RuleFor(x => InputSanitizer.Clean(x.Name)).OverridePropertyName("Name")` hits
a type inference limitation in v12 when a static method is used in the lambda. Use the
`Must()` pattern above instead for all sanitization-aware rules.

This still does not mutate the DTO — the service layer receives the original, raw value. This matters: `" Admin "` (with spaces) would pass validation after being cleaned
to `"Admin"`, but `RoleStrategy` would receive `" Admin "` and the `HashSet` comparison
would silently fail — a legitimate user denied access.

The fix is unchanged: `InputSanitizer` is a shared static helper. Validators call it
inside `Must()` lambdas. The service layer calls it directly before using string values
in evaluation logic. Same rules, one source of truth.

---

## Packages to Add

### `Banderas.Application/Banderas.Application.csproj`
```xml
<PackageReference Include="FluentValidation" Version="12.*" />
```

> **Note:** `FluentValidation.AspNetCore` is **deprecated** as of v11 and should not
> be added. Validation is wired manually in controllers using injected `IValidator<T>`
> instances. `AddValidatorsFromAssemblyContaining` lives in the separate
> `FluentValidation.DependencyInjectionExtensions` package — do not add it. Register
> validators explicitly in `DependencyInjection.cs` instead (see Files to Modify).

---

## New Files to Create

All new files go in: `Banderas.Application/Validators/`

---

### 1. `InputSanitizer.cs`

Shared sanitization logic. Used by validators (via `Must()` lambdas) and by
`Banderas` directly. Any future input surface (CLI, seed data) must also
call this helper — do not inline equivalent logic elsewhere.

```csharp
namespace Banderas.Application.Validators;

/// <summary>
/// Shared input sanitization helper.
/// Called by validators (via .Transform()) and by the service layer before
/// string values are used in evaluation logic.
///
/// Sanitizes for the HTTP boundary only. Does not substitute for prompt
/// sanitization (Phase 1.5: IPromptSanitizer) or structured logging conventions.
/// </summary>
internal static class InputSanitizer
{
    /// <summary>
    /// Trims leading/trailing whitespace and removes ASCII control characters
    /// (codepoints below 0x20, except tab). Returns null if input is null.
    /// </summary>
    internal static string? Clean(string? value)
    {
        if (value is null) return null;

        // Strip control characters (0x00–0x1F) except tab (0x09)
        var cleaned = new string(value
            .Where(c => c == '\t' || c >= 0x20)
            .ToArray());

        return cleaned.Trim();
    }

    /// <summary>
    /// Applies Clean() to each element. Removes entries that are null or
    /// empty after cleaning.
    /// </summary>
    internal static IEnumerable<string> CleanCollection(IEnumerable<string>? values)
    {
        if (values is null) return [];

        return values
            .Select(Clean)
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>();
    }
}
```

---

### 2. `CreateFlagRequestValidator.cs`

```csharp
using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using FluentValidation;

namespace Banderas.Application.Validators;

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateFlagRequest>
{
    public CreateFlagRequestValidator()
    {
        // Sanitize inside the RuleFor lambda (v12: .Transform() removed).
        // OverridePropertyName required — property name cannot be inferred from a lambda.
        RuleFor(x => InputSanitizer.Clean(x.Name))
            .OverridePropertyName("Name")
            .NotEmpty().WithMessage("Flag name is required.")
            .MaximumLength(100).WithMessage("Flag name must not exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage("Flag name may only contain letters, numbers, hyphens, and underscores.");

        RuleFor(x => x.Environment)
            .NotEqual(EnvironmentType.None)
            .WithMessage("A valid environment must be specified (Development, Staging, or Production).");

        RuleFor(x => x.StrategyType)
            .IsInEnum()
            .WithMessage("StrategyType must be a valid value (None, Percentage, or RoleBased).");

        // StrategyConfig: enforce size limit first, then cross-field structure rules.
        // Note: StrategyConfig is NOT sanitized — it is JSON and must be stored verbatim.
        // Only its length and internal structure are validated.
        RuleFor(x => x.StrategyConfig)
            .MaximumLength(2000)
            .WithMessage("StrategyConfig must not exceed 2000 characters.");

        // When strategy is None, StrategyConfig must be null or empty
        RuleFor(x => x.StrategyConfig)
            .Empty()
            .When(x => x.StrategyType == RolloutStrategy.None)
            .WithMessage("StrategyConfig must be empty when StrategyType is None.");

        // When strategy is Percentage, config must contain a 'percentage' field (1–100)
        RuleFor(x => x.StrategyConfig)
            .NotEmpty().WithMessage("StrategyConfig is required for Percentage strategy.")
            .Must(BeValidPercentageConfig)
            .WithMessage(
                "StrategyConfig for Percentage strategy must be valid JSON with " +
                "a 'percentage' field between 1 and 100.")
            .When(x => x.StrategyType == RolloutStrategy.Percentage);

        // When strategy is RoleBased, config must contain a non-empty 'roles' array
        RuleFor(x => x.StrategyConfig)
            .NotEmpty().WithMessage("StrategyConfig is required for RoleBased strategy.")
            .Must(BeValidRoleConfig)
            .WithMessage(
                "StrategyConfig for RoleBased strategy must be valid JSON with " +
                "a non-empty 'roles' array.")
            .When(x => x.StrategyType == RolloutStrategy.RoleBased);
    }

    private static bool BeValidPercentageConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config)) return false;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("percentage", out var prop)) return false;
            if (!prop.TryGetInt32(out var percentage)) return false;
            return percentage >= 1 && percentage <= 100;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }

    private static bool BeValidRoleConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config)) return false;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("roles", out var prop)) return false;
            if (prop.ValueKind != System.Text.Json.JsonValueKind.Array) return false;
            return prop.GetArrayLength() > 0;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }
}
```

---

### 3. `UpdateFlagRequestValidator.cs`

```csharp
using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using FluentValidation;

namespace Banderas.Application.Validators;

public sealed class UpdateFlagRequestValidator : AbstractValidator<UpdateFlagRequest>
{
    public UpdateFlagRequestValidator()
    {
        RuleFor(x => x.StrategyType)
            .IsInEnum()
            .WithMessage("StrategyType must be a valid value (None, Percentage, or RoleBased).");

        // StrategyConfig: size limit first, then cross-field rules
        RuleFor(x => x.StrategyConfig)
            .MaximumLength(2000)
            .WithMessage("StrategyConfig must not exceed 2000 characters.");

        // When strategy is None, StrategyConfig must be null or empty
        RuleFor(x => x.StrategyConfig)
            .Empty()
            .When(x => x.StrategyType == RolloutStrategy.None)
            .WithMessage("StrategyConfig must be empty when StrategyType is None.");

        // When strategy is Percentage, config must contain a 'percentage' field (1–100)
        RuleFor(x => x.StrategyConfig)
            .NotEmpty().WithMessage("StrategyConfig is required for Percentage strategy.")
            .Must(BeValidPercentageConfig)
            .WithMessage(
                "StrategyConfig for Percentage strategy must be valid JSON with " +
                "a 'percentage' field between 1 and 100.")
            .When(x => x.StrategyType == RolloutStrategy.Percentage);

        // When strategy is RoleBased, config must contain a non-empty 'roles' array
        RuleFor(x => x.StrategyConfig)
            .NotEmpty().WithMessage("StrategyConfig is required for RoleBased strategy.")
            .Must(BeValidRoleConfig)
            .WithMessage(
                "StrategyConfig for RoleBased strategy must be valid JSON with " +
                "a non-empty 'roles' array.")
            .When(x => x.StrategyType == RolloutStrategy.RoleBased);
    }

    private static bool BeValidPercentageConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config)) return false;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("percentage", out var prop)) return false;
            if (!prop.TryGetInt32(out var percentage)) return false;
            return percentage >= 1 && percentage <= 100;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }

    private static bool BeValidRoleConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config)) return false;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("roles", out var prop)) return false;
            if (prop.ValueKind != System.Text.Json.JsonValueKind.Array) return false;
            return prop.GetArrayLength() > 0;
        }
        catch (System.Text.Json.JsonException) { return false; }
    }
}
```

---

### 4. `EvaluationRequestValidator.cs`

```csharp
using Banderas.Application.DTOs;
using Banderas.Domain.Enums;
using FluentValidation;

namespace Banderas.Application.Validators;

public sealed class EvaluationRequestValidator : AbstractValidator<EvaluationRequest>
{
    public EvaluationRequestValidator()
    {
        // Sanitize inside the RuleFor lambda (v12: .Transform() removed).
        RuleFor(x => InputSanitizer.Clean(x.FlagName))
            .OverridePropertyName("FlagName")
            .NotEmpty().WithMessage("FlagName is required.")
            .MaximumLength(100).WithMessage("FlagName must not exceed 100 characters.");

        RuleFor(x => InputSanitizer.Clean(x.UserId))
            .OverridePropertyName("UserId")
            .NotEmpty().WithMessage("UserId is required.")
            .MaximumLength(256).WithMessage("UserId must not exceed 256 characters.");

        RuleFor(x => x.Environment)
            .NotEqual(EnvironmentType.None)
            .WithMessage("A valid environment must be specified (Development, Staging, or Production).");

        // UserRoles: not null, max 50 entries, each role max 100 chars after sanitization
        RuleFor(x => x.UserRoles)
            .NotNull()
            .WithMessage("UserRoles must not be null. Pass an empty array if the user has no roles.");

        RuleFor(x => x.UserRoles)
            .Must(roles => roles.Count() <= 50)
            .WithMessage("UserRoles must not exceed 50 entries.")
            .When(x => x.UserRoles is not null);

        // Validate cleaned length per role — consistent with service-layer sanitization behavior
        RuleForEach(x => x.UserRoles)
            .Must(role => (InputSanitizer.Clean(role)?.Length ?? 0) <= 100)
            .WithMessage("Each role must not exceed 100 characters.")
            .When(x => x.UserRoles is not null);
    }
}
```

---

## Files to Modify

### `Banderas.Application/DependencyInjection.cs`

Replace the full file content with:

```csharp
using Banderas.Application.Evaluation;
using Banderas.Application.Interfaces;
using Banderas.Application.Services;
using Banderas.Application.Strategies;
using Banderas.Application.Validators;
using Banderas.Domain.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Banderas.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Validators — registered explicitly; IValidator<T> injected into controllers.
        // AddValidatorsFromAssemblyContaining lives in FluentValidation.DependencyInjectionExtensions
        // (separate package) — do not use it. Explicit registration is clearer for 3 validators.
        services.AddScoped<IValidator<DTOs.CreateFlagRequest>, CreateFlagRequestValidator>();
        services.AddScoped<IValidator<DTOs.UpdateFlagRequest>, UpdateFlagRequestValidator>();
        services.AddScoped<IValidator<DTOs.EvaluationRequest>, EvaluationRequestValidator>();

        // Strategies — Singleton: stateless, safe to share across requests
        services.AddSingleton<IRolloutStrategy, NoneStrategy>();
        services.AddSingleton<IRolloutStrategy, PercentageStrategy>();
        services.AddSingleton<IRolloutStrategy, RoleStrategy>();

        // Evaluator — Singleton: depends only on Singleton strategies
        services.AddSingleton<FeatureEvaluator>();

        // Service — Scoped: depends on Scoped repository
        services.AddScoped<IBanderasService, BanderasService>();

        return services;
    }
}
```

---

### `Banderas.Api/Program.cs`

No changes required. `AddControllers()` stays exactly as-is. Do not add
`AddFluentValidationAutoValidation()` — `FluentValidation.AspNetCore` is deprecated
and not installed. Validation is handled manually in each controller action.

---

### `Banderas.Application/Services/BanderasService.cs`

`.Transform()` in validators does not mutate the DTO — the service receives the original
unsanitized values. Add `InputSanitizer` calls in two places:

**In `IsEnabledAsync`** — sanitize the evaluation context before passing to the evaluator:

```csharp
public async Task<bool> IsEnabledAsync(
    string flagName,
    FeatureEvaluationContext context,
    CancellationToken ct = default)
{
    // Sanitize evaluation inputs. .Transform() in validators does not mutate the DTO.
    // UserId and UserRoles must be cleaned here to ensure consistent SHA256 hashing
    // in PercentageStrategy and HashSet lookups in RoleStrategy.
    // Note: FeatureEvaluationContext constructor accepts IEnumerable<string> for userRoles.
    var sanitizedContext = new FeatureEvaluationContext(
        userId: Validators.InputSanitizer.Clean(context.UserId) ?? context.UserId,
        userRoles: Validators.InputSanitizer.CleanCollection(context.UserRoles),
        environment: context.Environment
    );

    var flag = await _repository.GetByNameAsync(flagName, sanitizedContext.Environment, ct);

    if (flag is null)
        throw new KeyNotFoundException(
            $"Flag '{flagName}' not found in {sanitizedContext.Environment}.");

    if (!flag.IsEnabled)
        return false;

    return _evaluator.Evaluate(flag, sanitizedContext);
}
```

**In `CreateFlagAsync`** — sanitize `Name` before constructing the entity:

```csharp
public async Task<FlagResponse> CreateFlagAsync(
    CreateFlagRequest request,
    CancellationToken ct = default)
{
    // Sanitize Name so the stored value matches the validated form.
    var sanitizedName = Validators.InputSanitizer.Clean(request.Name) ?? request.Name;

    var flag = new Flag(
        sanitizedName,
        request.Environment,
        request.IsEnabled,
        request.StrategyType,
        request.StrategyConfig);

    await _repository.AddAsync(flag, ct);
    await _repository.SaveChangesAsync(ct);
    return flag.ToResponse();
}
```

---

### `Banderas.Api/Controllers/BanderasController.cs`

Inject `IValidator<CreateFlagRequest>` and `IValidator<UpdateFlagRequest>` and validate
manually at the top of each mutating action. Read operations (GET) require no changes.

Add constructor parameters:

```csharp
private readonly IBanderasService _service;
private readonly IValidator<CreateFlagRequest> _createValidator;
private readonly IValidator<UpdateFlagRequest> _updateValidator;

public BanderasController(
    IBanderasService service,
    IValidator<CreateFlagRequest> createValidator,
    IValidator<UpdateFlagRequest> updateValidator)
{
    _service = service;
    _createValidator = createValidator;
    _updateValidator = updateValidator;
}
```

Add validation at the top of the `POST` action:

```csharp
var validation = await _createValidator.ValidateAsync(request, ct);
if (!validation.IsValid)
    return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
```

Add validation at the top of the `PUT` action:

```csharp
var validation = await _updateValidator.ValidateAsync(request, ct);
if (!validation.IsValid)
    return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
```

---

### `Banderas.Api/Controllers/EvaluationController.cs`

Inject `IValidator<EvaluationRequest>` and validate manually before constructing the
context.

Add constructor parameter:

```csharp
private readonly IBanderasService _service;
private readonly IValidator<EvaluationRequest> _validator;

public EvaluationController(IBanderasService service, IValidator<EvaluationRequest> validator)
{
    _service = service;
    _validator = validator;
}
```

Add validation at the top of the `POST` action, before the `FeatureEvaluationContext`
is constructed:

```csharp
var validation = await _validator.ValidateAsync(request, ct);
if (!validation.IsValid)
    return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
```

## Acceptance Criteria

### AC-1: Name sanitization and validation (`CreateFlagRequest`)
- `Name: " dark-mode "` (padded whitespace) → accepted, stored as `"dark-mode"`
- `Name: ""` (empty) → `400`
- `Name` exceeding 100 characters → `400`
- `Name: "my flag!"` (disallowed characters) → `400`
- `Name: "dark-mode"` → proceeds normally

### AC-2: Environment validation (all three DTOs)
- `"Environment": "None"` → `400`
- `"Environment": "Development"` → proceeds normally

### AC-3: StrategyConfig size limit
- `StrategyConfig` string exceeding 2000 characters → `400`

### AC-4: StrategyConfig cross-field validation (`CreateFlagRequest` and `UpdateFlagRequest`)
- `StrategyType: "None"` + non-empty `StrategyConfig` → `400`
- `StrategyType: "Percentage"` + missing `StrategyConfig` → `400`
- `StrategyType: "Percentage"` + `{"percentage": 0}` → `400` (below minimum)
- `StrategyType: "Percentage"` + `{"percentage": 101}` → `400` (above maximum)
- `StrategyType: "Percentage"` + `{"percentage": 30}` → proceeds normally
- `StrategyType: "RoleBased"` + `{"roles": []}` → `400` (empty array)
- `StrategyType: "RoleBased"` + `{"roles": ["Admin"]}` → proceeds normally
- Any `StrategyConfig` containing malformed JSON → `400`

### AC-5: `EvaluationRequest` — sanitization and bounds
- `UserId: " user-123 "` → accepted, evaluated as `"user-123"`
- `UserId` empty → `400`
- `UserId` exceeding 256 characters → `400`
- `FlagName` empty → `400`
- `FlagName` exceeding 100 characters → `400`
- `UserRoles: null` → `400`
- `UserRoles: []` → proceeds normally
- `UserRoles` with more than 50 entries → `400`
- Any role string exceeding 100 characters → `400`

### AC-6: Sanitization reaches evaluation logic
- `UserId` submitted with leading/trailing whitespace produces the same evaluation
  result as the same `UserId` submitted without whitespace
- A role submitted as `" Admin "` matches a flag configured with `"Admin"` in
  `StrategyConfig`

### AC-7: Error response shape
All `400` responses return `ValidationProblemDetails`:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["Flag name is required."],
    "Environment": ["A valid environment must be specified (Development, Staging, or Production)."]
  }
}
```

### AC-8: Controller validation wiring
- `BanderasController` POST and PUT actions return `400` with
  `ValidationProblemDetails` shape before any service layer code runs
- `EvaluationController` POST action returns `400` before `FeatureEvaluationContext`
  is constructed
- GET and DELETE actions in `BanderasController` require no changes

### AC-9: Build integrity
- `dotnet build` passes with 0 warnings and 0 errors
- All 8 existing tests continue to pass

---

## What NOT to Do

- Do not add `[Required]` data annotations to DTOs — FluentValidation replaces that
- Do not add `FluentValidation.AspNetCore` — it is deprecated; use manual validation
  in controllers instead
- Do not use `.Transform()` — removed in FluentValidation v12; use `RuleFor(x =>
  InputSanitizer.Clean(x.Field)).OverridePropertyName("Field")` instead
- Do not add validation logic inside `Banderas` beyond the sanitization
  calls described above
- Do not sanitize `StrategyConfig` content — it is JSON and must be stored verbatim;
  only its length and structure are validated
- Do not modify the `Flag` domain entity
- Do not modify `IBanderasService`
- Do not change `StrategyConfig` from `string` to `JsonDocument`
- Do not inline sanitization logic — always call `InputSanitizer.Clean()` or
  `CleanCollection()`

---

## Folder Structure After This Change

```
Banderas.Api/
├── Controllers/
│   ├── BanderasController.cs    ← MODIFIED (POST + PUT manual validation)
│   └── EvaluationController.cs      ← MODIFIED (POST manual validation)
└── Program.cs                        ← NO CHANGES

Banderas.Application/
├── DTOs/
│   ├── CreateFlagRequest.cs
│   ├── UpdateFlagRequest.cs
│   ├── EvaluationRequest.cs
│   ├── FlagResponse.cs
│   └── FlagMappings.cs
├── Evaluation/
│   └── FeatureEvaluator.cs
├── Interfaces/
│   └── IBanderasService.cs
├── Services/
│   └── BanderasService.cs        ← MODIFIED (sanitization calls added)
├── Strategies/
│   ├── NoneStrategy.cs
│   ├── PercentageStrategy.cs
│   └── RoleStrategy.cs
├── Validators/                       ← NEW FOLDER
│   ├── InputSanitizer.cs             ← NEW
│   ├── CreateFlagRequestValidator.cs
│   ├── UpdateFlagRequestValidator.cs
│   └── EvaluationRequestValidator.cs
└── DependencyInjection.cs            ← MODIFIED
```