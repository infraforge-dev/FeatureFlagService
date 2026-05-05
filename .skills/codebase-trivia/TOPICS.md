# Topics Reference

Each topic lists what to probe at each difficulty level. Always ground questions in the developer's actual code — use their class names, layer names, and domain terms.

---

## 1. Architecture & Layering
**Probe:** Layer boundaries, dependency direction, what belongs where.
- Rookie: "Which layer does X class live in and why?"
- Mid: "This class touches both domain logic and EF Core — is that a problem? Where would you move it?"
- Senior: "If you needed to swap SQL Server for MongoDB, which layers would change and which would be untouched? Why?"

---

## 2. Design Patterns
**Probe:** Pattern identification, intent, and why they chose it.
- Rookie: "Name the pattern used in X class."
- Mid: "You're using Repository here — what problem does it solve, and what does it NOT solve?"
- Senior: "Where in this codebase would the Mediator pattern reduce coupling, and what's the tradeoff of adding it?"

---

## 3. Domain-Driven Design
**Probe:** Entities vs value objects, aggregates, invariants, ubiquitous language.
- Rookie: "Is X a value object or an entity? How do you know?"
- Mid: "Where does this aggregate enforce its invariants? Show me the guard clause."
- Senior: "This domain event is raised in the application layer, not the domain layer. Is that a DDD smell? Defend your position."

---

## 4. Dependency Injection & Lifetimes
**Probe:** Scoped vs singleton vs transient, captive dependency bug, registration location.
- Rookie: "What lifetime is X registered with and what does that mean?"
- Mid: "If you inject a Scoped service into a Singleton, what happens at runtime?"
- Senior: "Walk me through how the DI container resolves a request from controller to repository. Where could a lifetime mismatch hide?"

---

## 5. Testing Strategy
**Probe:** What's tested, what should be, unit vs integration boundaries, mocking decisions.
- Rookie: "Is this test a unit test or integration test? What's the difference?"
- Mid: "This test mocks the repository. What's the risk of that approach?"
- Senior: "If you had to add one integration test that gave you the most confidence in this feature, what would it test and why?"

---

## 6. Async / Await Correctness
**Probe:** Deadlock risk, fire-and-forget anti-patterns, ConfigureAwait, Task vs ValueTask.
- Rookie: "What does `await` actually do here?"
- Mid: "This method is `async void` — why is that dangerous?"
- Senior: "Spot the potential deadlock in this code path. How would you fix it?"

---

## 7. Data Access & EF Core
**Probe:** N+1 queries, eager vs lazy loading, when EF leaks into wrong layers, migration safety.
- Rookie: "What does `.Include()` do and when do you need it?"
- Mid: "This query calls `.ToList()` before filtering — what's the performance impact?"
- Senior: "How would you detect an N+1 query in production? What's your mitigation strategy in this codebase?"

---

## 8. API Contract Design
**Probe:** Status codes, error response shape, versioning, validation placement.
- Rookie: "Why does this endpoint return 200 instead of 201?"
- Mid: "Where should input validation live — controller, application layer, or domain? What does this codebase do?"
- Senior: "If a consumer of this API is on v1 and you need to make a breaking change, walk me through your versioning strategy."

---

## 9. Error Handling
**Probe:** Exception middleware, result pattern, ProblemDetails, what leaks to the client.
- Rookie: "What happens if this service throws an unhandled exception? What does the client see?"
- Mid: "Compare throwing exceptions vs returning a Result<T> — when would you use each?"
- Senior: "Design the error handling strategy for this API end-to-end: from domain error to HTTP response."

---

## 10. SOLID Principles
**Probe:** Spot violations, name the principle, suggest the fix.
- Rookie: "Name the SOLID principle this class violates and why."
- Mid: "This service has 7 injected dependencies. Which principle is under pressure and what would you do?"
- Senior: "The Open/Closed Principle says open for extension, closed for modification. Show me a place in this codebase where a switch statement is a violation waiting to happen."

---

## 11. Middleware & Request Pipeline
**Probe:** Order sensitivity, short-circuiting, where cross-cutting concerns live.
- Rookie: "What is middleware and where does it sit in a request?"
- Mid: "If authentication middleware runs after routing middleware, what security hole does that create?"
- Senior: "Trace a request from HTTP entry to database call in this codebase. Name every middleware it passes through and why the order matters."

---

## 12. Observability & Logging
**Probe:** Structured logging, what to log vs not, correlation IDs, log levels.
- Rookie: "What's the difference between `LogInformation` and `LogError`?"
- Mid: "This log message uses string concatenation instead of structured logging — what's the problem?"
- Senior: "How would you trace a single failing request across multiple services in this architecture? What's missing from the current logging setup?"

---

## 13. Security Surface
**Probe:** Auth boundaries, input sanitization, what's exposed, secrets management.
- Rookie: "Where does this API enforce authorization — middleware or controller?"
- Mid: "This endpoint accepts a raw string that gets passed to a query — what's the risk and how does EF Core mitigate it?"
- Senior: "Walk me through the auth flow for a protected endpoint. Where could an attacker find a gap?"

---

## 14. Configuration & Environment Management
**Probe:** Secrets, appsettings hierarchy, environment-specific overrides, options pattern.
- Rookie: "How does this app know which database connection string to use in production vs development?"
- Mid: "Where should secrets live — appsettings.json, environment variables, or a secrets manager? What does this project do?"
- Senior: "Describe how you'd introduce a new third-party API key into this codebase safely across dev, staging, and production."