---
name: spec
description: >
  Drive a structured interview with the user to reach shared understanding of a feature,
  then produce a specification document (spec.md) placed in
  Docs/decisions/<feature-branch-name>/spec.md. Use this skill whenever the user says
  "/spec", "let's spec this out", "write the spec", "create a spec", or is ready to
  define a feature after a session-start orientation. Large features may produce multiple
  phased spec files named spec-NN-<slug>.md (e.g. spec-01-create-flag.md).
---

**Skills referenced

# Spec

Produce a complete, approvable specification for the next feature. Do not write code.
Do not suggest implementation details beyond what the spec requires. Your only output
is the spec file(s) and a confirmation prompt.

## Step 1 — Establish scope

Before the interview begins, confirm:

1. What feature or task are we speccing? (pull from session-start if already known)
2. Is this one spec or does the scope warrant splitting into multiple files?
   - Single spec: one bounded feature, one branch, one PR
   - Multiple specs: split when the work is large enough that a single spec plus its
     implementation context would consume more than ~40% of the model's context window.
     This is a guideline, not a hard limit — also split when the work has natural
     phase boundaries that benefit from independent verification or parallel agents.

When splitting, each file is a **tracer-bullet phase**: a thin vertical slice that
cuts end-to-end through every layer it touches (domain → API → tests, etc.) and is
individually testable and demoable. Do NOT split horizontally by layer
(`spec-1` = all domain, `spec-2` = all API) — that produces unverifiable intermediate
states and blocks parallel work. Instead, split by user-visible capability:
- `spec-01-create-flag.md` — create + read a flag end-to-end
- `spec-02-toggle-flag.md` — toggle on/off end-to-end
- `spec-03-archive-flag.md` — archive end-to-end

This shape is what lets a Ralph loop pick up phases in parallel later.

Filename convention: `spec-NN-<kebab-slug>.md` with a zero-padded ordering prefix and
a short slug naming the slice. The folder already encodes the feature via the branch
name, so the slug describes the slice, not the feature.

Confirm the branch name and PR number if known. Use the format:
`Docs/decisions/<feature-branch-name>/spec.md` for a single spec, or
`Docs/decisions/<feature-branch-name>/spec-NN-<slug>.md` for phased specs.

## Step 2 — Conduct the interview (grill-me style)

Use the /grill-me skill

Ask questions one at a time. For each question, provide your recommended answer so the
user can confirm, correct, or build on it rather than answering from scratch.

Cover every section of the spec template in order:

- **User story** — who benefits and what value is delivered
- **Background and goals** — why this exists, what problem it solves today
- **Design decisions** — at least 3 non-obvious choices with explicit tradeoffs
- **Architecture** — what new components exist, what layer boundaries are crossed,
  does this need a Mermaid diagram?
- **Scope** — every file that will be created or modified (this is the contract)
- **Acceptance criteria** — behavioral correctness, given/when/then, including error paths
- **Technical notes** — packages, build sequence, known pitfalls, ADR references
- **Out of scope** — what is explicitly deferred and to which phase
- **Learning opportunities** — 2–3 .NET-specific concepts this feature exercises
- **DX / Tooling idea** — one small, buildable developer experience improvement
- **Definition of Done** — binary checklist items, including build and test gates

Do not move to the next section until the current one is resolved. If a question can
be answered by reading the codebase or existing foundation docs, do that instead of
asking.

## Step 3 — Write the spec file(s)

Using the confirmed answers, produce the spec using this template structure:

```
# Specification: <Feature Name>

**Document:** Docs/decisions/<feature-branch-name>/spec.md
**Status:** Draft
**Branch:** <branch>
**PR:** TBD
**Phase:** <phase number and name>
**Depends on:** <spec name or "None">
**Author:** <from current-state.md or ask>
**Date:** <today>

## Table of Contents
## User Story
## Background and Goals
## Design Decisions
## Architecture Overview
## Scope
## Acceptance Criteria
## File Layout
## Technical Notes
## Out of Scope
## Learning Opportunities
## DX / Tooling Idea (only offer if you identify particular pain-points in the dev workflow.)
## Definition of Done
```

Every section must be populated. "N/A" is only acceptable in "DX / Tooling Idea" if
genuinely nothing applies. An empty Design Decisions section means the interview is
not done — go back.

## Step 4 — Confirm before closing

Present the spec and end with:

> This is the spec. Review it carefully — once you approve, reply "approved" to
> invoke `/implement`. Implementation does not begin until you explicitly approve.

Do not begin implementation. Do not update any foundation documents. Wait for explicit
approval.

## Split spec rules

When splitting into multiple files:
- Each spec file is independently approvable and implementable
- `spec-02-toggle-flag.md` must list `spec-01-create-flag.md` in its **Depends on** field
- Each has its own DoD — do not mix checklist items across files
- Both files live in the same `Docs/decisions/<feature-branch-name>/` folder
