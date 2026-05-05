---
name: design-an-interface
description: Generate 2-3 competing API designs for a .NET endpoint or service interface, evaluate tradeoffs, and help the developer pick and defend the best one. Covers minimal API vs controller, versioning, ProblemDetails, validation placement, and service layer patterns. Use when the user says "design an interface", "design an endpoint", "how should I structure this API", or is about to add a new endpoint or service interface.
---

# Design an Interface

Generate competing designs. Force a tradeoff conversation. Pick one and commit.

See [REFERENCE.md](REFERENCE.md) for .NET patterns: minimal API, controllers, versioning, ProblemDetails, and validation.

## Philosophy

Never propose one design. Two designs reveal a tradeoff. Three designs reveal a spectrum. The goal is not to find the "right" answer — it's to make the tradeoffs visible so the developer can make an informed, defensible choice.

## Workflow

### 1. Understand the interface being designed
Ask or infer:
- What does this endpoint/service do? Is it a read or a write?
- Who calls it? (external clients, internal services, background jobs)
- What are the failure modes? (validation errors, not found, conflict, domain errors)
- Does it need versioning? Is this a public API or internal?

### 2. Explore the codebase
Read existing endpoints to understand current conventions — routing style, response shapes, error handling, validation approach, how the service layer is structured. New interfaces should feel consistent with what's already there unless there's a reason to break the pattern.

### 3. Generate 2-3 competing designs

For each design, show:
- The endpoint signature / interface declaration
- The request/response shape
- How validation is handled
- How errors surface to the caller
- The key tradeoff in one sentence

Label designs clearly: **Option A**, **Option B**, **Option C**.

### 4. Present the tradeoff table

After showing the designs, produce a summary table:

| | Option A | Option B | Option C |
|---|---|---|---|
| Complexity | low | medium | high |
| Testability | — | — | — |
| Consistency with codebase | — | — | — |
| Interview talking point | — | — | — |

### 5. Ask the developer to pick and defend

"Which option fits your codebase and why? What would you tell an interviewer?"

This is not optional. The defense is the learning.

### 6. Finalize the design

Once a design is chosen, produce the complete implementation sketch:
- Route and HTTP method
- Request/response types
- Validation
- Error response (ProblemDetails shape)
- Handler or controller action stub
- Suggested test cases (one happy path, one failure path)

## Design dimensions to vary across options

Vary these across your three options — don't just change variable names:

- **Endpoint style**: minimal API (`app.MapPost`) vs controller (`[ApiController]`)
- **Return type**: `IResult` / `TypedResults` vs `ActionResult<T>` vs raw response type
- **Validation placement**: endpoint filter, FluentValidation pipeline, domain layer guard
- **Error surfacing**: `ProblemDetails` with type URI vs plain status codes vs `Result<T>`
- **Service dispatch**: direct service injection vs a more structured handler pattern — match what the codebase already uses
- **Versioning**: URL segment (`/v1/`) vs header (`api-version`) vs none
