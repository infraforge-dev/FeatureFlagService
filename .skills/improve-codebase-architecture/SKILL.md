---
name: improve-codebase-architecture
description: Explore a .NET codebase to find architectural friction — shallow modules, anemic domain models, fat controllers, EF Core leaking into wrong layers — and propose deep module refactors as GitHub issue RFCs. Use when user wants to improve architecture, find refactoring opportunities, surface design smells, or make a codebase more testable and AI-navigable.
---

# Improve Codebase Architecture

Explore the codebase like an AI would. Surface friction. Propose deep module refactors as reviewed GitHub RFCs.

A **deep module** (John Ousterhout, "A Philosophy of Software Design") has a small interface hiding a large implementation. Deep modules are more testable, more AI-navigable, and let you assert behavior at a boundary instead of poking at internals.

See [REFERENCE.md](REFERENCE.md) for dependency categories, .NET test stand-ins, design option templates, and the RFC issue template.

---

## Process

## Step 0 — Check for project documentation

Before reading any code, ask:

> "Do you have project docs — architecture notes, current state, 
> or a roadmap? If so, where do they live?"

If docs exist, read them first. They may answer questions the 
code can't. Then proceed with the trail below.

### 1. Explore the codebase

Navigate organically — not with rigid heuristics. Note where you experience friction:

- **Anemic domain model**: entities with no behavior; all logic lives in service classes
- **Fat controllers**: action methods doing validation, mapping, business logic, and persistence
- **Shallow repositories**: `IUserRepository` that's just a wrapper around `DbSet<User>` with zero added value
- **EF Core leaking**: `DbContext` or `IQueryable` appearing in domain or controller code
- **Over-injection**: constructors with 6+ parameters — a SRP smell hiding in plain sight
- **Mixed service concerns**: one class orchestrating workflow AND encoding business rules
- **Untestable seams**: logic that can only be tested by standing up the full HTTP pipeline

The friction you hit during exploration IS the signal. Note it.

### 2. Present candidates

Show a numbered list of deepening opportunities. For each:

- **Cluster**: which classes/concepts are involved
- **Smell**: which friction signal from Step 1 applies
- **Dependency category**: In-process / Local-substitutable / Remote-owned / True external (see REFERENCE.md)
- **Test impact**: what existing tests would be replaced by boundary tests

Do NOT propose interfaces yet. Ask: "Which of these would you like to explore?"

### 3. User picks a candidate

### 4. Frame the problem space

Before designing options, write a user-facing explanation:
- The constraints any new interface must satisfy
- The dependencies it must rely on
- A rough illustrative C# sketch to ground the constraints — not a proposal, just a thinking tool

Show this to the user, then immediately proceed to Step 5.

### 5. Design 3 competing interfaces

**If running in Claude Code**: spawn 3 sub-agents in parallel using the Agent tool, each with a different design constraint (see REFERENCE.md for prompts and option templates).

**If running in Claude.ai**: design all 3 options sequentially yourself. Use the same option templates and constraints from REFERENCE.md.

Each option must produce:
1. Interface signature (types, methods, params)
2. C# usage example showing how callers use it
3. What complexity it hides internally
4. Dependency strategy (see REFERENCE.md)
5. CQRS migration note: does this design make a future MediatR migration easier or harder?
6. Trade-offs

Present designs, then compare in prose. Give a clear recommendation — which design is strongest and why. If a hybrid is better, say so explicitly.

### 6. User picks an interface (or accepts recommendation)

### 7. Draft the chore file

Using the RFC template in REFERENCE.md for the body, draft a chore file and show it to the user for review. Do NOT write the file until the user approves. Once approved, write to `chores/<id>.md`:

```markdown
---
id: <kebab-case-id>
title: <title>
type: HITL
status: open
blocked-by: []
parent-prd: null
---

<RFC body from REFERENCE.md template>
```

Use `type: HITL` — architecture decisions require human review before any implementation begins.
