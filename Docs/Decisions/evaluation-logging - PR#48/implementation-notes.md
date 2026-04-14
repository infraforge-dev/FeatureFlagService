# feature/evaluation-logging — Implementation Notes
**Session date:** 2026-04-09
**Branch:** `feature/evaluation-logging`
**Spec reference:** `Docs/Decisions/evaluation-logging - PR#48/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 110/110 passing
**PR:** 48

## Table of Contents
- [Summary](#summary)
- [Implemented Scope](#implemented-scope)
- [Implementation Notes](#implementation-notes)
- [Spec Deviations](#spec-deviations)
- [Verification](#verification)

## Summary
This PR added structured evaluation decision logging to `Bandera`,
modeled evaluation outcomes as a discriminated union in
`Bandera.Application/Evaluation/`, and added focused unit tests covering the
new logging behavior without changing the evaluator, controller, or existing test
files.

## Implemented Scope
- Added `Bandera.Application/Evaluation/EvaluationResult.cs` with:
  `EvaluationReason`, `EvaluationResult`, `FlagDisabled`, and
  `StrategyEvaluated`.
- Updated `Bandera.Application/Services/BanderaService.cs` to:
  accept `ILogger<BanderaService>`, log a warning before
  `FlagNotFoundException`, construct `EvaluationResult` instances, and emit
  structured completion logs.
- Added `HashUserId(...)` to keep raw `UserId` values out of logs while still
  allowing deterministic correlation across evaluations.
- Added `Bandera.Tests/Services/BanderaServiceLoggingTests.cs` with four
  unit tests covering disabled-flag logging, strategy-evaluated logging, hashed
  user IDs, and the not-found warning path.
- Added `Microsoft.Extensions.Diagnostics.Testing` to
  `Bandera.Tests/Bandera.Tests.csproj` for `FakeLogger<T>`.

## Implementation Notes
The final design stayed aligned with the spec's intended architecture:

- `FeatureEvaluator` remains pure and unchanged.
- `Bandera` remains the imperative shell and owns all logging.
- The service still preserves the existing sanitization block before evaluation.
- The log shape differs by outcome branch while sharing the common
  `"Flag evaluation complete."` prefix for successful evaluation outcomes.

The new tests verify the structured logging contract directly through
`FakeLogRecord.GetStructuredStateValue(...)` rather than relying only on rendered
message text. This makes the assertions stricter for fields like `Reason`,
`StrategyType`, and hashed `UserId`.

## Spec Deviations
Two small implementation deviations were made during coding:

1. `Bandera.LogResult(...)` now returns early when
   `_logger.IsEnabled(LogLevel.Information)` is false. This was added to satisfy
   analyzer rule `CA1873` and to avoid unnecessary SHA256 hashing work when info
   logging is disabled. It does not change behavior when info logging is enabled.
2. The new logging tests assert structured state via
   `FakeLogRecord.GetStructuredStateValue(...)` for key fields instead of relying
   only on rendered message substrings. This is slightly stronger than the spec's
   example assertions and better verifies the structured telemetry contract.

## Verification
The implementation was verified with:

```bash
dotnet csharpier format .
dotnet build Bandera.sln
dotnet test Bandera.sln --filter "Category=Unit"
dotnet test Bandera.sln --filter "Category=Integration"
dotnet csharpier check .
```

Final result:
- Build: passed with 0 warnings / 0 errors
- Unit tests: 79/79 passing
- Integration tests: 31/31 passing
- Total: 110/110 passing
