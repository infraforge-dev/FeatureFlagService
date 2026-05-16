---
name: post-work
description: >
  Update all foundation documents to reflect completed implementation, update c4 diagrams (if necessary) ,and produce the
  implementation notes file. Use this skill whenever the user says "/post-work",
  "update the docs", "update foundation docs", "write the implementation notes", or
  after implementation has been reviewed and approved at the HITL gate. This skill
  must not run before code review is complete. This is step 3 of the Specwright workflow.
---

# Post-Work

The code has been approved. Now close the loop: update the project's living memory
and produce the implementation record. Do not touch the codebase. Only documents change.

## Before you begin — confirm the gate

Ask the user: "Has the code been reviewed and approved?" Do not proceed until they
confirm. This skill runs after the HITL gate, never before.

## Step 1 — Read the approved spec

Read `Docs/decisions/<feature-branch-name>/spec.md` (and spec-2.md etc. if split).
This is the contract. Everything you write in this step must reconcile with it.

## Step 2 — Update current-state.md

Make these changes:

1. **Status Summary** — update the phase status line (e.g., 🔄 In Progress → ✅ Complete
   if the phase DoD is now fully checked)
2. **What Is Completed** — move the implemented tasks from "Not Yet Built" to the
   correct phase section under "Completed". Use specific file and class names.
3. **What Is Not Yet Built** — remove the items that are now done. Add any new items
   that were discovered during implementation (deferred work, follow-ups from the
   Implementation Notes risks section).
4. **Known Issues** — open any new KIs surfaced during implementation. Close any KIs
   that this work resolved (mark Closed — PR #XX, do not delete).
5. **Current Focus → Immediate Next Tasks** — update to reflect what comes next
   based on the remaining DoD items or the next phase.
6. **Definition of Done — Phase X** — check off every completed item.
7. **Lessons Learned** — add an entry if implementation revealed a spec gap, a pattern
   to avoid, or a process insight. Format: `[YYYY-MM-DD] — [Title]` with a one-paragraph
   description and "The rule going forward:" statement.

## Step 3 — Update roadmap.md

1. Check off completed tasks in the current phase section.
2. If the phase is now fully complete, update its status in the Phase Map from 🔄 to ✅.
3. Update the current phase's "In Progress" label to reflect actual state.
4. Do not add new phases or change phase scope — that is architecture-level work.

## Step 4 — Update architecture.md (conditional)

Only update if this feature introduced or changed:
- A new layer, project, or assembly
- A new cross-cutting pattern (e.g., middleware, interceptor, global handler)
- A change to the security model or auth boundary
- A new external dependency or integration point
- Deprecation of a previously documented pattern

If none of these apply, write "No architecture changes — skipping" and move on.

## Step 5 — Produce implementation-notes.md

Write this file to: `Docs/decisions/<feature-branch-name>/implementation-notes.md`

Use this structure exactly:

```
# <Feature Name> — Implementation Notes

**Session date:** <today>
**Branch:** <branch>
**Spec reference:** Docs/decisions/<feature-branch-name>/spec.md
**Build status:** <from user confirmation>
**Tests:** <X>/<X> passing
**PR:** TBD

## What Was Built
## Spec Gaps Resolved
## Deviations from Spec
## Key Decisions
## File-by-File Changes
## Risks and Follow-Ups
## How to Test
## Interview Lens
## Foundation Docs Updated
## Definition of Done — Status
```
## Step 6 — Update C4 Diagrams (Docs/c4)

Update c4 diagrams if there were any changes that require modification to the diagrams. 

Rules:
- **What Was Built**: 2–4 sentences for a new team member. Outcome, not steps.
- **Spec Gaps Resolved**: Every place the spec was ambiguous or wrong. "None" if clean.
- **Deviations from Spec**: Every deliberate departure. State what the spec said, what
  was built instead, and why. "None" if implementation matched the spec exactly.
- **Key Decisions**: Choices made during implementation not specified in the spec —
  the things a senior engineer notices that a junior misses.
- **Interview Lens**: Pick the single most interesting engineering decision and write
  2–4 sentences explaining it as you would in a technical interview. Lead with the
  problem, state the tradeoff, name what you'd do differently at a different scale.
  This section is your portfolio ammunition — write it well.
- **Foundation Docs Updated**: Check every item as done.
- **Definition of Done — Status**: Mirror the spec's DoD and mark each item ✅ or ❌
  with a reason for any incomplete item.

## Step 6 — Confirm completion

List every file that was modified or created, and end with:

> Foundation documents are updated. Implementation notes are written.
> Ready for `/git-workflow` when you are.
