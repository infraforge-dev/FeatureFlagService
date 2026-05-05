---
name: session-start
description: >
  Orient the agent at the beginning of a work session by reading the project's foundation
  documents and surfacing what should be worked on next. Use this skill whenever the user
  says "session start", "let's get started", "what are we working on", "orient yourself",
  or begins a session without a specific task. Always invoke this before any spec or
  implementation work begins. 
---

# Session Start

You are orienting yourself at the beginning of a development session. Do this before
anything else — no spec, no code, no suggestions until this is complete.

## Step 1 — Read the foundation documents

Read all three in this order. They are the single source of truth for the project state.

1. `docs/architecture.md` — understand the system shape, tech stack, layer boundaries,
   and any patterns or guardrails that must be respected
2. `docs/current-state.md` — find the current phase, what's completed, what's not yet
   built, and any open known issues
3. `docs/roadmap.md` — confirm the phase sequence and what the current phase's success
   metric is

If any of these files is missing, tell the user which one is absent and ask them to
provide it or confirm the path before continuing.

## Step 2 — Produce the orientation summary

Output exactly these four sections — no more, no less:

### Where we are
2–3 sentences. Current phase name and number, what was most recently completed, and
the overall project health (green / has blockers / needs attention).

### Next recommended task
The single most important next task based on the current phase's Definition of Done
in `current-state.md`. Name the specific file(s), class(es), or behavior involved.
If the DoD is fully checked, name the next phase's first task from `roadmap.md`.

### Open known issues
List any KIs from `current-state.md` that are Open or Mitigated — severity and one-line
description. If none, write "None."

### What not to touch
Pull the "What Not To Do Right Now" guardrails from `current-state.md` verbatim.
These are active constraints. The user and agent must not violate them in this session.

## Step 3 — Confirm and hand off

End with this exact prompt:

> Ready. Does this match what you had in mind, or do you want to work on something
> different? If you're ready to spec, invoke `/spec`.

Do not begin a spec, write code, or make any suggestions about implementation
until the user confirms or redirects.
