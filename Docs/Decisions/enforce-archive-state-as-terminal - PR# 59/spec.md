# Specification: Enforce Archived State as Terminal

**Document:** `Docs/Decisions/enforce-archive-state-as-terminal - PR# 59/spec.md`
**Status:** Draft
**Branch:** `dev`
**PR:** #59
**Phase:** 2 — Testing & Reliability
**Depends on:** None
**Author:** Jose
**Date:** 2026-05-01

---

## Table of Contents

- [User Story](#user-story)
- [Background and Goals](#background-and-goals)
- [Design Decisions](#design-decisions)
- [Architecture Overview](#architecture-overview)
- [Scope](#scope)
- [Acceptance Criteria](#acceptance-criteria)
- [File Layout](#file-layout)
- [Technical Notes](#technical-notes)
- [Out of Scope](#out-of-scope)
- [Learning Opportunities](#learning-opportunities)
- [DX / Tooling Idea](#dx--tooling-idea)
- [Definition of Done](#definition-of-done)

---

## User Story

> As a developer consuming the Banderas API, I want attempts to mutate an archived
> flag to return a clear `409 Conflict` error — so I know immediately that the
> operation was rejected by a business rule, not a server fault.

---

## Background and Goals

`Flag` currently allows mutation after archival. Calling `SetEnabled()`,
`Update()`, `UpdateStrategy()`, `UpdateName()`, or `Archive()` a second time on
an already-archived flag silently succeeds — the entity changes, `UpdatedAt`
advances, and the change is persisted. This is incorrect domain behavior.

Archived is a **terminal state** in the Banderas domain. Once a flag is archived
it is a historical record. No further mutations are meaningful or permitted. This
rule must be enforced inside the `Flag` entity itself — not in a service, not in a
validator, not in a controller — because the entity is the only place that owns its
own consistency.

This spec introduces `FlagDomainException` (if not already present) and adds a
guard clause at the top of every mutation method on `Flag`.

An `UnarchiveFlag` operation is **explicitly out of scope** and is deferred to
Phase 3, where it will require an authorized principal and a dedicated, audited
code path.

---

## Design Decisions

### DD-1 — Single exception type with a descriptive message over per-violation exceptions

**Options considered:**

| Option | Pros | Cons |
|---|---|---|
| One `FlagDomainException` with a message | One type to maintain, one middleware case, scales to future invariants | Less granular if callers need to distinguish violations programmatically |
| `FlagAlreadyArchivedException` + future specific types | Precise, easy to catch by type | N domain invariants = N exception types; maintenance overhead grows with the model |

**Decision:** Use `FlagDomainException` with a descriptive message. The message
is the specificity. The middleware maps one base type to 409. This is consistent
with how `DuplicateFlagNameException` works today — a single type per violation
category, not per field.

**Rationale:** The domain model is growing. If every invariant violation gets its
own exception type, the `Exceptions/` folder becomes noise. The message string
carries the business-language detail callers need for error reporting.

---

### DD-2 — Guard clause on `Archive()` itself, not just the other mutation methods

**Options considered:**

| Option | Pros | Cons |
|---|---|---|
| Guard on all 5 methods including `Archive()` | Double-archive is caught; caller gets a clear error | Could argue double-archive is a no-op |
| Guard on 4 mutation methods only, skip `Archive()` | Idempotent archive is sometimes desirable | Hides a likely caller bug; inconsistent with "archived is immutable" |

**Decision:** Guard on all 5 methods including `Archive()`. If a caller is
trying to archive an already-archived flag, that is a bug in the caller, not a
legitimate operation. Returning an error surfaces the bug. Making it a silent
no-op hides it.

---

### DD-3 — Domain throws, middleware catches — no intermediary handling

**Options considered:**

| Option | Pros | Cons |
|---|---|---|
| Domain throws → middleware catches (current pattern) | Consistent with all other domain exceptions; zero new coupling | None — this is the established pattern |
| Application layer catches and maps to a result type | More explicit flow control | Requires Application to know about EF/HTTP; violates layer rules |
| Controller try/catch | Simple | Returns to the per-controller error handling anti-pattern we removed in PR #36 |

**Decision:** Domain throws `FlagDomainException`. It extends `BanderasException`.
`GlobalExceptionMiddleware` catches it via the existing `BanderasException` catch
block and writes a `409 ProblemDetails` response automatically. No new middleware
code required.

---

### DD-4 — Archived state is strictly terminal now; unarchive is a Phase 3 feature

**Options considered:**

| Option | Pros | Cons |
|---|---|---|
| Strictly terminal now, unarchive later | Simple, correct default; no premature auth logic | Requires a deliberate Phase 3 PR to relax |
| Leave open now "just in case" | Fewer future changes | Permits invalid state today; auth concern bleeds into domain prematurely |

**Decision:** Strictly terminal. Document the unarchive path in the roadmap and
DDD backlog for Phase 3. It must be an auth-gated, audited operation when it
arrives — not a relaxed domain rule.

---

## Architecture Overview

This change is entirely within the **Domain layer**. It does not cross any
layer boundary.

```
Banderas.Domain
└── Exceptions/
│   └── FlagDomainException.cs    ← NEW (or verify already exists)
└── Entities/
    └── Flag.cs                   ← MODIFY: add guard clause to 5 methods
```

The `GlobalExceptionMiddleware` in `Banderas.Api` already catches
`BanderasException` via the base class. Because `FlagDomainException` extends
`BanderasException` with `StatusCode = 409`, no middleware change is needed.

**Dependency rule check:** Domain has no dependency on Application, Infrastructure,
or Api. This change adds nothing new. ✅

---

## Scope

| # | Action | File |
|---|---|---|
| 1 | Create (or verify) `FlagDomainException` | `Banderas.Domain/Exceptions/FlagDomainException.cs` |
| 2 | Add guard clause to `SetEnabled()` | `Banderas.Domain/Entities/Flag.cs` |
| 3 | Add guard clause to `UpdateStrategy()` | `Banderas.Domain/Entities/Flag.cs` |
| 4 | Add guard clause to `UpdateName()` | `Banderas.Domain/Entities/Flag.cs` |
| 5 | Add guard clause to `Update()` | `Banderas.Domain/Entities/Flag.cs` |
| 6 | Add guard clause to `Archive()` | `Banderas.Domain/Entities/Flag.cs` |
| 7 | Unit tests for all 5 guard clauses | `Banderas.Tests/Domain/FlagArchivedInvariantTests.cs` |
| 8 | Verify `GlobalExceptionMiddleware` 409 path | `Banderas.Api/Middleware/GlobalExceptionMiddleware.cs` |

No controller changes. No service changes. No repository changes. No migration.

---

## Acceptance Criteria

### AC-1: `FlagDomainException` exists and extends `BanderasException`

**File:** `Banderas.Domain/Exceptions/FlagDomainException.cs`

If the file already exists, verify it matches this shape. If it does not exist,
create it.

```csharp
using Microsoft.AspNetCore.Http;

namespace Banderas.Domain.Exceptions;

/// <summary>
/// Thrown when an operation violates a domain invariant on Flag.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class FlagDomainException : BanderasException
{
    public FlagDomainException(string message)
        : base(message, StatusCodes.Status409Conflict) { }
}
```

**Rules:**
- Must extend `BanderasException` — this is what connects it to the middleware.
- `StatusCode` is `409`. The middleware derives the HTTP status from `ex.StatusCode`.
- `sealed` — not intended for subclassing.
- No ASP.NET Core references except `StatusCodes` — consistent with existing
  exceptions in the hierarchy.

---

### AC-2: Guard clause added to all five mutation methods on `Flag`

**File:** `Banderas.Domain/Entities/Flag.cs`

Add the following guard clause as the **first statement** inside each of these
five methods: `SetEnabled()`, `UpdateStrategy()`, `UpdateName()`, `Update()`,
`Archive()`.

```csharp
if (IsArchived)
    throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");
```

The complete updated signatures after modification:

```csharp
public void SetEnabled(bool enabled)
{
    if (IsArchived)
        throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");

    IsEnabled = enabled;
    UpdatedAt = DateTime.UtcNow;
}

public void UpdateStrategy(RolloutStrategy strategyType, string? strategyConfig)
{
    if (IsArchived)
        throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");

    StrategyType = strategyType;
    StrategyConfig = strategyConfig ?? "{}";
    UpdatedAt = DateTime.UtcNow;
}

public void UpdateName(string name)
{
    if (IsArchived)
        throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");

    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty.", nameof(name));

    Name = name;
    UpdatedAt = DateTime.UtcNow;
}

public void Update(bool isEnabled, RolloutStrategy strategyType, string? strategyConfig)
{
    if (IsArchived)
        throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");

    IsEnabled = isEnabled;
    StrategyType = strategyType;
    StrategyConfig = strategyConfig ?? "{}";
    UpdatedAt = DateTime.UtcNow;
}

public void Archive()
{
    if (IsArchived)
        throw new FlagDomainException($"Flag '{Name}' is archived and cannot be modified.");

    IsArchived = true;
    ArchivedAt = DateTime.UtcNow;
    UpdatedAt = DateTime.UtcNow;
}
```

**Rules:**
- Guard clause must be the **first statement** in each method — before any
  argument validation, before any state mutation.
- The exception message must include the flag's `Name` — callers need to know
  which flag was rejected.
- Use `FlagDomainException` — not `InvalidOperationException`, not
  `ArgumentException`, not a raw `Exception`.
- Do not change the method signatures. Do not change any logic below the guard.
- `using Banderas.Domain.Exceptions;` must be added to the top of `Flag.cs`
  (it is not currently present).
- **Behavior shift on `UpdateName`:** the archived-guard runs *before* the
  existing whitespace-name guard. After this change, calling `UpdateName("")`
  on an archived flag throws `FlagDomainException` instead of `ArgumentException`.
  No existing caller or test depends on the previous order, but this is an
  intentional, observable change.

---

### AC-3: Unit tests for all guard clauses

**File:** `Banderas.Tests/Domain/FlagArchivedInvariantTests.cs`

Create a new test class. Use `FlagBuilder` (already present in
`Banderas.Tests/Helpers/FlagBuilder.cs`) to construct flags.

The test class must carry `[Trait("Category", "Unit")]` at the class level.

**Tests to implement:**

**AI-1 — `SetEnabled` on an archived flag throws `FlagDomainException`**
```
SetEnabled_WhenFlagIsArchived_ThrowsFlagDomainException
```
Build a flag. Call `Archive()`. Call `SetEnabled(true)`.
Assert `FlagDomainException` is thrown.

**AI-2 — `UpdateStrategy` on an archived flag throws `FlagDomainException`**
```
UpdateStrategy_WhenFlagIsArchived_ThrowsFlagDomainException
```
Build a flag. Call `Archive()`. Call `UpdateStrategy(RolloutStrategy.None, null)`.
Assert `FlagDomainException` is thrown.

**AI-3 — `UpdateName` on an archived flag throws `FlagDomainException`**
```
UpdateName_WhenFlagIsArchived_ThrowsFlagDomainException
```
Build a flag. Call `Archive()`. Call `UpdateName("new-name")`.
Assert `FlagDomainException` is thrown.

**AI-4 — `Update` on an archived flag throws `FlagDomainException`**
```
Update_WhenFlagIsArchived_ThrowsFlagDomainException
```
Build a flag. Call `Archive()`. Call `Update(false, RolloutStrategy.None, null)`.
Assert `FlagDomainException` is thrown.

**AI-5 — `Archive` called twice throws `FlagDomainException`**
```
Archive_WhenFlagIsAlreadyArchived_ThrowsFlagDomainException
```
Build a flag. Call `Archive()`. Call `Archive()` again.
Assert `FlagDomainException` is thrown.

**AI-6 — Mutations on a non-archived flag succeed (baseline, one per method)**

Add a happy-path assert per guarded method to catch copy-paste errors in the
guard (e.g. `if (!IsArchived)`):

```
SetEnabled_WhenFlagIsNotArchived_Succeeds
UpdateStrategy_WhenFlagIsNotArchived_Succeeds
UpdateName_WhenFlagIsNotArchived_Succeeds
Update_WhenFlagIsNotArchived_Succeeds
Archive_WhenFlagIsNotArchived_Succeeds
```

Each builds a non-archived flag, invokes the method, and asserts no exception
plus the expected state mutation occurred (e.g. `IsEnabled == true`,
`IsArchived == true`, etc.). One-liner asserts are sufficient.

**Test structure example:**

```csharp
[Fact]
[Trait("Category", "Unit")]
public void SetEnabled_WhenFlagIsArchived_ThrowsFlagDomainException()
{
    // Arrange
    var flag = FlagBuilder.Build();
    flag.Archive();

    // Act
    var act = () => flag.SetEnabled(true);

    // Assert
    act.Should().Throw<FlagDomainException>();
}
```

---

### AC-4: `GlobalExceptionMiddleware` — verify 409 path

**File:** `Banderas.Api/Middleware/GlobalExceptionMiddleware.cs`

No code change required — verified present. The `BanderasException` catch block
exists (line 39) and `GetTitleForStatusCode` already includes the `409` case
(line 111):

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

This AC is satisfied by the current state of the file. No work item.

---

## File Layout

```
Banderas.Domain/
└── Exceptions/
│   └── FlagDomainException.cs         ← CREATE (or verify)
└── Entities/
    └── Flag.cs                         ← MODIFY

Banderas.Tests/
└── Domain/
    └── FlagArchivedInvariantTests.cs   ← CREATE
```

No other files are created or modified.

---

## Technical Notes

- `FlagDomainException` needs a `FrameworkReference` to
  `Microsoft.AspNetCore.App` to use `StatusCodes` — this reference is already
  present on `Banderas.Domain` from prior work (`FlagNotFoundException` uses it).
  No package change required.
- `FlagBuilder` is in `Banderas.Tests/Helpers/FlagBuilder.cs`. Use it as-is.
  If `FlagBuilder` does not expose a way to produce an already-archived flag
  directly, call `Archive()` on the built flag inside the test — do not add
  an `isArchived` parameter to `FlagBuilder` as part of this PR.
- `Flag.cs` currently has only `using Banderas.Domain.Enums;`. This PR **must**
  add `using Banderas.Domain.Exceptions;` at the top of the file — same-assembly
  namespaces are not implicitly imported, and there is no `GlobalUsings` file in
  this project.
- The `xmin`/`Version` concurrency property added in the previous PR must remain
  untouched. The guard clauses are inserted above existing mutation logic only.
- **No API-level integration test** asserts the end-to-end `409` response for
  an archived-flag mutation in this PR. Middleware mapping
  (`FlagDomainException` → `409 Conflict`) is verified by inspection only here;
  HTTP-level coverage is deferred to the Phase 2 follow-up PR listed in
  *Out of Scope*.

---

## Out of Scope

| Item | Deferred to |
|---|---|
| `UnarchiveFlag` operation | Phase 3 — requires auth-gated, audited code path |
| Authorization policy for who may archive a flag | Phase 3 — JWT + RBAC |
| API-level integration test asserting `409` response from a `PUT` or `DELETE` on an archived flag | Phase 2 follow-up PR — `refactor/flag-domain-invariants` integration coverage |
| `IsSeeded` removal from `Flag` | Next item in DDD backlog — separate PR |
| Any change to `FeatureFlagContext`, `FeatureEvaluationContext`, or evaluation logic | Out of scope for this refactor |

---

## Learning Opportunities

**1. Make Illegal States Unrepresentable (MISR)**
This is a core DDD principle. Instead of checking "is the flag archived?" in
multiple places across the service and controller layers, we encode the rule
in the type itself. An archived `Flag` object physically cannot be mutated
without throwing. The compiler and runtime together enforce the business rule —
not a validator somewhere downstream that might be forgotten.

**2. Guard clauses as domain invariant enforcement**
Guard clauses at the top of methods are the idiomatic C# way to enforce
preconditions. The pattern is: check the condition first, throw if violated,
then proceed with the happy path. This keeps the method's intent visible and
the happy path unindented. In DDD, these guard clauses are the domain's
"immune system" — they prevent the model from entering an invalid state.

**3. Exception hierarchy as a communication protocol**
`FlagDomainException` extending `BanderasException` is not just inheritance for
reuse — it is a communication protocol between the domain and the middleware.
The middleware only needs to know `BanderasException`; it does not need to know
every specific violation type. Adding a new domain rule violation in the future
requires only a new `throw new FlagDomainException(...)` — the middleware
already handles it.

---

## DX / Tooling Idea

Add an `IsArchived` assertion helper to `FlagBuilder` — not a constructor
parameter, but a fluent method:

```csharp
public static Flag BuildArchived() =>
    Build().TapArchive();
```

where `TapArchive()` is an internal test helper extension that calls
`flag.Archive()` and returns the flag. This makes archived-flag tests a
one-liner in setup and signals clearly to the reader that the test intent
is "an archived flag." Defer to after this PR if `FlagBuilder` needs broader
changes.

---

## Definition of Done

- [ ] `FlagDomainException` exists in `Banderas.Domain/Exceptions/` and extends
      `BanderasException` with `StatusCode = 409`
- [ ] Guard clause is the first statement in `SetEnabled()`
- [ ] Guard clause is the first statement in `UpdateStrategy()`
- [ ] Guard clause is the first statement in `UpdateName()`
- [ ] Guard clause is the first statement in `Update()`
- [ ] Guard clause is the first statement in `Archive()`
- [ ] All 10 tests in `FlagArchivedInvariantTests.cs` pass (5 archived-guard + 5 happy-path)
- [ ] `using Banderas.Domain.Exceptions;` added to top of `Flag.cs`
- [ ] `[Trait("Category", "Unit")]` is present on the test class
- [ ] `GlobalExceptionMiddleware.GetTitleForStatusCode` includes the `409` case
- [ ] `dotnet build` passes with zero warnings
- [ ] `dotnet csharpier check .` passes
- [ ] All existing tests continue to pass — no regressions
