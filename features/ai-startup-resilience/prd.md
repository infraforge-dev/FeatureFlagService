---
slug: ai-startup-resilience
status: drafted
created: 2026-04-28
---

# AI Startup Resilience

## One-sentence intent
Allow the API to start without Azure OpenAI configuration while keeping AI flag analysis explicitly unavailable at request time.

## Who benefits, and how
API operators and developers benefit because non-AI endpoints remain available even when Azure OpenAI configuration is missing or incomplete, while API clients still receive a clear 503 response when requesting AI analysis that cannot run.

## Success criteria
- The application starts successfully in non-testing environments when `AzureOpenAI:Endpoint` is missing.
- Existing non-AI endpoints, including CRUD and feature evaluation endpoints, remain reachable when Azure OpenAI configuration is missing.
- Requests to the AI flag analysis endpoint return the existing documented 503 ProblemDetails response when Azure OpenAI configuration is unavailable.
- Startup-time dependency injection no longer throws `InvalidOperationException` solely because `AzureOpenAI:Endpoint` is missing.
- Existing testing behavior remains isolated from live Azure dependencies.

## Layers and interfaces
This crosses API, application, and infrastructure layers.

The infrastructure layer should change AI analyzer registration so missing Azure OpenAI configuration produces an unavailable analyzer or deferred failure path instead of a startup exception.

The application-facing `IAiFlagAnalyzer` interface should remain the boundary for AI analysis. No Azure SDK, Semantic Kernel, or configuration-specific types should leak into controllers or application orchestration.

The API layer should continue relying on the existing exception middleware to translate AI unavailability into the documented 503 ProblemDetails response.

## Explicitly out of scope
This PRD does not cover semantic validation of AI model responses; that belongs to a separate AI response contract PRD.

This PRD does not cover adding the AI unhappy-path integration test unless needed to prove this startup resilience behavior; broader AI error-path coverage can be handled separately.

This PRD does not change prompt construction, prompt sanitization, model selection, Azure authentication, or AI analysis output shape.

This PRD does not redesign the feature evaluation boundary or domain invariants.

## Dependencies and unknowns
The existing `IAiFlagAnalyzer` abstraction and `AiAnalysisUnavailableException` behavior are prerequisites.

[VERIFY] Confirm the current AI endpoint route and existing 503 ProblemDetails type URI before implementation.

[VERIFY] Confirm whether missing values other than `AzureOpenAI:Endpoint`, such as deployment/model settings or credentials, should follow the same endpoint-scoped unavailable behavior.

## Notes from interview
This PRD comes from the Phase 1 / 1.5 architecture review finding that `AzureOpenAI:Endpoint` currently creates a hard startup dependency for all non-testing environments. The intended fix is to reduce the blast radius of optional AI analysis so unrelated API capabilities are not taken down by missing AI configuration.
