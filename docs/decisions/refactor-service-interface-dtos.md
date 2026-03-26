# Refactor: Service Interface — Domain Leakage Fix

## Branch
`refactor/service-interface-dtos`

## Problem

`IFeatureFlagService` currently:
- Returns `Flag` domain entities to callers (controllers)
- Accepts a raw `Flag` entity in `CreateFlagAsync`
- Accepts 5 primitive parameters in `UpdateFlagAsync` instead of a DTO

This breaks Clean Architecture. The controller is coupled to the domain model
and must call `.ToResponse()` itself — logic that belongs inside the service.

## Goal

Make `IFeatureFlagService` speak entirely in DTOs. The domain entity `Flag`
must never cross the service boundary. All mapping happens inside
`FeatureFlagService`, not in controllers.

---

## Changes Required

### 1. `IFeatureFlagService` — update signatures
```csharp
Task<FlagResponse> GetFlagAsync(
    string name, EnvironmentType environment, CancellationToken ct = default);

Task<IReadOnlyList<FlagResponse>> GetAllFlagsAsync(
    EnvironmentType environment, CancellationToken ct = default);

Task<FlagResponse> CreateFlagAsync(
    CreateFlagRequest request, CancellationToken ct = default);

Task UpdateFlagAsync(
    string name, EnvironmentType environment,
    UpdateFlagRequest request, CancellationToken ct = default);

Task ArchiveFlagAsync(
    string name, EnvironmentType environment, CancellationToken ct = default);
```

`IsEnabledAsync` signature is unchanged.

---

### 2. `FeatureFlagService` — update implementation

**`CreateFlagAsync`:**
- Accept `CreateFlagRequest` instead of `Flag`
- Construct `Flag` entity internally from the request
- Return `flag.ToResponse()`

**`GetFlagAsync`:**
- Return `flag.ToResponse()` instead of `flag`

**`GetAllFlagsAsync`:**
- Return `flags.Select(f => f.ToResponse()).ToList()`

**`UpdateFlagAsync`:**
- Accept `UpdateFlagRequest` instead of 5 primitives
- Call `flag.Update(request.IsEnabled, request.StrategyType, request.StrategyConfig)`

---

### 3. `FeatureFlagsController` — simplify

- Remove all `.ToResponse()` calls — service now handles mapping
- `Create`: pass `request` directly to `_service.CreateFlagAsync(request, ct)`
- `Update`: pass `request` directly to `_service.UpdateFlagAsync(name, environment, request, ct)`
- `GetAll` and `GetByName`: return service result directly — already a DTO

---

### 4. `FlagMappings.cs` — no changes needed

`ToResponse()` extension method stays. It is now called from inside
`FeatureFlagService` only, not from controllers.

---

## What Does NOT Change

- `Flag` domain entity — no changes
- `FlagResponse`, `CreateFlagRequest`, `UpdateFlagRequest` DTOs — no changes
- `EvaluationController` — no changes
- All strategy and evaluator logic — no changes
- Database, EF Core config, migrations — no changes

---

## Acceptance Criteria

- [ ] `IFeatureFlagService` has no `Flag` type in any method signature
- [ ] `FeatureFlagsController` contains zero `.ToResponse()` calls
- [ ] `FeatureFlagService.CreateFlagAsync` constructs `Flag` internally
- [ ] `FeatureFlagService.UpdateFlagAsync` accepts `UpdateFlagRequest`, not primitives
- [ ] Build passes: `dotnet build` — 0 errors, 0 warnings
- [ ] All 8 existing tests still pass: `dotnet test`
- [ ] Manual smoke test: POST `/api/flags`, GET `/api/flags`, PUT, DELETE all return correct responses