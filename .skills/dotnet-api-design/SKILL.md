---
name: dotnet-api-design
description: Audit an existing ASP.NET Core API for production readiness, or guide the design of a new one from scratch. In audit mode, traces the request lifecycle through the codebase and produces a scored report (5 must-haves) with optional GitHub RFCs per gap. In greenfield mode, interviews the developer on two key architectural decisions then generates a wired-up Program.cs scaffold. Use when the user says "audit my API", "is my API production-ready", "review my endpoints", "design a new API", "start a new .NET API project", or "what am I missing in my API".
---

# dotnet-api-design

Audit a mature API for production readiness. Or design a new one right the first time.

See [REFERENCE.md](REFERENCE.md) for pass/fail patterns and scaffold templates for each of the five must-haves.

## Philosophy

Unopinionated but conservative. This skill does not care whether you use minimal APIs or controllers, FluentValidation or data annotations, a service layer or something else entirely — but it will absolutely care if you have no versioning strategy, no consistent error shape, or naked domain entities leaking into your HTTP responses.

---

## Step 0 — Check for project documentation

Before reading any code, ask:

> "Do you have project docs — architecture notes, current state, 
> or a roadmap? If so, where do they live?"

If docs exist, read them first. They may answer questions the 
code can't. Then proceed with the trail below.

## Step 1 — Determine mode

Ask or infer from context:

- Existing project → **Audit mode**
- Starting fresh → **Greenfield mode**
- Unclear → ask: *"Are you reviewing an existing API or designing a new one?"*

## Step 2 — Check project type (both modes)

Read the `.csproj` first. If no HTTP surface is present — no `Microsoft.AspNetCore.*`, no OpenAPI package, no `Asp.Versioning` — this may be a Worker Service, CLI, gRPC service, or library.

**If non-HTTP:** Say so immediately. Do not run the HTTP checklist. Offer what this skill can help with — DI registration conventions, `appsettings.json` structure, `IHostedService` patterns — and stop there.

---

## Audit mode

## Step 0 — Check for project documentation

Before reading any code, ask:

> "Do you have project docs — architecture notes, current state, 
> or a roadmap? If so, where do they live?"

If docs exist, read them first. They may answer questions the 
code can't. Then proceed with the trail below.

### 1. Follow the trail

Trace the request lifecycle. Read in this order — each file informs the next:

1. `.csproj` — which packages are installed? Note: `Asp.Versioning`, `FluentValidation`, `Swashbuckle` / `Scalar`, `Microsoft.AspNetCore.OpenApi`
2. `Program.cs` — what is wired into the DI container and middleware pipeline?
3. Endpoints — controllers (`Controllers/`), minimal API files (`Endpoints/`, `Features/`), or `app.Map*` calls in `Program.cs`
4. Request / response types — look for `*Request`, `*Response`, `*Dto` files; check whether domain entities appear in endpoint signatures
5. Test project — integration tests? Unit tests on domain logic?

### 2. Score each must-have

For each must-have, assign: **✅ Present and wired** | **⚠️ Partial** | **❌ Missing**

See REFERENCE.md for exact pass/fail criteria. Package present is not enough — evidence of *use* is required to pass.

| # | Must-have | Status | Finding |
|---|---|---|---|
| 1 | Versioning strategy | | |
| 2 | ProblemDetails / global error shape | | |
| 3 | Validation placement | | |
| 4 | DTO conventions (no naked entities) | | |
| 5 | OpenAPI documentation | | |

### 3. Present the scored report

Show the completed table. Add one sentence per gap: what was found and why it matters.

Then ask: **"Which gap do you want to address first?"**

Do not draft or file any GitHub issues yet.

### 4. Drill into a gap (on request)

For the chosen gap:
- Show the anti-pattern found in the codebase
- Show the corrected pattern from REFERENCE.md
- Draft a chore file: title, intent, acceptance criteria, type (`HITL` if the gap requires design decisions; `AFK` if the fix is mechanical)

Show the draft in full. Ask: **"Should I write this chore file?"**

Once confirmed, write to `chores/<id>.md`. Do not write without explicit confirmation. Repeat for each gap the developer wants to address.

---

## Greenfield mode

### 1. Interview — two questions only

These are the two decisions you cannot easily undo later. Everything else gets a sensible default.

**Question 1 — Endpoint style:**
> "Minimal API (`app.MapPost`) or controllers (`[ApiController]`)?"
> Default recommendation: minimal API for new projects; controllers if the team already lives in that world.

**Question 2 — Versioning strategy:**
> "URL segment (`/api/v1/`), query string (`?api-version=1.0`), or header (`api-version: 1.0`)?"
> Default recommendation: URL segment — most visible, easiest to test, what most public APIs use.

### 2. Generate the scaffold

Using the two answers, produce a `Program.cs` skeleton with all five must-haves wired as sensible defaults. See REFERENCE.md for the scaffold templates.

Tell the developer which defaults were applied and where each concern lives in the generated code.

### 3. Offer the next step

> "Want me to run an audit once you've added your first endpoint?"
