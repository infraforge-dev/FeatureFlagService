# Spec: Input Validation Hardening

**Document:** `Docs/Decisions/input-validation-hardening/spec.md`
**Branch:** `fix/input-validation-hardening`
**Phase:** 1 — MVP Completion
**Closes:** KI-008, KI-NEW-001
**Status:** Ready for Implementation
**Author:** Jose / Claude Architect Session
**Date:** 2026-04-03

---

## Table of Contents

- [User Story](#user-story)
- [Background and Goals](#background-and-goals)
- [Scope](#scope)
- [AC-1: StrategyConfigRules — Extract Shared Validator Logic](#ac-1-strategyconfigrules--extract-shared-validator-logic)
- [AC-2: Update Both Validators to Call StrategyConfigRules](#ac-2-update-both-validators-to-call-strategyconfigrules)
- [AC-3: IFeatureFlagRepository — Add ExistsAsync](#ac-3-ifeatureflagrepository--add-existsasync)
- [AC-4: FeatureFlagRepository — Implement ExistsAsync](#ac-4-featureflagrepository--implement-existsasync)
- [AC-5: FeatureFlagService — Enforce Name Uniqueness on Create](#ac-5-featureflagservice--enforce-name-uniqueness-on-create)
- [AC-6: GlobalExceptionMiddleware — Register 409 Title](#ac-6-globalexceptionmiddleware--register-409-title)
- [AC-7: RouteParameterGuard — Allowlist Validation on URL Parameters](#ac-7-routeparameterguard--allowlist-validation-on-url-parameters)
- [AC-8: FeatureFlagsController — Call RouteParameterGuard](#ac-8-featureflagscontroller--call-routeparameterguard)
- [File Layout](#file-layout)
- [What NOT to Do](#what-not-to-do)
- [Definition of Done](#definition-of-done)

---

## User Story

> As a developer integrating with FeatureFlagService, I want invalid flag names
> rejected at the URL boundary, and duplicate flag names rejected with a clear
> `409 Conflict` before the database is touched — so I receive actionable error
> responses and no garbage reaches the application internals.

---

## Background and Goals

Three open issues remain in the input validation layer:

1. **KI-008** — `GET /api/flags/{name}` and `PUT /api/flags/{name}` accept any
   string as a route parameter. The request body is validated by FluentValidation,
   but the URL segment bypasses all validation. EF Core parameterized queries
   prevent SQL injection, but unexpected characters reach logs and the repository.

2. **Name uniqueness** — `DuplicateFlagNameException` is defined in
   `FeatureFlag.Domain/Exceptions/` but is never thrown. A duplicate `POST` reaches
   the database, which throws a PostgreSQL unique constraint violation, which the
   middleware catches as an unhandled `Exception` and returns a `500`. The correct
   response is `409 Conflict`.

3. **KI-NEW-001** — `BeValidPercentageConfig` and `BeValidRoleConfig` are private
   static methods duplicated identically in both `CreateFlagRequestValidator` and
   `UpdateFlagRequestValidator`. They will drift the moment one is updated without
   the other.

This spec addresses all three in a single focused PR. No new packages are required.

---

## Scope

| # | What | Layer | File(s) |
|---|---|---|---|
| 1 | Extract `StrategyConfigRules` shared class | Application | `FeatureFlag.Application/Validators/StrategyConfigRules.cs` |
| 2 | Remove duplicated methods from both validators | Application | `CreateFlagRequestValidator.cs`, `UpdateFlagRequestValidator.cs` |
| 3 | Add `RouteParameterGuard` helper | Api | `FeatureFlag.Api/Helpers/RouteParameterGuard.cs` |
| 4 | Call `RouteParameterGuard.ValidateName()` in controller | Api | `FeatureFlag.Api/Controllers/FeatureFlagsController.cs` |
| 5 | Add `ExistsAsync` to `IFeatureFlagRepository` | Domain | `FeatureFlag.Domain/Interfaces/IFeatureFlagRepository.cs` |
| 6 | Implement `ExistsAsync` in `FeatureFlagRepository` | Infrastructure | `FeatureFlag.Infrastructure/Repositories/FeatureFlagRepository.cs` |
| 7 | Call `ExistsAsync` + throw in `CreateFlagAsync` | Application | `FeatureFlag.Application/Services/FeatureFlagService.cs` |
| 8 | Add `409` title to middleware switch | Api | `FeatureFlag.Api/Middleware/GlobalExceptionMiddleware.cs` |

---

## AC-1: StrategyConfigRules — Extract Shared Validator Logic

**File:** `FeatureFlag.Application/Validators/StrategyConfigRules.cs` *(new)*

Extract the two private static methods currently duplicated in both validators
into a single shared `internal static` class.

```csharp
using System.Text.Json;

namespace FeatureFlag.Application.Validators;

/// <summary>
/// Shared strategy config validation rules. Called by both
/// CreateFlagRequestValidator and UpdateFlagRequestValidator.
/// Add new strategy rules here when new IRolloutStrategy types are introduced.
/// </summary>
internal static class StrategyConfigRules
{
    /// <summary>
    /// Returns true if config is valid JSON containing a 'percentage'
    /// integer field with a value between 1 and 100 inclusive.
    /// </summary>
    internal static bool BeValidPercentageConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return false;

        try
        {
            var doc = JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("percentage", out JsonElement prop))
                return false;
            if (!prop.TryGetInt32(out int percentage))
                return false;
            return percentage >= 1 && percentage <= 100;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true if config is valid JSON containing a 'roles' array
    /// with at least one element.
    /// </summary>
    internal static bool BeValidRoleConfig(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return false;

        try
        {
            var doc = JsonDocument.Parse(config);
            if (!doc.RootElement.TryGetProperty("roles", out JsonElement prop))
                return false;
            if (prop.ValueKind != JsonValueKind.Array)
                return false;
            return prop.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
```

**Rules:**
- `internal static` — not part of the public API; accessible within
  `FeatureFlag.Application` only
- No constructor, no state — pure static methods
- XML doc comments required on both methods
- `using System.Text.Json` at the top — do not use the fully qualified
  `System.Text.Json.JsonDocument` inline; the `using` directive is cleaner

---

## AC-2: Update Both Validators to Call StrategyConfigRules

**Files:**
- `FeatureFlag.Application/Validators/CreateFlagRequestValidator.cs` *(modify)*
- `FeatureFlag.Application/Validators/UpdateFlagRequestValidator.cs` *(modify)*

In **both** validators:

1. Delete the `BeValidPercentageConfig` private static method entirely.
2. Delete the `BeValidRoleConfig` private static method entirely.
3. Replace all `.Must(BeValidPercentageConfig)` calls with
   `.Must(StrategyConfigRules.BeValidPercentageConfig)`.
4. Replace all `.Must(BeValidRoleConfig)` calls with
   `.Must(StrategyConfigRules.BeValidRoleConfig)`.

All `RuleFor` chains, `WithMessage` strings, and `.When()` conditions remain
unchanged. The only change is the method reference in each `.Must()` call and
the removal of the now-deleted private methods at the bottom of each class.

No other changes to either file.

---

## AC-3: IFeatureFlagRepository — Add ExistsAsync

**File:** `FeatureFlag.Domain/Interfaces/IFeatureFlagRepository.cs` *(modify)*

Add one method to the interface:

```csharp
/// <summary>
/// Returns true if a non-archived flag with the given name and environment
/// already exists in the store.
/// </summary>
Task<bool> ExistsAsync(
    string name,
    EnvironmentType environment,
    CancellationToken ct = default
);
```

The full interface after the change:

```csharp
using FeatureFlag.Domain.Entities;
using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Domain.Interfaces;

public interface IFeatureFlagRepository
{
    Task<Flag?> GetByNameAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task<bool> ExistsAsync(
        string name,
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<Flag>> GetAllAsync(
        EnvironmentType environment,
        CancellationToken ct = default
    );
    Task AddAsync(Flag flag, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**Rules:**
- `ExistsAsync` must check non-archived flags only. A flag that has been archived
  does not block creation of a new flag with the same name in the same environment.
- XML doc comment required on the interface method.
- Signature follows the existing convention: optional `CancellationToken ct = default`.

---

## AC-4: FeatureFlagRepository — Implement ExistsAsync

**File:** `FeatureFlag.Infrastructure/Repositories/FeatureFlagRepository.cs` *(modify)*

Add the implementation for `ExistsAsync`. Use `AnyAsync` — do not fetch the full
entity when a boolean is sufficient.

```csharp
public async Task<bool> ExistsAsync(
    string name,
    EnvironmentType environment,
    CancellationToken ct = default
) =>
    await _context.Flags
        .Where(f => f.Name == name
                 && f.Environment == environment
                 && !f.IsArchived)
        .AnyAsync(ct);
```

**Rules:**
- Use `AnyAsync` — not `FirstOrDefaultAsync`, not `CountAsync`.
- The `Where` clause must filter on all three conditions: `Name`, `Environment`,
  and `!IsArchived`.
- No `ToListAsync`, no materialisation of entities.
- `_context` is the existing injected `FeatureFlagDbContext` field — do not change
  its name or type.

---

## AC-5: FeatureFlagService — Enforce Name Uniqueness on Create

**File:** `FeatureFlag.Application/Services/FeatureFlagService.cs` *(modify)*

In `CreateFlagAsync`, add an existence check before the `AddAsync` call.

The updated method body (the method signature is unchanged):

```csharp
public async Task<FlagResponse> CreateFlagAsync(
    CreateFlagRequest request,
    CancellationToken ct = default
)
{
    var name = InputSanitizer.Clean(request.Name)!;

    if (await _repository.ExistsAsync(name, request.Environment, ct))
        throw new DuplicateFlagNameException(name, request.Environment);

    var flag = new Flag(
        name,
        request.Environment,
        request.IsEnabled,
        request.StrategyType,
        request.StrategyConfig
    );

    await _repository.AddAsync(flag, ct);
    await _repository.SaveChangesAsync(ct);
    return flag.ToResponse();
}
```

**Rules:**
- Sanitize `request.Name` with `InputSanitizer.Clean()` before the existence
  check and before constructing `Flag`. Use the cleaned value for both.
- The `null`-forgiving operator (`!`) on `InputSanitizer.Clean(request.Name)!`
  is intentional — `NotEmpty` in the validator guarantees the value is non-null
  and non-whitespace before the service is reached.
- Throw `DuplicateFlagNameException` — not `InvalidOperationException`,
  not a string message. The exception is already defined in
  `FeatureFlag.Domain/Exceptions/DuplicateFlagNameException.cs`.
- `DuplicateFlagNameException` constructor currently accepts `(string flagName)`.
  Update the constructor to also accept `EnvironmentType environment` so the
  error message can include the environment. See constructor update below.
- Add `using FeatureFlag.Domain.Exceptions;` if not already present.

### DuplicateFlagNameException constructor update

**File:** `FeatureFlag.Domain/Exceptions/DuplicateFlagNameException.cs` *(modify)*

Update the constructor signature and message to include the environment:

```csharp
using Microsoft.AspNetCore.Http;
using FeatureFlag.Domain.Enums;

namespace FeatureFlag.Domain.Exceptions;

/// <summary>
/// Thrown when a flag with the given name already exists in the specified
/// environment. Maps to HTTP 409 Conflict.
/// </summary>
public sealed class DuplicateFlagNameException : FeatureFlagException
{
    public DuplicateFlagNameException(string flagName, EnvironmentType environment)
        : base(
            $"A feature flag named '{flagName}' already exists in {environment}.",
            StatusCodes.Status409Conflict
        )
    {
    }
}
```

Add `using FeatureFlag.Domain.Enums;` to the file.

---

## AC-6: GlobalExceptionMiddleware — Register 409 Title

**File:** `FeatureFlag.Api/Middleware/GlobalExceptionMiddleware.cs` *(modify)*

The `GetTitleForStatusCode` switch already handles `400` and `404`. Confirm that
`409` is present. The current implementation in the codebase already includes it:

```csharp
private static string GetTitleForStatusCode(int statusCode) =>
    statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status404NotFound   => "Not Found",
        StatusCodes.Status409Conflict   => "Conflict",
        _                               => "An error occurred",
    };
```

**No change required if `409` is already present.** Verify it exists. If absent,
add it. This is a verification step, not a guaranteed code change.

---

## AC-7: RouteParameterGuard — Allowlist Validation on URL Parameters

**File:** `FeatureFlag.Api/Helpers/RouteParameterGuard.cs` *(new)*

Create the `Helpers/` folder under `FeatureFlag.Api/` if it does not exist.

```csharp
using System.Text.RegularExpressions;
using FeatureFlag.Domain.Exceptions;

namespace FeatureFlag.Api.Helpers;

/// <summary>
/// Guards route parameters against values that do not conform to the
/// flag name allowlist. Called at the top of controller actions that
/// accept a {name} route segment before any service logic runs.
/// </summary>
public static class RouteParameterGuard
{
    private static readonly Regex NamePattern =
        new(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);

    /// <summary>
    /// Throws <see cref="FeatureFlagValidationException"/> if <paramref name="name"/>
    /// contains characters outside the allowed set (letters, digits, hyphens,
    /// underscores). Callers should return the resulting 400 response immediately.
    /// </summary>
    /// <exception cref="FeatureFlagValidationException">
    /// Thrown when <paramref name="name"/> fails the allowlist check.
    /// </exception>
    public static void ValidateName(string name)
    {
        if (!NamePattern.IsMatch(name))
            throw new FeatureFlagValidationException(
                "Flag name may only contain letters, numbers, hyphens, and underscores."
            );
    }
}
```

### FeatureFlagValidationException — new domain exception

**File:** `FeatureFlag.Domain/Exceptions/FeatureFlagValidationException.cs` *(new)*

```csharp
using Microsoft.AspNetCore.Http;

namespace FeatureFlag.Domain.Exceptions;

/// <summary>
/// Thrown when a request parameter fails allowlist or structural validation
/// outside the FluentValidation pipeline (e.g. route parameters).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class FeatureFlagValidationException : FeatureFlagException
{
    public FeatureFlagValidationException(string message)
        : base(message, StatusCodes.Status400BadRequest)
    {
    }
}
```

**Why a new domain exception rather than returning `BadRequest()` directly from
the controller?**

Controllers must contain only the happy path — no conditional returns, no
`if (bad) return BadRequest(...)` inline. `GlobalExceptionMiddleware` already
handles all `FeatureFlagException` subclasses and maps them to the correct
`ProblemDetails` response. `FeatureFlagValidationException` follows the same
Open/Closed pattern established in PR #36: add a new exception subclass,
and the middleware handles it automatically without modification.

---

## AC-8: FeatureFlagsController — Call RouteParameterGuard

**File:** `FeatureFlag.Api/Controllers/FeatureFlagsController.cs` *(modify)*

Add `RouteParameterGuard.ValidateName(name)` as the **first line** of
`GetByNameAsync`, `UpdateAsync`, and `ArchiveAsync`. No other changes to any action.

**GetByNameAsync — after:**
```csharp
public async Task<IActionResult> GetByNameAsync(
    string name,
    [FromQuery] EnvironmentType environment,
    CancellationToken ct)
{
    RouteParameterGuard.ValidateName(name);
    var flag = await _service.GetFlagAsync(name, environment, ct);
    return Ok(flag);
}
```

**UpdateAsync — after:**
```csharp
public async Task<IActionResult> UpdateAsync(
    string name,
    [FromQuery] EnvironmentType environment,
    [FromBody] UpdateFlagRequest request,
    CancellationToken ct)
{
    RouteParameterGuard.ValidateName(name);
    // ... existing validation and service call unchanged
}
```

**ArchiveAsync — after:**
```csharp
public async Task<IActionResult> ArchiveAsync(
    string name,
    [FromQuery] EnvironmentType environment,
    CancellationToken ct)
{
    RouteParameterGuard.ValidateName(name);
    // ... existing service call unchanged
}
```

**Rules:**
- `RouteParameterGuard.ValidateName(name)` must be the first statement in all
  three actions — before `ValidateAsync`, before any service call.
- Do not add `try/catch`. `GlobalExceptionMiddleware` catches
  `FeatureFlagValidationException` and returns the `400 ProblemDetails` response.
- Add `using FeatureFlag.Api.Helpers;` to the controller file.
- Do not add the guard to `GetAllAsync` or `CreateAsync` — those actions take
  no `{name}` route parameter.

---

## File Layout

```
FeatureFlag.Domain/
  Exceptions/
    FeatureFlagException.cs              (existing — no change)
    FlagNotFoundException.cs             (existing — no change)
    DuplicateFlagNameException.cs        (existing — modify constructor)
    FeatureFlagValidationException.cs    (NEW)

FeatureFlag.Application/
  Validators/
    InputSanitizer.cs                    (existing — no change)
    StrategyConfigRules.cs               (NEW)
    CreateFlagRequestValidator.cs        (modify — remove duplicated methods)
    UpdateFlagRequestValidator.cs        (modify — remove duplicated methods)
  Services/
    FeatureFlagService.cs                (modify — add uniqueness check)

FeatureFlag.Infrastructure/
  Repositories/
    FeatureFlagRepository.cs             (modify — implement ExistsAsync)

FeatureFlag.Api/
  Helpers/
    RouteParameterGuard.cs               (NEW)
  Controllers/
    FeatureFlagsController.cs            (modify — add guard calls)
  Middleware/
    GlobalExceptionMiddleware.cs         (verify 409 present)
```

---

## What NOT to Do

- Do not add `try/catch` to any controller action — `GlobalExceptionMiddleware`
  handles all exceptions
- Do not call `InputSanitizer` from `RouteParameterGuard` — the guard checks
  structural validity only; sanitization is the service layer's responsibility
- Do not use `CountAsync` or `FirstOrDefaultAsync` in `ExistsAsync` — use `AnyAsync`
- Do not add `RouteParameterGuard` to `GetAllAsync` or `CreateAsync` — those
  endpoints take no `{name}` route parameter
- Do not change any `WithMessage` strings in the validators — only the `.Must()`
  method references change
- Do not add `FluentValidation.AspNetCore` or `AddFluentValidationAutoValidation()`
- Do not use `.Transform()` — removed in FluentValidation v12
- Do not run `dotnet format` without following up with `dotnet csharpier format .`

---

## Definition of Done

- [ ] `StrategyConfigRules.cs` created with `BeValidPercentageConfig` and
      `BeValidRoleConfig` as `internal static` methods
- [ ] Both duplicated private methods removed from `CreateFlagRequestValidator`
- [ ] Both duplicated private methods removed from `UpdateFlagRequestValidator`
- [ ] Both validators call `StrategyConfigRules.BeValidPercentageConfig` and
      `StrategyConfigRules.BeValidRoleConfig` in their `.Must()` chains
- [ ] `FeatureFlagValidationException` created in `FeatureFlag.Domain/Exceptions/`
- [ ] `RouteParameterGuard.ValidateName()` created in `FeatureFlag.Api/Helpers/`
- [ ] `RouteParameterGuard.ValidateName(name)` is the first call in
      `GetByNameAsync`, `UpdateAsync`, and `ArchiveAsync`
- [ ] `ExistsAsync` added to `IFeatureFlagRepository` with XML doc comment
- [ ] `ExistsAsync` implemented in `FeatureFlagRepository` using `AnyAsync`
- [ ] `DuplicateFlagNameException` constructor updated to accept
      `(string flagName, EnvironmentType environment)`
- [ ] `CreateFlagAsync` calls `ExistsAsync` and throws
      `DuplicateFlagNameException` before `AddAsync`
- [ ] `GlobalExceptionMiddleware.GetTitleForStatusCode` contains the `409`
      case (verify; add if absent)
- [ ] `POST /api/flags` with a duplicate name returns `409` with
      `application/problem+json` and a `detail` naming the flag and environment
- [ ] `GET /api/flags/{name}` with `name = "bad name!"` returns `400` with
      `application/problem+json`
- [ ] `PUT /api/flags/{name}` with `name = "bad name!"` returns `400` with
      `application/problem+json`
- [ ] `DELETE /api/flags/{name}` with `name = "bad name!"` returns `400` with
      `application/problem+json`
- [ ] `dotnet build FeatureFlagService.sln` → 0 errors, 0 warnings
- [ ] All existing tests passing: `dotnet test --filter "Category!=Integration"`
- [ ] `dotnet csharpier check .` → 0 violations

---

*FeatureFlagService | fix/input-validation-hardening | Phase 1 | v1.0*
