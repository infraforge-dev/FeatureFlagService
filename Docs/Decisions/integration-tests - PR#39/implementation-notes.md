# test/integration-tests — Implementation Notes
**Session date:** 2026-04-08
**Branch:** `test/integration-tests`
**Spec reference:** `Docs/Decisions/integration-tests - PR#39/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 106/106 passing
**PR:** —

## Table of Contents
- [Summary](#summary)
- [Implemented Scope](#implemented-scope)
- [Environment Validation Placement](#environment-validation-placement)
- [Runtime Issues Found During Integration Testing](#runtime-issues-found-during-integration-testing)
- [Verification](#verification)

## Summary
This PR added a dedicated integration test project backed by Testcontainers Postgres,
covered all six API endpoints through the full HTTP pipeline, added a new CI
`integration-test` job, and introduced the minimal production changes required to
support the tested contract.

## Implemented Scope
- Added `FeatureFlag.Tests.Integration` and registered it in `FeatureFlagService.sln`.
- Added shared test infrastructure:
  `FeatureFlagApiFactory`, `IntegrationTestCollection`, and `IntegrationTestBase`.
- Implemented 24 flag-endpoint integration tests and 7 evaluation-endpoint
  integration tests.
- Added `public partial class Program { }` for `WebApplicationFactory<Program>`.
- Updated `.github/workflows/ci.yml` with the `integration-test` job and wired
  `ai-review` to depend on it.

## Environment Validation Placement
During implementation, we revisited where `EnvironmentType` validation should live.

The spec revision originally allowed a narrow controller-layer fix in
`FeatureFlagsController`, but we chose a stronger Application-layer design instead:

- Added `FeatureFlag.Application/Validation/EnvironmentRules.cs`
- Reused `EnvironmentRules.IsValid(...)` in validators
- Enforced `EnvironmentRules.RequireValid(...)` in `FeatureFlagService`
- Removed controller-local `ValidateEnvironment(...)` duplication

Why this was better:
- The rule now lives with the rest of request/use-case validation in the
  Application layer.
- Query-driven endpoints and non-HTTP callers are protected by the service
  boundary, not just controllers.
- Validators and service enforcement now share one source of truth and one
  message string.

This was kept in the same PR because the integration-test spec directly depends on
invalid-environment behavior across multiple endpoints, so splitting it into a
separate PR would have added coordination overhead without reducing risk.

## Runtime Issues Found During Integration Testing
The first end-to-end integration run exposed two production-path issues that were
not visible from the unit suite alone:

1. `strategyConfig: null` was being rejected at model binding time for
   `CreateFlagRequest` and `UpdateFlagRequest` because the DTOs exposed
   `StrategyConfig` as non-nullable `string`.
2. Successful `POST /api/flags` requests were failing with `500` because
   `CreatedAtAction(...)` could not generate a matching route for the supplied
   values in this controller shape.

Fixes applied:
- Updated `CreateFlagRequest` and `UpdateFlagRequest` to use `string? StrategyConfig`
- Updated `Flag` mutation signatures to accept nullable config and preserve the
  existing `?? "{}"` normalization behavior
- Added a named route to `GetByNameAsync` and switched create responses to
  `CreatedAtRoute(nameof(GetByNameAsync), ...)`, removing the need to hard-code
  the location URL while keeping the `Location` header stable under integration tests

These fixes were necessary to match the HTTP contract exercised by the new
integration suite.

## Verification
The implementation was verified with:

```bash
dotnet build FeatureFlagService.sln -p:TreatWarningsAsErrors=true
dotnet test FeatureFlagService.sln --filter "Category=Integration" --logger "console;verbosity=normal"
dotnet test FeatureFlagService.sln --filter "Category!=Integration" --logger "console;verbosity=normal"
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
```

Final result:
- Build: passed with 0 warnings / 0 errors
- Unit tests: 75/75 passing
- Integration tests: 31/31 passing
- Total: 106/106 passing
