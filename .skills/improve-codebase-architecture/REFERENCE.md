# Improve Codebase Architecture — .NET Reference

## Dependency Categories

### 1. In-process
Pure C# computation, in-memory state, no I/O. Domain logic, value objects, calculation services.
Always deepenable — merge the modules and test directly with no test infrastructure needed.

### 2. Local-substitutable
Dependencies with a local test stand-in that runs in-process.

| Real dependency | .NET test stand-in |
|---|---|
| SQL Server via EF Core | EF Core InMemory provider or SQLite in-memory |
| Full HTTP pipeline | `WebApplicationFactory<Program>` |
| File system | `System.IO.Abstractions` with `MockFileSystem` |
| Clock / time | `TimeProvider` (abstract, .NET 8+) or `ISystemClock` |

Deepenable — run the stand-in inside the test suite, test at the module boundary.

### 3. Remote but owned (Ports & Adapters)
Your own services across a network boundary (internal APIs, other microservices).
Define a port (C# interface) at the module boundary. The deep module owns the logic; transport is injected.

- **Production adapter**: typed `HttpClient` via `IHttpClientFactory`, real gRPC client, etc.
- **Test adapter**: in-memory implementation of the port interface

### 4. True external (Mock)
Third-party services you don't control (Stripe, SendGrid, Twilio).
Define an interface (port) at the boundary. Mock it in tests. Never let the third-party SDK bleed through the interface.

---

## Sub-agent design constraints (Step 5)

Use these when spawning sub-agents (Claude Code) or designing sequentially (Claude.ai):

- **Option A — Minimal**: Aim for 1-2 entry points max. Hide everything behind the simplest possible surface.
- **Option B — Flexible**: Support multiple use cases and future extension. More methods, but all cohesive.
- **Option C — Caller-optimized**: Design around the most common caller. Make the default case trivial; edge cases are secondary.

Each option must also answer: *does this design make a future MediatR/CQRS migration easier or harder?*

---

## CQRS migration note

The codebase is currently using direct service calls but is planning a migration to CQRS (MediatR). When evaluating interface designs, flag:

- ✅ **CQRS-friendly**: interface that separates reads from writes at the boundary — easy to wrap in `IRequest<T>` later
- ⚠️ **CQRS-neutral**: interface that mixes reads and writes — migration is possible but requires splitting
- ❌ **CQRS-hostile**: interface that bakes in assumptions that conflict with command/query separation

---

## Testing strategy

Core principle: **replace, don't layer.**

- Write new tests at the deepened module's interface boundary
- Assert on observable outcomes through the public interface — not internal state
- Tests should survive internal refactors — they describe behavior, not implementation
- Old unit tests on shallow modules become redundant once boundary tests exist — **delete them**

---

## RFC Issue Template

```md
## Problem

Describe the architectural friction:
- Which classes/concepts are shallow and tightly coupled
- Which smell from the architecture review applies (anemic model, fat controller, EF leak, etc.)
- Why this makes the codebase harder to test, navigate, and extend

## Proposed Interface

The chosen design:
- Interface signature (C# types, methods, params)
- Usage example showing how callers interact with it
- What complexity it hides internally

## Dependency Strategy

Which category applies and how dependencies are handled:
- **In-process**: merged directly
- **Local-substitutable**: tested with [specific stand-in, e.g. EF Core InMemory]
- **Ports & adapters**: port interface, production adapter, test adapter
- **Mock**: mock boundary for external service

## CQRS Migration Impact

Is this design CQRS-friendly, CQRS-neutral, or CQRS-hostile? What, if anything, needs to change when MediatR is introduced?

## Testing Strategy

- **New boundary tests to write**: describe behaviors to verify at the interface
- **Old tests to delete**: shallow module tests that become redundant
- **Test environment needs**: stand-ins or adapters required

## Implementation Guidance

Durable guidance NOT coupled to current file paths:
- What the module should own (responsibilities)
- What it should hide (implementation details)
- What it should expose (the interface contract)
- How callers should migrate to the new interface
```
