---
name: git-guardrails-claude-code
description: >
  Install the CLAUDE.md guardrails block and wire safe-exec as the command enforcement
  layer for a project. Prevents an AI agent from running destructive git, EF Core, or
  filesystem commands without explicit tiered confirmation. Use when setting up a new
  project for AI-assisted development, or when an agent has shell/terminal access to
  a .NET codebase.
---

# Git Guardrails for Claude Code

This skill installs two things into your project:

1. A `## Guardrails` block in `CLAUDE.md` — tells the agent its safety boundaries
   at session start, before any code is read.
2. A reference to `safe-exec` — the runtime enforcement layer that classifies every
   shell command against `DANGEROUS_COMMANDS.md` before it runs.

These two layers work together:
- `CLAUDE.md` sets expectations at session start (declarative)
- `safe-exec` enforces those expectations at runtime (programmatic)

---

## Step 1 — Install the CLAUDE.md block

Paste this into the **top** of your project's `CLAUDE.md` under a `## Guardrails` heading.
The agent reads `CLAUDE.md` at the start of every Claude Code session.

```md
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
```

---

## Step 2 — Place DANGEROUS_COMMANDS.md at the project root

Copy `DANGEROUS_COMMANDS.md` from the skills directory into the root of your project.
This is the editable tier list — adjust it to match your environment and risk tolerance.

```sh
cp ~/.claude/skills/safe-exec/../DANGEROUS_COMMANDS.md ./DANGEROUS_COMMANDS.md
```

The file is yours to edit. Add patterns, move tiers, adjust unlock phrases.
Changes take effect on the next Claude Code session (or when the agent re-reads the file mid-session).

---

## Step 3 — Verify the install

Start a new Claude Code session and run:

> "What are your guardrails for this project?"

Expected response: the agent reads `CLAUDE.md`, then `DANGEROUS_COMMANDS.md`,
and recites the Tier 0 blocked commands plus confirms it will route through safe-exec
before executing anything destructive.

If the agent cannot find `DANGEROUS_COMMANDS.md`, it should say so immediately
rather than proceeding with unknown safety boundaries.

---

## How safe-exec and CLAUDE.md work together

```
Session start
    └── Agent reads CLAUDE.md
            └── Learns the tier model and Tier 0 summary
            └── Knows to route through safe-exec at runtime

Runtime (any shell command)
    └── Calling skill invokes safe-exec
            └── safe-exec reads DANGEROUS_COMMANDS.md
            └── Classifies the command
            └── Returns: PROCEED / WARN / LOCKED / BLOCKED
            └── Calling skill respects the outcome
```

CLAUDE.md is the orientation. safe-exec is the enforcement.
Neither works without the other in place.
