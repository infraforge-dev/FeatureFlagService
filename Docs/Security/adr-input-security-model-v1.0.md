# ADR: Input Security Model — v1.0

**Status:** Superseded by [v1.1](adr-input-security-model-v1.1.md)
**Superseded because:** FluentValidation v12 removed `.Transform()` and deprecated
`FluentValidation.AspNetCore`. Mitigations 1 and 2 below describe the originally
designed approach. The actual implementation (PR #30) uses manual `ValidateAsync()`
in controllers and `Must()` lambdas for sanitization-aware rules. See v1.1 for
corrected mitigations.
**Original date:** 2026-03-26
**Branch:** `feature/fluent-validation-dtos`
**Supersedes:** Nothing
**Superseded by:** `adr-input-security-model-v1.1.md`
**Related specs:** `Docs/Decisions/fluent-validation - PR#30/spec.md`

---

## Table of Contents

- [Context](#context)
- [Assets We Are Protecting](#assets-we-are-protecting)
- [Threat Actors](#threat-actors)
- [Attack Surface — Endpoint Analysis](#attack-surface--endpoint-analysis)
- [Mitigations In Place — Phase 1](#mitigations-in-place--phase-1)
- [Consciously Deferred Decisions](#consciously-deferred-decisions)
- [New Known Issue Created by This ADR](#new-known-issue-created-by-this-adr)
- [Consequences of This Decision](#consequences-of-this-decision)

---

## Context

Bandera exposes a REST API that accepts untrusted input across four
endpoints. As of Phase 1, the API has no authentication layer — it is assumed to
run in a controlled environment (local dev, internal network) rather than open
internet. However, the API is designed to become publicly accessible in Phase 3,
and an AI analysis endpoint (Phase 1.5) will embed stored flag data into AI model
prompts, which introduces a qualitatively different threat model from standard
HTTP input handling.

This ADR documents the threat model, the mitigations applied in Phase 1, and the
security decisions deferred to later phases with explicit rationale.

---

## Assets We Are Protecting

| Asset | Why It Matters |
|---|---|
| **Flag configuration** | Flags control feature availability — a corrupted flag could disable critical features or grant unintended access |
| **Evaluation correctness** | Evaluation results drive runtime behavior — manipulated inputs could cause wrong users to receive wrong features |
| **StrategyConfig integrity** | Free-form JSON that is deserialized and executed by strategy classes — the highest-risk input in the system |
| **Application logs** | Will contain flag names, user IDs, and evaluation decisions — log injection corrupts audit trails |
| **AI prompts (Phase 1.5)** | Flag data will be embedded into prompts sent to AI models — stored malicious content could hijack model instructions |

---

## Threat Actors

Ranked by likelihood for the current deployment context, from most to least probable:

### 1. Misconfigured or Malicious SDK Clients — *Highest Likelihood*

The primary consumers of this API will be application SDKs and internal services
calling `/api/evaluate` at high frequency. Misconfigured clients are the most
probable source of bad data — malformed JSON, missing fields, unexpected enum
values, null payloads. A malicious SDK client could deliberately send oversized
payloads to exhaust server resources or craft `StrategyConfig` values designed
to survive storage and cause harm when later processed.

**Primary concern:** Malformed input, oversized payloads, StrategyConfig injection.

---

### 2. Automated Scanners and Bots — *High Likelihood*

Any HTTP endpoint exposed beyond localhost will encounter automated scanners
within hours. These tools probe for common vulnerabilities: SQL injection strings,
path traversal sequences, known CVE payloads, oversized inputs. Without
authentication, every endpoint is a valid target for enumeration.

**Primary concern:** Injection probing, endpoint enumeration, payload fuzzing.

---

### 3. Curious or Probing External Developers — *Medium Likelihood*

Developers who discover the API (via docs, source code, or network traffic) will
explore it manually. Most are benign — testing boundaries, understanding behavior.
Some will probe intentionally for misconfigured permissions or information leakage
in error responses.

**Primary concern:** Verbose error messages leaking internal structure, unintended
access to flag data across environments.

---

### 4. Insider Threats (Developers with API Access) — *Lower Likelihood, Higher Impact*

In the absence of authentication and audit logging, any developer with network
access can read, modify, or archive any flag in any environment. There is no
record of who made what change. This is the lowest-probability threat but the
highest-impact one — a developer (malicious or simply mistaken) could archive
a Production flag with no recoverable audit trail.

**Primary concern:** Unaudited flag mutations, no identity attached to changes,
no separation between Development and Production access.

---

## Attack Surface — Endpoint Analysis

### `POST /api/flags` — CreateFlagRequest

| Field | Risk | Mitigation |
|---|---|---|
| `Name` | Path traversal, log injection, Unicode abuse | Regex allowlist `^[a-zA-Z0-9\-_]+$`, max 100 chars, `.Transform()` trim |
| `Environment` | Sentinel bypass (`None = 0`) | Enum guard — `None` rejected explicitly |
| `StrategyConfig` | JSON injection, oversized payload, prompt injection (Phase 1.5) | JSON structure validation via `Must()`, max 2000 chars, Phase 1.5: `IPromptSanitizer` |
| `StrategyType` | Invalid enum value | `.IsInEnum()` validation |

### `PUT /api/flags/{name}` — UpdateFlagRequest + route params

| Field | Risk | Mitigation |
|---|---|---|
| `{name}` route param | Path traversal, unexpected characters | **Partially mitigated** — creation enforces allowlist, route param has no independent guard. Tracked as KI-008. |
| `StrategyConfig` | Same as above | Same as above |

### `POST /api/evaluate` — EvaluationRequest

| Field | Risk | Mitigation |
|---|---|---|
| `UserId` | Log injection, oversized value | Max 256 chars, `.Transform()` trim, structured logging |
| `UserRoles` | Oversized array, injection via role strings | Max 50 entries, max 100 chars per role, `.Transform()` trim |
| `FlagName` | Same as `Name` above | `NotEmpty()`, max length |
| `Environment` | Sentinel bypass | Same as above |

### `GET /api/flags/{name}` — Route parameter only

| Field | Risk | Mitigation |
|---|---|---|
| `{name}` route param | Path traversal, unexpected characters | **Not currently validated.** Tracked as KI-008. EF Core parameterized query prevents SQL injection. |

---

## Mitigations In Place — Phase 1

> ⚠️ **Note:** Mitigations 1 and 2 below describe the originally designed approach.
> The actual implementation differed due to FluentValidation v12 breaking changes.
> See `adr-input-security-model-v1.1.md` for the corrected mitigations as implemented.

### 1. Input Validation (FluentValidation) — *As Designed*
All three request DTOs have `AbstractValidator<T>` implementations with:
- Required field enforcement
- Maximum length bounds on all string inputs
- Maximum count bounds on collection inputs
- Regex allowlist on `Name` fields
- `EnvironmentType.None` sentinel rejection
- Cross-field `StrategyConfig` validation keyed on `StrategyType`
- Auto-validation wired via `AddFluentValidationAutoValidation()` — invalid
  requests are rejected before any application logic executes

### 2. Input Sanitization (`.Transform()`) — *As Designed, Not Implemented*
FluentValidation `.Transform()` applied to all string inputs before validation
rules execute:
- Whitespace trimming on `Name`, `UserId`, role strings, `FlagName`
- Control character stripping (characters below ASCII 0x20 except tab)
- Applied at the HTTP boundary — domain layer never receives unsanitized input

### 3. SQL Injection Prevention (EF Core)
All database access uses EF Core with parameterized queries. String inputs are
never interpolated into raw SQL. Raw SQL via `FromSqlRaw()` with string
concatenation is prohibited by architectural convention. If raw SQL becomes
necessary in future, `FromSqlInterpolated()` must be used exclusively.

### 4. Mass Assignment Prevention (Record Types)
All request DTOs are `sealed record` types with explicit constructor parameters.
The JSON deserializer only maps declared properties — extra fields in the request
body are silently ignored.

### 5. Payload Size Limits
- `StrategyConfig`: maximum 2,000 characters
- `UserId`: maximum 256 characters
- `UserRoles`: maximum 50 entries, 100 characters per role
- `Name`: maximum 100 characters
- ASP.NET Core default request body limit (28.6MB) remains as a backstop

### 6. Structured Error Responses
Validation failures return `ValidationProblemDetails` (RFC 9110 compliant).
Internal exception details are never surfaced in HTTP responses.

---

## Consciously Deferred Decisions

### DEFERRED-001: Authentication and Authorization
**Deferred to:** Phase 3
**Rationale:** Depends on deployment target decided in Phase 1.5 (Azure).

### DEFERRED-002: Rate Limiting
**Deferred to:** Phase 3
**Rationale:** Meaningful rate limits require caller identity.

### DEFERRED-003: Audit Logging
**Deferred to:** Phase 4
**Rationale:** Requires identity from Phase 3.

### DEFERRED-004: Prompt Injection Defense (IPromptSanitizer)
**Deferred to:** Phase 1.5
**Rationale:** Only relevant when flag data is embedded in AI model prompts.

### DEFERRED-005: Route Parameter Validation on GET and PUT
**Deferred to:** Phase 1 (tracked as KI-008)
**Rationale:** Minor gap — EF Core prevents SQL injection; log noise only.

---

## New Known Issue Created by This ADR

### KI-008 — Route Parameters Lack Allowlist Validation
**Severity:** Low | **Status:** Open — Phase 1
Route parameter `{name}` on GET and PUT has no character allowlist validation.
EF Core prevents SQL injection. Risk is unexpected characters in logs.
**Planned fix:** `RouteParameterGuard.ValidateName()` helper at top of affected
controller actions.

---

## Consequences of This Decision

- Documented threat model referenced in code reviews and onboarding
- Clear accountability for what is and is not protected at each phase
- Foundation for Phase 1.5 prompt injection threat model
- The deferred items represent real, open risk — this document does not create
  false confidence

---

*Bandera | Security ADR v1.0 — superseded by v1.1*
