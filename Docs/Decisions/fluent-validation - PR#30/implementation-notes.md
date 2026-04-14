# FluentValidation — Implementation Notes

**Session date:** 2026-03-28
**Branch:** `feature/fluent-validation-dtos`
**Spec reference:** `Docs/Decisions/fluent-validation/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 8/8 passing
**PR:** —

---

## What Was Implemented

- `InputSanitizer` — internal static helper in `Banderas.Application/Validators/`
- `CreateFlagRequestValidator` — Name regex + length, env sentinel, StrategyConfig cross-field rules
- `UpdateFlagRequestValidator` — StrategyConfig cross-field rules
- `EvaluationRequestValidator` — FlagName/UserId length + empty, UserRoles null/count/per-role length
- `DependencyInjection.cs` — explicit `IValidator<T>` registrations
- `BanderasService.IsEnabledAsync` — sanitizes `UserId` and `UserRoles` before building context
- `BanderasService.CreateFlagAsync` — sanitizes `Name` before constructing `Flag`
- `BanderasController` — manual `ValidateAsync` on POST and PUT
- `EvaluationController` — manual `ValidateAsync` before `FeatureEvaluationContext` is constructed

---

## Deviations from Spec

### DEV-001 — `AddValidatorsFromAssemblyContaining` Not Used

**Reason:** `AddValidatorsFromAssemblyContaining` lives in
`FluentValidation.DependencyInjectionExtensions` — a separate NuGet package not bundled
in the core `FluentValidation` package. The spec was incorrect on this point.

**Fix:** Explicit `AddScoped<IValidator<T>, TValidator>()` registrations in
`DependencyInjection.cs`.

**Impact:** Zero behavior change. More transparent — a new engineer reading
`DependencyInjection.cs` sees exactly what is registered without chasing an assembly
scan. Assembly scanning earns its complexity at dozens of validators, not three.

**Spec update required:** Replace `AddValidatorsFromAssemblyContaining` with explicit
registrations. Remove the claim that DI extensions are bundled in core.

---

### DEV-002 — RuleFor Sanitization Pattern Changed

**Reason:** Calling `RuleFor(x => InputSanitizer.Clean(x.Name)).OverridePropertyName()`
hits a type inference limitation in FluentValidation v12. When the lambda passed to
`RuleFor` returns a value via a static method call, the compiler cannot always infer
`TProperty` for the extension method chain.

**Fix:** Validate structural constraints on the raw property; run sanitized value through
`Must()` for the regex check:

```csharp
RuleFor(x => x.Name)
    .NotEmpty().WithMessage("Flag name is required.")
    .MaximumLength(100).WithMessage("Flag name must not exceed 100 characters.")
    .Must(name => Regex.IsMatch(
        InputSanitizer.Clean(name) ?? string.Empty,
        @"^[a-zA-Z0-9\-_]+$"))
    .WithMessage("Flag name may only contain letters, numbers, hyphens, and underscores.");
```

**Why this is semantically equivalent:**
- `NotEmpty` runs on the raw value — `" "` (whitespace-only) is treated as empty by
  FluentValidation and returns a validation failure. ✅
- `MaximumLength(100)` runs on the raw value — a 101-character string with spaces is
  still too long after trimming. ✅
- The regex `Must()` runs the sanitized value through the check — the only rule where
  sanitization actually changes the outcome. `" dark-mode "` → cleaned to `"dark-mode"`
  → passes regex. ✅

**Pattern to follow:** Any future validator that needs sanitization-aware rules uses this
same `Must()` pattern — validate structural constraints raw, run cleaned value through
`Must()` for rules where sanitization changes the outcome.

**Spec update required:** Document this as the v12 approach. Remove `.Transform()` and
`OverridePropertyName` from any future spec examples.

---

## Known Issues (New)

### KI-NEW-001 — `BeValidPercentageConfig` / `BeValidRoleConfig` Duplicated

`BeValidPercentageConfig` and `BeValidRoleConfig` are private static methods duplicated
identically in both `CreateFlagRequestValidator` and `UpdateFlagRequestValidator`.

**Candidate fix:** Extract to a `StrategyConfigRules` internal static class in
`Banderas.Application/Validators/`. Both validators call the shared methods.

**Deferred:** Not a Phase 1 blocker. Candidate for a small cleanup spec.

---

## Notes

- `InputSanitizer` is `internal` — accessible from `Banderas` (same project)
  but not from the Api project. This is intentional.
- `StrategyConfig` is intentionally not sanitized — it is JSON, stored verbatim; only
  its length and internal structure are validated.
- `FluentValidation.AspNetCore` was not added — it is deprecated. Manual `ValidateAsync`
  in controllers is the correct v12 approach.
- Architecture.md references auto-validation and `.Transform()` — both are now outdated.
  Update architecture.md to reflect the v12 manual validation approach. *(Deferred to
  end of session.)*
