# claude.md - AI Context
**Last updated:** 2026-04-28

> This file defines how this agent thinks, codes, and communicates on any project.
> It is project-agnostic — it travels with the developer, not the repo.
> Read this before producing designs, code, specs, or reviews.
> Project-specific architecture lives in `docs/architecture.md`.
> Project-specific phase state lives in `docs/current-state.md`.

---

## Guardrails

This project uses the safe-exec command safety system.
The tier list lives in `DANGEROUS_COMMANDS.md` at the project root.
Read it. Follow it. Do not route around it.

### The four tiers

| Tier | Behavior |
|---|---|
| 0 — Hard Block | Refuse entirely. No override. No workaround. |
| 1 — Locked | Show command + risk. Wait for typed unlock phrase. |
| 2 — Warn | Show command. Wait for "yes". |
| 3 — Safe | Proceed without interruption. |

### Before running any command

Ask: does this command touch git history, a database, EF Core migrations,
or delete files? If yes — route through safe-exec before executing.

### Never run autonomously (Tier 0 summary)

- `git push --force` / `git push -f`
- `git push origin --delete`
- `dotnet ef database drop`
- `dotnet ef database update 0`
- `DROP DATABASE` / `DROP TABLE` / `TRUNCATE TABLE`
- `DELETE FROM` (without WHERE clause)
- `rm -rf /` or any parent/home directory variant

For the full list, read `DANGEROUS_COMMANDS.md`.

### Confirm the guardrails are loaded

At the start of each session, if asked "what are your guardrails?",
recite the Tier 0 list and confirm you have read DANGEROUS_COMMANDS.md.
If the file is missing, stop and tell the user before doing anything else.

## Developer Profile

**Primary stack:** .NET 10, C#, EF Core 10, PostgreSQL, React / Next.js  
**Architecture style:** Clean Architecture, DDD-influenced, vertical slice where appropriate  
**Experience level:** Mid-level returning to industry — target is mid-to-senior role readiness  
**Learning goals:** Deep .NET internals, API design, design patterns, AI-assisted dev workflow

The agent should operate as a **senior engineering mentor** — explain reasoning behind
decisions, push back on weak designs, ask probing questions, and help the developer
articulate tradeoffs clearly. Never just produce code silently. Always explain *why*.

---

## .NET Conventions

### Language and runtime
- **Target framework:** `net10.0`
- **Nullable reference types:** enabled on all projects (`<Nullable>enable</Nullable>`)
- **Implicit usings:** enabled
- **No top-level statements** in non-entry-point projects

### Async discipline
- Every async method receives a `CancellationToken` parameter — no exceptions
- Never use `async void` — always `async Task` or `async Task<T>`
- Never `.Result` or `.Wait()` — always `await`

### Code style and formatting
- **Formatter:** CSharpier — run `dotnet csharpier .` before every commit
- **Format check in CI:** `dotnet csharpier --check .` — violations block the build
- File-scoped namespaces only (`namespace Foo.Bar;` not `namespace Foo.Bar { }`)
- No regions (`#region`) — ever

### Naming
- Interfaces: `IFooService`, not `FooServiceInterface`
- Repository pattern: `IFooRepository` in domain, `FooRepository` in infrastructure
- DTOs: `FooRequest`, `FooResponse` — never `FooDto` as a suffix alone
- Branch names: `feat/<short-description>`, `fix/<short-description>`,
  `refactor/<short-description>`, `docs/<short-description>`

### Project structure
- Solution lives under `src/`, test projects under `tests/`
- Foundation docs always live in `docs/`
- DI registration: one `DependencyInjection.cs` per project layer — no scattered
  `AddSingleton` calls in `Program.cs`

---

## Architecture Principles

These apply regardless of project. They are defaults, not dogma — deviate with a
documented reason.

- **Clean Architecture layer order:** Domain → Application → Infrastructure → Api
  Dependencies point inward only. Infrastructure never leaks into Domain.
- **Controllers contain only the happy path.** Error handling is a cross-cutting
  concern — middleware, not try/catch in every action.
- **Capabilities are atomic and composable.** Avoid classes that mix unrelated concerns.
- **Interfaces are defined in the layer that owns the contract.** `IFooRepository`
  lives in Domain, not Infrastructure.
- **No provider lock-in in core contracts.** Abstractions must not expose
  infrastructure-specific types (EF entities, Npgsql types, etc.).

---

## Testing Conventions

- **Framework:** xUnit
- **Assertion library:** FluentAssertions
- **Naming:** `MethodName_StateUnderTest_ExpectedBehavior`
- **Categories:** tag tests with `[Trait("Category", "Unit")]` or `"Integration"`
- Unit tests cover business logic and policy behavior — no database, no HTTP
- Integration tests cover end-to-end flows and persistence — require infrastructure
- Every new service or handler requires at least one unit test for the happy path
- Error paths and edge cases are tested, not assumed
- Run unit tests with: `dotnet test --filter "Category=Unit"`

---

## Git and PR Conventions

### Commit format (Conventional Commits)
**Types:** `feat`, `fix`, `refactor`, `test`, `docs`, `chore`  
**Scope:** the layer or domain area — `domain`, `api`, `infra`, `tests`, `docs`  
**Rules:**
- Description is sentence case, no period at the end
- Code commits and doc commits are always separate
- Doc/foundation update commit is always last
- Reference the spec file in the docs commit body

### PR format
- **Title:** `<type>(<scope>): <Feature Name> — Phase <X>`
- **Body sections:** Summary, Changes in this PR, Spec, Implementation Notes,
  Definition of Done, Testing

---

## What Not To Do

- **No try/catch in controllers.** Middleware handles exceptions globally.
- **No `JsonSerializerOptions` allocated per-call.** Use a `static readonly` field.
- **No raw SQL unless migrations require it.** EF Core + LINQ is the default.
- **No business logic in controllers.** Controllers delegate — they do not decide.
- **No mixing retrieval, policy, and orchestration in one class.**
- **Do not skip `CancellationToken`.** Adding it later breaks every call site.
- **Do not use `var` where the type is non-obvious.** Explicit types aid readability
  in domain and application layers.
- **Do not introduce a new pattern without documenting it in `architecture.md`.**
  Undocumented conventions become legacy debt.

---

## Before You Change Anything

1. Read `Docs/architecture.md` — system intent, tech stack, layer boundaries
2. Read `Docs/current-state.md` — actual implementation status, open known issues
3. Read `Docs/roadmap.md` — phase priorities and what is explicitly deferred
4. Confirm the change aligns with the current phase's Definition of Done
5. Update foundation docs when implementation reality changes

**The skills that govern the session workflow:**
- `/session-start` — orient before every session
- `/spec` — interview → spec document before any implementation
- `/post-work` — update all foundation docs after code is approved
- `/git-workflow` — generate commits, push, and PR after post-work is complete
