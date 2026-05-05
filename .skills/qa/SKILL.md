---
name: qa
description: Interactive QA session where the user reports bugs conversationally and the agent files durable, domain-language GitHub issues after user review. Use when user wants to do QA, report bugs conversationally, file issues from a testing session, or mentions "QA session".
---

# QA Session

Run an interactive QA session. The user describes problems in plain language. You clarify, explore the codebase for context, draft issues using the project's domain language, and file them after user review.

See [REFERENCE.md](REFERENCE.md) for issue templates.

## For each issue the user raises

### 1. Listen and lightly clarify

Let the user describe the problem in their own words. Ask **at most 2-3 short questions** focused on:

- What they expected vs what actually happened
- Steps to reproduce (if not obvious)
- Whether it's consistent or intermittent

Do NOT over-interview. If the description is clear enough to draft an issue, move on.

### 2. Explore the codebase

**If running in Claude Code**: kick off a background Agent (subagent_type=Explore) while talking to the user.

**If running in Claude.ai**: explore the relevant area directly before drafting.

Goal is NOT to find a fix — it's to:
- Learn the domain language used in that area (check `UBIQUITOUS_LANGUAGE.md` if it exists)
- Understand what the feature is supposed to do
- Identify the user-facing behavior boundary

The issue should NOT reference specific files, line numbers, or internal implementation details.

### 3. Single issue or breakdown?

Decide before drafting:

**Break down when:**
- The report spans multiple independent areas that could be worked in parallel
- There are clearly separable failure modes

**Keep as single issue when:**
- One behavior is wrong in one place
- All symptoms share the same root cause

### 4. Draft and review

Draft the issue(s) using the templates in REFERENCE.md. Show the draft(s) to the user before writing. Do NOT write files until the user approves.

Once approved, write in dependency order (blockers first) so real IDs can be referenced in `blocked-by`. Write each to `chores/<id>.md`:

```markdown
---
id: <kebab-case-id>
title: <title>
type: AFK
status: open
blocked-by: [<ids>]    # [] if none
parent-prd: null
---

<issue body from REFERENCE.md template>
```

Share file paths with blocking relationships summarized. Then ask: "Next issue, or are we done?"

### 5. Continue the session

Each issue is independent — keep going until the user says they're done.

---

## Rules for all issue bodies

- **No file paths or line numbers** — these go stale
- **Use the project's domain language** — check `UBIQUITOUS_LANGUAGE.md` if it exists
- **Describe behaviors, not code** — "the flag evaluation returns stale state" not "the cache isn't invalidated on line 42"
- **Reproduction steps are mandatory** — if you can't determine them, ask
- **Keep it concise** — a developer should be able to read the issue in 30 seconds
