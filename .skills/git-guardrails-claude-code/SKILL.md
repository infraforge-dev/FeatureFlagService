---
name: git-guardrails-claude-code
description: Prevent an AI agent from running destructive git or EF Core migration commands without explicit confirmation. Paste the guardrails block into your project's CLAUDE.md. Use when setting up a new project for AI-assisted development, or when an agent has shell/terminal access to a .NET codebase.
---

# Git Guardrails for Claude Code

This skill generates a guardrails block for your `CLAUDE.md`. It tells the agent what it must never run autonomously and what requires your explicit sign-off first.

## How to install

Run this skill and paste the output block into the top of your project's `CLAUDE.md` under a `## Guardrails` heading. The agent reads `CLAUDE.md` at the start of every session.

---

## The guardrails block

Copy this into `CLAUDE.md`:

```md
## Guardrails

### Never run without explicit confirmation

**Git — destructive history operations:**
- `git push --force` or `git push -f` — rewrites remote history, breaks teammates
- `git reset --hard` — permanently discards uncommitted changes
- `git clean -fd` — deletes all untracked files (unrecoverable)
- `git rebase` on any branch that has been pushed to remote
- `git commit --amend` on any commit that has already been pushed

**EF Core — database operations:**
- `dotnet ef database update` — always confirm which connection string is active first
- `dotnet ef database update 0` — rolls back ALL migrations, drops all tables
- `dotnet ef database drop` — destroys the database
- `dotnet ef migrations remove` — deletes the last migration (catastrophic if already applied to any environment)

### Always pause and show before running

- `dotnet ef migrations add <Name>` — generate the migration, show me the `.cs` file, wait for review before applying anything
- Any command targeting a non-Development environment (staging, production connection strings)
- Any `git push` to `main` or `master`

### Safe to run autonomously

- `dotnet build`, `dotnet test`, `dotnet run`
- `dotnet ef migrations list`, `dotnet ef dbcontext info`
- `git status`, `git log`, `git diff`, `git stash list`
- `git checkout -b <branch>`, `git add`, `git commit` (new commits only)
- `git push origin <feature-branch>` (non-protected branches only)
```

---

## Why each category exists

**`dotnet ef migrations remove`** is the sneakiest one. It looks harmless — just undoing the last migration. But if that migration has already been applied to your dev database (or worse, staging), you now have a schema that no longer matches your migration history. The only way out is manual SQL surgery.

**`dotnet ef database update` without checking the connection string** is how you accidentally run a migration against production while thinking you're on localhost. Always run `dotnet ef dbcontext info` first to see which connection string is active.

**`git reset --hard`** with an agent at the wheel is particularly dangerous because the agent may not distinguish between "throw away this one bad change" and "throw away the last three hours of work."

---

## Confirm the install worked

After adding to `CLAUDE.md`, start a new Claude Code session and ask:

> "What are your guardrails for this project?"

The agent should recite the blocked commands back to you. If it can't, the block isn't being read.
