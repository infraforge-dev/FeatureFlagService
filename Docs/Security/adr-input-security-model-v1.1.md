# ADR: Input Security Model — v1.1

**Status:** Accepted — Phase 1 scope
**Date:** 2026-03-28
**Supersedes:** [v1.0](adr-input-security-model-v1.0.md)
**Supersedes because:** FluentValidation v12 removed `.Transform()` and the
`FluentValidation.AspNetCore` package was deprecated. The sanitization and
auto-validation wiring described in v1.0 Mitigations 1 and 2 was updated in
PR #30 to use manual `ValidateAsync()` in controllers and `Must()` lambdas for
sanitization-aware rules. All other mitigations, threat actors, and deferred
decisions are unchanged from v1.0.
**Related PR:** `Docs/Decisions/fluent-validation - PR#30/`
[Pull Request #XX](https://github.com/amodelandme/Bandera/pull/xx)

---

## Table of Contents

- [Context](#context)
- [What Changed from v1.0](#what-changed-from-v10)
- [Assets We Are Protecting](#assets-we-are-protecting)
- [Threat Actors](#threat-actors)
- [Attack Surface — Endpoint Analysis](#attack-surface--endpoint-analysis)
- [Mitigations In Place — Phase 1](#mitigations-in-place--phase-1)
- [Consciously Deferred Decisions](#consciously-deferred-decisions)
- [Known Issues](#known-issues)
- [Consequences of This Decision](#consequences-of-this-decision)

---

## Context

Bandera exposes a REST API that accepts untrusted input across four
endpoints. As of Phase 1, the API has no authentication layer — it is assumed to
run in a controlled environment (local dev, internal network) rather than open
internet. The API is designed to become publicly accessible in Phase 3, and an
AI analysis endpoint (Phase 1.5) will embed stored flag data into AI model
prompts, introducing a qualitatively different threat model from standard HTTP
input handling.

This ADR documents the threat model, the mitigations applied in Phase 1 as
actually implemented, and the security decisions deferred to later phases.

---

## What Changed from v1.0

| Area | v1.0 (Designed) | v1.1 (Implemented) | Reason |
|---|---|---|---|
| Validation wiring | `AddFluentValidationAutoValidation()` via `FluentValidation.AspNetCore` | Manual `ValidateAsync()` in controllers | `FluentValidation.AspNetCore` deprecated |
| Sanitization mechanism | `.Transform()` in validators | `Must()` lambdas calling `InputSanitizer.Clean()` | `.Transform()` removed in FluentValidation v12 |
| Sanitization scope | Validator only (via `.Transform()`) | Validator + service layer (two-point) | `.Transform()` doesn't mutate DTO; service must sanitize independently |
| Validator registration | `AddValidatorsFromAssemblyContaining` | Explicit `AddScoped<IValidator<T>, TValidator>()` | `DependencyInjectionExtensions` is a separate package not bundled in core |

### Why Two-Point Sanitization

FluentValidation v12 validators validate the cleaned value via `Must()` lambdas
but do not mutate the DTO — the service layer receives the original unsanitized
value. Without service-layer sanitization, `" Admin "` would pass validation after
being cleaned to `"Admin"` in the `Must()` check, but `RoleStrategy` would receive
`" Admin "` and the `HashSet` lookup would silently fail, denying a legitimate user.

`InputSanitizer` is a shared `internal static` helper in
`Bandera.Application/Validators/`. Validators call it inside `Must()` lambdas.
`Bandera` calls it directly before evaluation. Same function, two call
sites, one source of truth.

---

## Assets We Are Protecting

| Asset | Why It Matters |
|---|---|
| **Flag configuration** | Flags control feature availability — corruption could disable features or grant unintended access |
| **Evaluation correctness** | Evaluation results drive runtime behavior — manipulated inputs could route wrong users to wrong features |
| **StrategyConfig integrity** | Free-form JSON deserialized and executed by strategy classes — highest-risk input in the system |
| **Application logs** | Contain flag names, user IDs, evaluation decisions — log injection corrupts audit trails |
| **AI prompts (Phase 1.5)** | Flag data embedded into AI model prompts — stored malicious content could hijack model instructions |

---

## Threat Actors

Ranked by likelihood, from most to least probable:

### 1. Misconfigured or Malicious SDK Clients — *Highest Likelihood*
Primary consumers calling `/api/evaluate` at high frequency. Misconfigured clients
are the most probable source of bad data. Malicious clients could send oversized
payloads or craft `StrategyConfig` values designed to survive storage and cause harm
when processed.

**Primary concern:** Malformed input, oversized payloads, StrategyConfig injection.

### 2. Automated Scanners and Bots — *High Likelihood*
Any endpoint exposed beyond localhost attracts automated scanners within hours.
Without authentication, every endpoint is a valid target for enumeration.

**Primary concern:** Injection probing, endpoint enumeration, payload fuzzing.

### 3. Curious or Probing External Developers — *Medium Likelihood*
Developers who discover the API will explore it manually. Some will probe for
misconfigured permissions or information leakage in error responses.

**Primary concern:** Verbose error messages leaking internal structure.

### 4. Insider Threats (Developers with API Access) — *Lower Likelihood, Higher Impact*
No authentication and no audit logging means any developer with network access
can mutate any flag with no record. Lowest probability, highest impact.

**Primary concern:** Unaudited flag mutations, no identity attached to changes.

---

## Attack Surface — Endpoint Analysis

### `POST /api/flags` — CreateFlagRequest

| Field | Risk | Mitigation |
|---|---|---|
| `Name` | Path traversal, log injection | Regex allowlist via `Must(InputSanitizer.Clean())`, max 100 chars |
| `Environment` | Sentinel bypass (`None = 0`) | `NotEqual(EnvironmentType.None)` |
| `StrategyConfig` | JSON injection, oversized payload | `Must()` structure validation, max 2000 chars |
| `StrategyType` | Invalid enum value | `.IsInEnum()` |

### `PUT /api/flags/{name}` — UpdateFlagRequest + route params

| Field | Risk | Mitigation |
|---|---|---|
| `{name}` route param | Path traversal, unexpected characters | **Partially mitigated** — creation enforces allowlist; route param has no independent guard. KI-008. |
| `StrategyConfig` | Same as above | Same as above |

### `POST /api/evaluate` — EvaluationRequest

| Field | Risk | Mitigation |
|---|---|---|
| `UserId` | Log injection, oversized value | Max 256 chars, `InputSanitizer.Clean()` in service layer |
| `UserRoles` | Oversized array, injection via role strings | Max 50 entries, max 100 chars per role via `Must()` |
| `FlagName` | Same as `Name` | `NotEmpty()`, max 100 chars |
| `Environment` | Sentinel bypass | `NotEqual(EnvironmentType.None)` |

### `GET /api/flags/{name}` — Route parameter only

| Field | Risk | Mitigation |
|---|---|---|
| `{name}` route param | Unexpected characters | **Not validated.** KI-008. EF Core prevents SQL injection. |

---

## Mitigations In Place — Phase 1

### 1. Input Validation (FluentValidation v12 — Manual)
All three request DTOs have `AbstractValidator<T>` implementations registered
as explicit `AddScoped<IValidator<T>, TValidator>()` in `DependencyInjection.cs`.
Controllers inject `IValidator<T>` and call `ValidateAsync()` manually at the top
of each mutating action (POST, PUT). Invalid requests return `400` before any
service code executes. `FluentValidation.AspNetCore` is not used — it is
deprecated.

Rules enforced:
- Required field checks (`NotEmpty`, `NotNull`)
- Maximum length bounds on all string inputs
- Maximum count bounds on collection inputs
- Regex allowlist on `Name` (applied to cleaned value via `Must()`)
- `EnvironmentType.None` sentinel rejection
- Cross-field `StrategyConfig` validation keyed on `StrategyType`

### 2. Input Sanitization — Two-Point Pattern
`InputSanitizer` is an `internal static` class in `Bandera.Application/Validators/`.
It trims whitespace and strips ASCII control characters (below 0x20, except tab).

**Point 1 — Validators:** `Must()` lambdas call `InputSanitizer.Clean()` for rules
where sanitization changes the outcome (e.g. regex checks on `Name`). Structural
rules (`NotEmpty`, `MaximumLength`) run on the raw value — whitespace-only strings
fail `NotEmpty`, and oversized strings with spaces are still too long after trimming.

**Point 2 — Service layer:** `BanderaService.IsEnabledAsync` rebuilds
`FeatureEvaluationContext` with sanitized `UserId` and `UserRoles` before passing
to the evaluator. `CreateFlagAsync` sanitizes `Name` before constructing the `Flag`
entity. This ensures consistent SHA256 hashing in `PercentageStrategy` and HashSet
lookups in `RoleStrategy`.

**Scope:** HTTP boundary only. Not a substitute for prompt injection defense
(Phase 1.5: `IPromptSanitizer`) or structured logging conventions (Phase 4).
Any non-HTTP input surface (CLI, seeds, migrations) must call `InputSanitizer`
independently.

### 3. SQL Injection Prevention (EF Core)
All database access uses EF Core parameterized queries. `FromSqlRaw()` with string
concatenation is prohibited. `FromSqlInterpolated()` must be used if raw SQL
becomes necessary.

### 4. Mass Assignment Prevention (Record Types)
All request DTOs are `sealed record` types. The deserializer only maps declared
properties — extra fields are silently ignored.

### 5. Payload Size Limits
- `StrategyConfig`: maximum 2,000 characters
- `UserId`: maximum 256 characters
- `UserRoles`: maximum 50 entries, 100 characters per role
- `Name`: maximum 100 characters
- ASP.NET Core default body limit (28.6MB) as backstop

### 6. Structured Error Responses
`ValidationProblemDetails` (RFC 9110 compliant) for all `400` responses. No
stack traces, connection strings, or entity names in HTTP responses.

---

## Consciously Deferred Decisions

### DEFERRED-001: Authentication and Authorization
**Deferred to:** Phase 3
**Rationale:** Identity provider decision depends on deployment target (Phase 1.5).
**Phase 3 plan:** JWT bearer auth on management endpoints; evaluation endpoint
optionally anonymous depending on SDK requirements.

---

### DEFERRED-002: Rate Limiting
**Deferred to:** Phase 3
**Rationale:** Meaningful rate limits require caller identity. IP-based limiting is
easily bypassed and creates operational pain for NAT'd clients.
**Phase 3 plan:** `AddRateLimiter` on `/api/evaluate` keyed on authenticated identity.

---

### DEFERRED-003: Audit Logging
**Deferred to:** Phase 4
**Rationale:** Requires identity from Phase 3.
**Phase 4 plan:** Structured audit events on all flag mutations. Azure Application
Insights custom events as the initial sink.

---

### DEFERRED-004: Prompt Injection Defense (IPromptSanitizer)
**Deferred to:** Phase 1.5
**Rationale:** Only relevant when flag data is embedded in AI prompts. Building
`IPromptSanitizer` before the prompt template is designed would produce the wrong
abstraction. `InputSanitizer` (HTTP boundary) is a complementary first layer, not
a substitute.
**Phase 1.5 plan:** `IPromptSanitizer` introduced alongside `IAiFlagAnalyzer`,
targeting newline injection, instruction override patterns, and role confusion.

---

### DEFERRED-005: Route Parameter Validation on GET and PUT
**Deferred to:** Phase 1 (KI-008)
**Rationale:** EF Core prevents SQL injection. Risk is log noise only.
**Fix:** `RouteParameterGuard.ValidateName()` helper at top of affected controller
actions.

---

## Known Issues

### KI-008 — Route Parameters Lack Allowlist Validation
**Severity:** Low | **Status:** Open — Phase 1
`{name}` route param on GET and PUT has no character allowlist. EF Core prevents
SQL injection. Risk is unexpected characters reaching logs.
**Planned fix:** Static `RouteParameterGuard.ValidateName(string name)` returning
`400` for non-conforming values.

### KI-NEW-001 — `BeValidPercentageConfig` / `BeValidRoleConfig` Duplicated
**Severity:** Low — code quality, no runtime impact | **Status:** Deferred
Identical private static methods in both `CreateFlagRequestValidator` and
`UpdateFlagRequestValidator`.
**Candidate fix:** Extract to `StrategyConfigRules` internal static class.

---

## Consequences of This Decision

**What this creates:**
- A documented, accurate threat model reflecting the actual implementation
- Clear record of what changed from the original design and why
- Foundation for Phase 1.5 prompt injection threat model
- Interview-ready answers grounded in specific, real decisions

**What this does not create:**
- Authentication or authorization of any kind
- Protection against a malicious actor with direct database access
- False confidence — deferred items represent real, open risk

**What future engineers must know:**
- `InputSanitizer` is `internal` to `Bandera.Application` — the Api project
  cannot call it directly; sanitization in the service layer is the correct pattern
- Any non-HTTP input surface must call `InputSanitizer` independently
- Any new `IRolloutStrategy` requires a corresponding validator rule before the
  API will accept its configuration
- When Phase 1.5 introduces the AI endpoint, a new ADR must document the prompt
  injection threat model specifically

---

*Bandera | Security ADR v1.1 — current*
