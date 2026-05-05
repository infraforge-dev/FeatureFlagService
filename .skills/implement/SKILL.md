---
name: implement
description: >
  Orchestrate the implementation of an approved spec using TDD, layer by layer, one
  acceptance criterion at a time. Use this skill whenever the user says "/implement",
  "let's build it", "start implementation", "approved", or immediately after a spec
  has been approved. This skill reads the spec, plans the build order, runs
  RED→GREEN→REFACTOR cycles, self-verifies the Definition of Done, and produces a
  HITL gate report before handing off to /post-work. Always invoke this skill after
  spec approval — never write implementation code outside of this skill.
---

# Implement

The spec is approved. Now build it — one acceptance criterion at a time, test first,
no shortcuts. This skill orchestrates the full build from approved spec to HITL gate.

Reference the TDD rules in `/tdd` and the naming conventions and patterns in
`reference.md` throughout this skill. Do not duplicate their content here — call them.

---

## Before you begin — confirm the gate

Ask: "Is the spec approved? Which spec file are we implementing?"

Do not proceed until confirmed. If multiple spec files exist (phased specs), confirm
which phase we are implementing. Implement one phase at a time.

---

## Step 1 — Read the inputs

Read all of these before writing a single line of code:

1. `docs/decisions/<feature-branch-name>/spec.md` — the contract
2. `docs/architecture.md` — layer boundaries and guardrails to respect
3. `reference.md` — naming conventions, test patterns, tooling
4. Existing test files in the relevant test project — match established conventions

Extract and hold in context:
- The **Acceptance Criteria** — these become your TDD cycle targets, one per cycle
- The **File Layout / Scope** — every file to be created or modified
- The **Definition of Done** checklist — the self-verify target at the end
- The **Technical Notes** — packages, pitfalls, build sequence

---

## Step 2 — Classify the work and plan the build order

Read the **Scope** section of the spec. For every file listed, classify it by layer:

| Layer | Examples | TDD approach |
|---|---|---|
| Domain | entities, value objects, domain services, exceptions | TDD — unit tests first |
| Application | command/query handlers, interfaces, DTOs | TDD — unit tests first |
| Infrastructure | repositories, DbContext, migrations, config | Integration/DB tests after |
| API | controllers, middleware, endpoints, DI registration | Integration tests after |
| Tests | test files themselves | Written as part of each cycle |

**Rules:**
- Apply TDD (RED→GREEN→REFACTOR) to Domain and Application layers
- Use integration or database tests written *after* for Infrastructure and API layers
- Never mix TDD and non-TDD files in the same cycle

Present the build plan to the user in this format:

```
## Build Plan

### TDD Cycles (test-first)
1. [AC #1 description] → unit test → [ClassName]
2. [AC #2 description] → unit test → [ClassName]

### Integration / Post-wiring (tests after)
3. [AC #3 description] → infra wiring → integration test
4. [AC #4 description] → API endpoint → integration test

### Non-TDD (no tests needed)
5. DI registration → DependencyInjection.cs
6. EF Core migration → generated

Proceed with this plan?
```

Wait for confirmation before writing any code.

---

## Step 3 — Execute TDD cycles (one acceptance criterion at a time)

For each item in the TDD section of the build plan, run one complete cycle before
moving to the next.

### Cycle structure

**3a. Announce the cycle**

```
--- Cycle N of X ---
Acceptance Criterion: [exact text from spec]
Target class/method: [ClassName.MethodName]
Test type: Unit | Integration | Database
```

**3b. Read reference.md for the correct test type pattern**

Pick the matching pattern (unit / integration / database) and apply it exactly —
naming convention, Arrange/Act/Assert structure, one assertion per test.

**3c. Write the test — RED**

Write the smallest possible failing test that captures this acceptance criterion.
Follow the naming convention from `reference.md`:
`MethodOrBehaviour_Scenario_ExpectedOutcome`

Show the test. Then show the expected failure output:

```
Expected RED:
dotnet test → [TestClassName.TestMethodName]
Reason: [ClassName] does not exist / method not implemented / returns wrong value
```

Pause and say:
> Run the test now. Confirm it fails for the reason above, then reply "red" to continue.

**3d. Write the implementation — GREEN**

Write the minimum code to make the test pass. No speculative logic. No handling
cases the tests have not yet asked for.

Show only the code required to pass this test.

Then say:
> Run the test now. Confirm it passes, then reply "green" to continue.

**3e. Refactor**

Look for duplication, unclear naming, or violation of layer boundaries introduced
by the GREEN step. Clean it up. Tests must stay green throughout.

If nothing needs refactoring, say: "No refactor needed — code is clean."

**3f. Check for spec gaps**

After each cycle, ask:
- Does this implementation surface any ambiguity in the **public API contract**
  (response shape, error codes, validation rules)?
  → **Stop and ask the user** before proceeding to the next cycle.
- Does this implementation surface an ambiguity in an **internal detail**
  (method naming, helper extraction, private structure)?
  → **Assume, flag it, and keep going.** Format: `⚠️ Assumption: [what was assumed and why]`

**3g. Drop a learning marker if applicable**

If this cycle exercised a non-obvious .NET concept, design pattern decision, or
revealed something the spec didn't anticipate — drop a marker:

`🧠 [Brief description of the teachable moment]`

These markers are collected into the Learning Summary in Step 5.

---

## Step 4 — Execute integration and post-wiring items

For each non-TDD item in the build plan:

1. Write the implementation code
2. Write the integration or database test *after* the code exists
3. Note any `⚠️ Assumption` or `🧠` markers as in Step 3

No RED→GREEN pause cycle for these — but confirm with the user after each item:
> "[Item N] complete. Tests written and passing. Continue to next item?"

---

## Step 5 — Self-verify the Definition of Done

Read the **Definition of Done** checklist from the spec. For each item, verify its
status by running the appropriate command or reading the relevant output.

Always run:

```bash
dotnet build
dotnet test
```

Produce a DoD status report:

```
## Definition of Done — Status

- [ ] ✅ Project builds with no errors
- [ ] ✅ All tests passing (X/X)
- [ ] ✅ No compiler warnings introduced
- [ ] ✅ [AC #1 from spec] — covered by [TestMethodName]
- [ ] ✅ [AC #2 from spec] — covered by [TestMethodName]
- [ ] ❌ [Item] — [reason not complete]
```

If any item is ❌, do not proceed to Step 6. Fix the item and re-verify.

---

## Step 6 — Write the Learning Summary

Collect all `🧠` markers dropped during the cycles. Produce the Learning Summary
using this fixed three-field structure:

```
## 🧠 Learning Summary

**Concept:** [What .NET feature, design pattern, or architectural principle did
this feature exercise? Name it specifically — e.g., "EF Core owned entities",
"IOptions<T> pattern", "MediatR pipeline behaviors"]

**Insight:** [What did the TDD cycle reveal that the spec didn't anticipate?
What design decision became obvious only once you wrote the test?]

**Junior Note:** [One plain-English explanation of the trickiest part of this
implementation — written for someone reading this codebase for the first time,
six months from now.]
```

If no `🧠` markers were dropped during the cycles, write the Learning Summary
from your own observation of the most interesting decision made during implementation.
This section must always be populated — "N/A" is not acceptable.

---

## Step 7 — HITL gate

Present the complete implementation report to the user:

```
## Implementation Complete — Review Required

**Branch:** [branch name from spec]
**Spec:** docs/decisions/<feature-branch-name>/spec.md
**Cycles completed:** X of X
**Build:** ✅ Passing
**Tests:** X/X passing

### Definition of Done
[paste DoD status report from Step 5]

### Assumptions Made
[list all ⚠️ Assumption flags from the cycles, or "None"]

### Spec Gaps Raised
[list any gaps that were stopped and resolved, or "None"]

### 🧠 Learning Summary
[paste Learning Summary from Step 6]
```

End with:

> Review the report above. If everything looks correct, reply "approved" and invoke
> `/post-work`. If anything needs fixing, tell me what to change.

Do not invoke `/post-work`. Do not update any foundation documents. Wait for explicit
approval.
