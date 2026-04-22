# Architecture Review — Phase 1 / 1.5 Audit Report

---

## 1. Executive Summary

**Overall Health:** Acceptable

**Recommended Gate Decision:** GO WITH CONDITIONS

**Why:**

- Phase 1 established strong seams around controller thinness, evaluator purity, strategy dispatch, and infrastructure abstractions.
- The current system is buildable, but two debts become materially more expensive in Phase 2: the partial DTO-boundary drift on evaluation and the hard startup dependency on Azure OpenAI configuration.
- The AI path is reasonably isolated, but its operational behavior and output contract are not yet validated strongly enough to call the platform phase-clean.

---

## 2. Phase Intent vs Actual Outcome

| Intended | Observed | Assessment |
|---|---|---|
| Thin controllers | CRUD controllers stay thin, but `EvaluationController` constructs `FeatureEvaluationContext` directly before calling the service. | Partial drift |
| DTO-only service boundary | `IBanderasService` keeps `Flag` out of signatures, but `IsEnabledAsync` accepts domain value object `FeatureEvaluationContext`. | Drifted |
| Strategy-based extensibility | `FeatureEvaluator` dispatches by registry and does not require modification for existing strategies. | Aligned |
| Validation at HTTP boundary | Request DTOs are validated at the controller boundary, but GET query `EnvironmentType` validation happens in `BanderasService`, not before service entry. | Partial drift |
| Fail-closed evaluation behavior | Missing strategy registrations, malformed JSON, invalid configs, and empty role config all resolve false. | Aligned |
| AI behind application abstractions | `IAiFlagAnalyzer` and `IPromptSanitizer` keep Semantic Kernel and Azure SDK types out of controllers and core evaluation logic. | Aligned |
| AI integration should degrade gracefully | Endpoint failures map to 503, but missing `AzureOpenAI:Endpoint` crashes non-testing startup before any endpoint is reachable. | Drifted |

---

## 3. Strong Seams and What Phase 1 Established Well

### Strength: Evaluator and Strategy Isolation

**Why it is strong:**

`FeatureEvaluator` remains a narrow dispatcher with no persistence or telemetry concerns. The strategies stay stateless and independently testable.

**Evidence:**

- `Banderas.Application/Evaluation/FeatureEvaluator.cs`
- `Banderas.Application/Strategies/NoneStrategy.cs`
- `Banderas.Application/Strategies/PercentageStrategy.cs`
- `Banderas.Application/Strategies/RoleStrategy.cs`

**Why it should be preserved:**

This is the cleanest seam in the system and the one most likely to support later rollout strategies without churn.

### Strength: Infrastructure Concerns Stay Behind Interfaces

**Why it is strong:**

Telemetry and AI analysis are expressed as application-layer interfaces and implemented in infrastructure. `BanderasService` does not depend on Application Insights, Semantic Kernel, or Azure SDK types.

**Evidence:**

- `Banderas.Application/Telemetry/ITelemetryService.cs`
- `Banderas.Application/AI/IAiFlagAnalyzer.cs`
- `Banderas.Infrastructure/Telemetry/ApplicationInsightsTelemetryService.cs`
- `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs`

**Why it should be preserved:**

This is the right dependency direction for future observability and AI changes.

### Strength: Validation and Route Guarding Are Explicit

**Why it is strong:**

The mutating endpoints call validators manually and the route-name guard makes the route hardening behavior obvious in code instead of relying on hidden framework magic.

**Evidence:**

- `Banderas.Api/Controllers/BanderasController.cs`
- `Banderas.Api/Controllers/EvaluationController.cs`
- `Banderas.Api/Helpers/RouteParameterGuard.cs`

**Why it should be preserved:**

The explicitness helps both reviewability and future debugging.

### Strength: Test Isolation for Azure Dependencies Is Well Designed

**Why it is strong:**

The integration factory forces `Testing`, swaps the DbContext, and injects a stub analyzer so CI never attempts live Azure access.

**Evidence:**

- `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs`

**Why it should be preserved:**

This is a strong seam for CI reliability and a prerequisite for evolving the AI surface safely.

---

## 4. Architectural Findings

### Finding: The DTO-only service boundary is not actually preserved on evaluation

**Severity:** Medium  
**Type:** Fact

**Why it matters:**

The architecture documents describe `IBanderasService` as a hard DTO boundary, but the evaluation path currently requires the API layer to know about `FeatureEvaluationContext`, a domain value object. That weakens the stated controller/service contract and makes boundary rules inconsistent across use cases.

**Evidence:**

- `IBanderasService.IsEnabledAsync(string flagName, FeatureEvaluationContext context, ...)` in `Banderas.Application/Interfaces/IBanderasService.cs`
- `EvaluationController` constructs `new FeatureEvaluationContext(...)` before calling the service in `Banderas.Api/Controllers/EvaluationController.cs`
- the pre-audit architecture docs described `IBanderasService` as DTO-only; that wording has now been corrected to match the implemented exception

**Suggested remediation:**

Either:

- restore a strict DTO boundary by changing `IsEnabledAsync` to accept an application DTO, or
- formally document `FeatureEvaluationContext` as the one accepted boundary exception and stop describing the interface as DTO-only.

**Fix timing:** Now

### Finding: Domain invariants are enforced mainly by outer layers, not by `Flag`

**Severity:** Medium  
**Type:** Fact

**Why it matters:**

The architecture claims the domain should never be invalid, but `Flag` only guards `Name`. It will accept `EnvironmentType.None`, undefined enum values, or mismatched strategy/config combinations if it is created outside the HTTP validation path. That is manageable today, but it makes future non-HTTP callers and test fixtures more likely to bypass intended rules.

**Evidence:**

- `Flag` constructor checks `name` only in `Banderas.Domain/Entities/Flag.cs`
- `EnvironmentRules.RequireValid(...)` exists in application, not domain, in `Banderas.Application/Validation/EnvironmentRules.cs`
- config-shape validation exists in FluentValidation helpers, not domain, in `Banderas.Application/Validators/StrategyConfigRules.cs`

**Suggested remediation:**

Move the minimum non-negotiable invariants into `Flag` itself:

- valid environment
- valid strategy enum
- a deliberate normalization rule for `StrategyConfig`

Leave richer JSON semantics at the boundary if needed, but do not let obviously invalid entity state exist.

**Fix timing:** Next phase

### Finding: GET query validation is later than documented

**Severity:** Low  
**Type:** Fact

**Why it matters:**

The docs describe invalid requests as being rejected at the HTTP boundary before service logic runs, but GET query `EnvironmentType` currently reaches `BanderasService` first. The behavior still returns 400, but the architectural claim is stronger than the implementation.

**Evidence:**

- `BanderasController.GetAllAsync` and `GetByNameAsync` pass `environment` directly to the service in `Banderas.Api/Controllers/BanderasController.cs`
- service-level validation occurs in `EnvironmentRules.RequireValid(...)` inside `Banderas.Application/Services/BanderasService.cs`

**Suggested remediation:**

Either add explicit controller-boundary handling for query env validation or weaken the documentation claim so it matches reality.

**Fix timing:** Later

---

## 5. Implementation Quality Findings

### Finding: Azure OpenAI configuration is a hard startup dependency for all non-testing environments

**Severity:** High  
**Type:** Fact

**Why it matters:**

Missing `AzureOpenAI:Endpoint` currently crashes application startup, which means an optional analytical capability can take down unrelated CRUD and evaluation endpoints. That is the wrong blast radius for Phase 1.5.

**Evidence:**

- `Banderas.Infrastructure/DependencyInjection.cs` throws `InvalidOperationException("AzureOpenAI:Endpoint is required.")`
- `Program.cs` always calls `AddInfrastructure(...)`
- `Docs/current-state.md` already records this as KI-008, but the implementation note for PR #52 incorrectly says other endpoints remain unaffected

**Suggested remediation:**

Make AI analyzer registration lazy or feature-gated:

- register a no-op/unavailable analyzer when config is missing, or
- move the configuration check into the analyzer invocation path rather than startup

This preserves 503 behavior for `/api/flags/health` without breaking the whole app.

**Fix timing:** Now

### Finding: AI response integrity is trusted rather than enforced

**Severity:** Medium  
**Type:** Fact

**Why it matters:**

`AiFlagAnalyzer` deserializes model output into `FlagHealthAnalysisResponse` but does not verify that statuses stay in the allowed set, that every input flag is represented, or that required fields are materially valid. A vendor-side or prompt-side drift can therefore return a structurally deserializable but semantically wrong 200 response.

**Evidence:**

- `AiFlagAnalyzer.AnalyzeAsync(...)` in `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs`
- `FlagAssessment.Status` is just `string` in `Banderas.Application/DTOs/FlagAssessment.cs`
- no post-deserialization validation logic exists in application or infrastructure

**Suggested remediation:**

Add a small post-deserialization verifier:

- enforce allowed status values
- enforce non-empty summary and recommendation fields
- enforce one result per analyzed flag or fail closed to `AiAnalysisUnavailableException`

**Fix timing:** Now

---

## 6. Testing and Reliability Findings

### Finding: The AI unhappy path is not covered end-to-end

**Severity:** Medium  
**Type:** Fact

**Why it matters:**

The code claims graceful degradation to 503, but there is no end-to-end test proving that the middleware returns the documented 503 ProblemDetails shape when AI analysis fails. The current integration test setup always injects a successful stub analyzer.

**Evidence:**

- `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs` always registers `StubAiFlagAnalyzer`
- `Banderas.Tests.Integration/AnalyzeFlagsEndpointTests.cs` covers only 200 and 400 paths
- only unit-level propagation is covered in `Banderas.Tests/AI/BanderasServiceAnalysisTests.cs`

**Suggested remediation:**

Add one integration test factory path that injects a failing analyzer and assert:

- 503 status
- `application/problem+json`
- correct RFC 9110 type URI

**Fix timing:** Now

### Finding: Domain tests do not match the documented importance of domain integrity

**Severity:** Medium  
**Type:** Fact

**Why it matters:**

The project documents domain integrity as a core principle, but the direct tests on `Flag` cover only seeded provenance and default config normalization. The highest-value entity rules are not exercised close to the entity.

**Evidence:**

- `Banderas.Tests/Domain/FlagTests.cs` contains only two tests, both focused on `IsSeeded`
- no direct tests cover `Update`, `Archive`, invalid environment values, or invalid strategy values

**Suggested remediation:**

Expand `FlagTests` around the invariants that the audit depends on. If those invariants remain intentionally outside the entity, make that explicit in the docs rather than implying stronger domain protection than exists.

**Fix timing:** Next phase

### Finding: Current local verification is harder to trust than it should be

**Severity:** Low  
**Type:** Fact

**Why it matters:**

The test suite exits successfully, but this environment did not surface a clean per-test summary through the CLI capture path. That is not a product bug, but it is a workflow reliability issue for future audits and CI debugging.

**Evidence:**

- local `dotnet test` reruns during the audit exited `0`
- in-sandbox runs failed on MSBuild named-pipe permissions
- escalated runs succeeded but did not emit useful VSTest summaries through this terminal capture

**Suggested remediation:**

No production change required. For future audits, prefer CI artifacts or explicit `.trx` handling in a shell environment known to preserve test output.

**Fix timing:** Later

---

## 7. Security and Operational Findings

### Finding: The current AI startup failure mode is fail-stop, not endpoint-scoped fail-closed

**Severity:** High  
**Type:** Fact

**Why it matters:**

The security model prefers fail-closed behavior for sensitive features. For AI analysis, the healthier equivalent is endpoint-scoped unavailability. Today the service fails before boot, which is operationally harsher than the documented 503 behavior.

**Evidence:**

- startup exception in `Banderas.Infrastructure/DependencyInjection.cs`
- 503 middleware path in `Banderas.Api/Middleware/GlobalExceptionMiddleware.cs`

**Suggested remediation:**

Keep AI failures local to `/api/flags/health` by moving failure detection after startup.

**Fix timing:** Now

### Finding: The route and DTO validation story is materially stronger than the repository layer story

**Severity:** Low  
**Type:** Inference

**Why it matters:**

Repository calls are safe because EF Core parameterizes queries, but architectural confidence currently depends heavily on outer-layer validation and conventions rather than the domain model itself. That is workable for the current HTTP-only shape, but it increases risk as soon as another input surface appears.

**Evidence:**

- repository queries are LINQ-only in `Banderas.Infrastructure/Persistence/BanderasRepository.cs`
- `InputSanitizer` and validators carry most structural protection in `Banderas.Application/Validators/*`
- `Flag` itself remains permissive in `Banderas.Domain/Entities/Flag.cs`

**Suggested remediation:**

Preserve the validator and route-guard approach, but add a smaller set of domain-level non-negotiables before new ingestion surfaces arrive.

**Fix timing:** Next phase

---

## 8. AI and Prompt Safety Findings

### Finding: The AI boundary is clean, but the contract boundary is still soft

**Severity:** Medium  
**Type:** Fact

**Why it matters:**

The good news is that AI concerns have not leaked into controllers, strategies, or the core evaluator. The remaining weakness is not architectural contamination; it is contract trust after the model responds.

**Evidence:**

- clean interface split via `IPromptSanitizer` and `IAiFlagAnalyzer`
- Semantic Kernel usage confined to `Banderas.Infrastructure/AI/AiFlagAnalyzer.cs`
- no post-parse semantic validation of AI output

**Suggested remediation:**

Keep the current abstraction split and add response validation plus one failing integration path.

**Fix timing:** Now

---

## 9. Technical Debt Register

| ID | Debt Item | Severity | Why It Exists | Impact | Suggested Fix | Fix Timing |
|---|---|---|---|---|---|---|
| TD-001 | `FeatureEvaluationContext` crosses `IBanderasService` boundary | Medium | Evaluation path predates a fully consistent DTO boundary | Inconsistent controller/service contract | Replace with application DTO or explicitly document the exception | Now |
| TD-002 | AI config crash at startup | High | Analyzer wiring is validated in DI, not at use time | Entire app can fail before boot in non-testing envs | Make analyzer registration/config lazy or degrade to unavailable analyzer | Now |
| TD-003 | `Flag` invariants are under-enforced | Medium | Validation concentrated at HTTP boundary during MVP | Future non-HTTP callers can create invalid entities | Move minimum invariants into `Flag` | Next phase |
| TD-004 | AI response semantics unverified | Medium | Structured output is trusted after JSON parse | Possible incorrect 200 responses from model drift | Add semantic response validation | Now |
| TD-005 | AI unhappy path not integration-tested | Medium | Test factory always injects successful stub analyzer | 503 contract can regress silently | Add failing-analyzer integration test | Now |

---

## 10. Risks for Phase 2

- What becomes painful if left alone:
  The Azure OpenAI startup dependency and boundary inconsistency will keep leaking into unrelated Phase 2 work.
- What assumptions are currently unproven:
  That AI failures consistently degrade to the documented 503 shape, and that model output always respects the expected response contract.
- What weak seams could slow upcoming work:
  The evaluation boundary inconsistency and permissive domain entity rules will complicate any future SDK, CLI, or alternate ingestion surface.
- What parts of the system invite accidental complexity in the next phase:
  AI wiring in DI, ad hoc repository filter growth, and documentation that still describes stricter boundaries than the code enforces.

---

## 11. Recommended Refactors Before Phase 2

### Fix Now

- Decouple Azure OpenAI configuration from global app startup so `/api/flags/health` can fail independently.
- Decide the evaluation boundary: restore a DTO-only service contract or explicitly ratify the `FeatureEvaluationContext` exception.
- Add contract enforcement and an unhappy-path integration test for AI analysis.

### Fix Soon

- Strengthen `Flag` invariants and expand direct domain tests.
- Clean up the remaining documentation drift around smoke-test path casing and service-boundary wording.

### Safe to Defer

- Replace nullable repository filtering with a richer `FlagQuery` object.
- Expand telemetry beyond evaluation events.
- Add dynamic strategy registration ergonomics beyond the current registry dispatch.

---

## 12. Documentation Updates Required

The audit resulted in these documentation changes:

- `Docs/current-state.md`
  Updated phase status, gate decision, known issues, current focus, and smoke-test path casing.
- `Docs/roadmap.md`
  Marked the architecture review complete, recorded the gate as GO WITH CONDITIONS, and adjusted current focus.
- `Docs/architecture.md`
  Clarified that the current implementation preserves the entity boundary but still leaks `FeatureEvaluationContext` across the service boundary.

No additional `Docs/Decisions/.../implementation-notes.md` file was amended in this pass. The audit report itself captures the durable deltas and debt.

---

## 13. Final Gate Verdict

### GO WITH CONDITIONS

Phase 2 may begin, but only with the current debt explicitly carried forward and a short pre-Phase-2 fix list owned up front:

- remove the Azure OpenAI startup blast radius
- close the AI unhappy-path and output-contract gaps
- resolve or formally document the evaluation boundary exception

---

## Scorecard Template

| Area | Score (1-5) | Notes |
|---|---:|---|
| Architecture / boundaries | 3 | Strong layering overall, but the evaluation path violates the stated DTO-only service boundary. |
| Domain model integrity | 3 | Controlled mutation exists, but key invariants still live outside the entity. |
| Application orchestration | 4 | Service orchestration is readable and appropriately central. |
| Evaluation engine design | 4 | Pure, deterministic, and easy to extend for current strategy set. |
| Strategy extensibility | 4 | Existing evaluator seam is clean; future strategy additions still need validator/config work. |
| Persistence design | 4 | Repository and EF usage are disciplined; uniqueness handling is pragmatic. |
| Validation / sanitization | 4 | Explicit and mostly strong, with a small GET/query boundary mismatch. |
| API / error handling | 4 | Consistent ProblemDetails behavior, but the AI 503 path lacks end-to-end proof. |
| AI boundary quality | 3 | Abstractions are clean; startup coupling and output-contract trust reduce confidence. |
| Observability seams | 4 | Evaluation telemetry abstraction is well-placed. |
| Test quality | 3 | Good breadth, but important AI and domain-invariant gaps remain. |
| Readiness for Phase 2 | 3 | Viable with conditions, not clean enough for an unconditional go. |

---

## Top 5 Priority Actions Template

1. Move Azure OpenAI endpoint validation out of startup so the AI endpoint can fail independently.
2. Resolve the `IBanderasService` boundary drift on `FeatureEvaluationContext`.
3. Add semantic validation of AI responses before returning 200.
4. Add an integration test for `AiAnalysisUnavailableException` -> 503 ProblemDetails.
5. Strengthen `Flag` invariants and direct domain tests before adding new input surfaces.

---

## Architecture Delta Summary Template

- Intended:
  `IBanderasService` as a DTO-only application boundary with validation rejected before service code and AI analysis degrading per endpoint.
- Actual:
  The entity boundary is preserved, but evaluation still crosses the service boundary with `FeatureEvaluationContext`, GET env validation partially occurs in the service layer, and Azure OpenAI configuration can fail the app at startup.
- Why the delta exists:
  The implementation favored incremental delivery and reuse of the existing domain evaluation context over a stricter service contract, and AI wiring was added at DI time for simplicity.
- Is it acceptable, temporary, or dangerous:
  Acceptable short term, but the startup coupling is dangerous enough to fix before broader Phase 2 work.
- Revisit by:
  Start of Phase 2.
