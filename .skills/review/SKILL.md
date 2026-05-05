---
name: review
description: Load-bearing HITL gate before push. Produces a structured review report anchored to PRD success criteria (or chore intent), checks pre-push feedback loops, and emits a decision. The report becomes the PR body on push. Use when the user says "/review", "review the branch", or "ready to push".
---

# review

The third and final HITL gate. Nothing reaches the remote without this passing. The report this skill produces becomes the PR body on push — write it for the reviewer who has not seen the AFK loop.

Targets:
- **Feature review.** `/review features/<slug>` — anchors against `features/<slug>/prd.md` success criteria + `features/<slug>/changelog.md`.
- **Chore review.** `/review chores/<id>` — anchors against the chore's Intent + ACs.

If no target is given, infer from the current branch / recent commits and confirm before proceeding.

---

## Step 1 — Gather inputs

For a feature target:

- `features/<slug>/prd.md` — the criteria spine.
- `features/<slug>/changelog.md` — the synthesis from `wrap-up`. If missing, stop and tell the user to run `/wrap-up` first.
- `features/<slug>/issues/done/*.md` — to confirm coverage.
- Diff vs `main`: `git diff main...HEAD` (or the configured base branch).
- `docs/architecture.md`, `docs/conventions.md` — for drift detection.

For a chore target:

- `chores/done/<id>.md` (or `chores/<id>.md` if not yet moved).
- Diff vs `main`.
- Foundation docs.

---

## Step 2 — Coverage check

For each PRD success criterion (or, for a chore, each AC):

- Locate the implementing files / tests / interfaces.
- Decide ✅ (covered with evidence), ⚠️ (partially covered, gaps named), or ❌ (not covered).
- For ⚠️ and ❌, name what's missing in concrete terms — file, behavior, test.

Be honest about ⚠️ and ❌. The whole point of this gate is catching them before push.

---

## Step 3 — Scope-outside-criteria check

Walk the diff. Any change that does not trace to a criterion (or chore AC) goes into the "Scope outside the criteria" section. For each:

- Mark as `intentional` (e.g., a tooling tweak that enabled the slice) or `scope creep candidate`.
- Scope creep candidates require user input at the decision step.

---

## Step 4 — Foundation-doc drift

Run terminology and convention checks:

- Invoke the `ubiquitous-language` skill's drift-detection logic against the diff. Surface any new domain term that contradicts existing language, or any old term that has shifted in meaning.
- Compare changes against `docs/conventions.md` (formatting, pinned versions, DVR rules) and `docs/architecture.md` (layer rules, allowed dependencies).
- If a change is *intentional* and the foundation doc is now wrong, propose the doc edit inline in the report — do not silently apply it.

If neither doc exists yet, note it and skip without failing.

---

## Step 5 — Pre-push feedback loops

Run, in this order, and capture the result:

- `dotnet build`
- `dotnet test --filter "Category!=Integration"`
- `dotnet format --verify-no-changes`

Any failure → decision is `block` regardless of coverage.

Then run `git-guardrails-claude-code` against the diff and surface anything destructive (history rewrites, force-push intent, deletions of unrelated files).

---

## Step 6 — Emit the report

Write the report to stdout (and, on push, to the PR body). Use this template verbatim:

```markdown
# Review — <feature-slug or chore-id>

## Coverage of success criteria
- ✅/⚠️/❌ <criterion text>
  - Evidence: <files/lines>
  - Concerns: <none | list>

## Scope outside the criteria
<files/lines not traced to any criterion; "intentional" or "scope creep candidate">

## Foundation-doc drift
<empty by default; proposed edits to architecture.md / conventions.md inline>

## Pre-push checklist
- [ ] dotnet build clean
- [ ] dotnet test --filter "Category!=Integration" passing
- [ ] dotnet format --verify-no-changes clean
- [ ] git-guardrails-claude-code reviewed any destructive ops

## Decision
<approve | request changes | block>
```

Decision rules:

- **block** — any feedback loop is red, or guardrails flag a destructive op the user has not confirmed.
- **request changes** — at least one ❌ criterion, or scope creep the user has not yet decided on.
- **approve** — every criterion ✅ (or ⚠️ explicitly accepted by user), every loop green, no unresolved scope creep, no unconfirmed drift.

Never auto-promote to `approve`. The user has to read the report and say go.

---

## Step 7 — On approve, push and open PR

Only when the user types something equivalent to "approved, push it":

1. Push the branch (`git push -u origin <branch>` if untracked, otherwise plain `git push`).
2. Open a PR with `gh pr create`. Use the report from Step 6 as the body verbatim. Title is the feature title or chore title.
3. Update PRD frontmatter `status: review` → `status: done` (feature only).
4. Print the PR URL.

If anything in this step fails, stop and surface the failure. Do not retry destructively (no force-pushing to recover, no `--no-verify`).

---

## What this skill does NOT do

- It does not bypass red feedback loops. There is no override flag.
- It does not silently edit `docs/architecture.md` or `docs/conventions.md`. Drift edits are proposed inline; the user applies them.
- It does not invoke ralph or write issues. By the time `/review` runs, the work is supposed to be done.
