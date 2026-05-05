---
name: ef-migration-plan
description: Plan, review, and safely deploy EF Core migrations. Three gated phases: plan (before dotnet ef migrations add), review (audit an existing migration file for data loss and deployment risk), and deploy (generate a safe deployment script + checklist). Detects zero-downtime violations and rewrites single migrations into safe multi-step sequences. Use when the user says "plan a migration", "review my migration", "is this migration safe", "how do I deploy this migration", or "zero-downtime migration".
---

# ef-migration-plan

Plan before you add. Review before you ship. Deploy with a checklist, not just a command.

See [REFERENCE.md](REFERENCE.md) for hard stop patterns, zero-downtime sequences, and provider-specific behavior.

## Philosophy

EF Core will generate whatever migration you ask for. It does not know if you have ten rows or ten million. It does not know if your column has nulls. It does not know if your deployment window is two minutes. This skill does.

Conservative by default. Every hard stop can be overridden — but only by a conscious human decision, never silently.

---

## Step 0 — Check for project documentation

Before reading any code, ask:

> "Do you have project docs — architecture notes, current state, 
> or a roadmap? If so, where do they live?"

If docs exist, read them first. They may answer questions the 
code can't. Then proceed with the trail below.

## Step 1 — Detect entry point

Ask or infer from context:

- No migration file yet → **Plan phase**
- Migration file exists, not yet deployed → **Review phase**
- Migration reviewed, ready to ship → **Deploy phase**
- Unclear → ask: *"Where are you in the process — planning a change, reviewing a migration file, or preparing to deploy?"*

## Step 2 — Follow the trail

Read in this order before any phase begins:

1. `.csproj` — is EF Core installed? Which provider? (`Npgsql`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Sqlite`). Provider changes the advice.
2. `DbContext` — current intended schema: entities, relationships, Fluent API configurations
3. Entity model files — understand what the domain looks like before looking at migrations
4. `Migrations/` folder — schema history, how this team writes migrations, what's already been applied
5. Any existing `MIGRATION_PLAN.md` or deployment docs — don't duplicate work already done

---

## Plan phase

*The developer is about to run `dotnet ef migrations add`. Nothing exists yet.*

### 1. Interview

Ask one at a time:

1. What is changing? (new table, new column, drop column, rename, index, constraint)
2. How many rows are in the affected table(s) in production? (approximate is fine — order of magnitude matters)
3. Is the application deployed continuously, or is there a maintenance window available?
4. Does the change require existing data to be transformed or backfilled?

### 2. Assess risk

Based on the answers, classify the migration:

| Risk level | Criteria |
|---|---|
| 🟢 Low | Additive only, nullable, no existing data affected, no backfill |
| 🟡 Medium | Non-nullable column, index on large table, data backfill required |
| 🔴 High | DROP operation, rename, constraint on existing rows, no maintenance window |

### 3. Detect zero-downtime violations

If the application runs continuously (no maintenance window), check whether the planned change requires a multi-step deployment. See REFERENCE.md for the full pattern catalogue.

If a violation is detected, propose the safe multi-step sequence *before* the developer runs `dotnet ef migrations add`. It is always cheaper to plan correctly upfront than to split a migration after the fact.

### 4. Produce MIGRATION_PLAN.md

Generate a plan document. Show a preview and ask: **"Should I save this as `MIGRATION_PLAN.md`?"**

Do not save without confirmation.

```
# Migration Plan — [descriptive name, not a timestamp]

## What is changing
[plain English description]

## Risk level
🟢 / 🟡 / 🔴 — [one sentence rationale]

## Affected tables
[table names and approximate row counts]

## Deployment strategy
[single migration / multi-step sequence]

## Rollback plan
[what running Down() does / what manual rollback looks like if Down() is unsafe]

## Backfill required
[yes/no — if yes, describe the backfill and when it runs]

## Checklist before running
- [ ] Backup taken
- [ ] Migration tested against a copy of production data
- [ ] Rollback window confirmed with team
- [ ] [any additional checks specific to this migration]
```

---

## Review phase

*The developer has run `dotnet ef migrations add`. The migration file exists.*

### 1. Read the migration

Read the generated `Up()` and `Down()` methods in full. Cross-reference against the `DbContext` and entity models to confirm the migration matches the intended change.

### 2. Check for hard stops

For each hard stop found, halt and present:

```
⛔ Hard stop: [what was found] in [file:line]
[One sentence explaining why this is dangerous]
[What data or behavior is at risk]

Type CONFIRM [KEYWORD] to acknowledge this risk and continue.
Or let's discuss a safer approach first.
```

Hard stops and their confirm keywords — see REFERENCE.md for full details:

| Pattern | Keyword | Risk |
|---|---|---|
| `DROP COLUMN` / `DROP TABLE` | `CONFIRM DROP` | Permanent data loss if Down() never runs |
| Empty or missing `Down()` | `CONFIRM IRREVERSIBLE` | Migration cannot be rolled back |
| Multi-statement with no transaction | `CONFIRM NO-ROLLBACK` | Partial failure leaves schema in broken state |
| Non-nullable column, no default, existing rows | `CONFIRM BREAKING` | Will fail at runtime against non-empty table |

All confirmed overrides are recorded in the review report.

### 3. Check for warnings

Surface these without blocking — one paragraph each in the report:

- Non-nullable column with a default value that may not match domain rules
- Index added without explicit uniqueness intent stated
- Migration touches more than two tables (elevated blast radius)
- No test that seeds data and runs the migration end-to-end
- `Down()` exists but does not fully reverse `Up()` (partial rollback)

### 4. Detect zero-downtime violations

Same check as plan phase — but now against the actual generated migration. If a violation is found, draft the corrected multi-step sequence and show it as a preview.

Ask: **"This migration needs to be split into [N] steps for zero-downtime deployment. Want me to draft the corrected migration sequence?"**

### 5. Produce the review report

Save as `MIGRATION_REVIEW.md` alongside the migration file. Show preview first.

```
# Migration Review — [MigrationName]

## Summary
[One paragraph: what the migration does, overall risk assessment]

## Hard stops
[List of hard stops found, confirm keywords used, developer acknowledgments]

## Warnings
[List of warnings with brief explanation each]

## Zero-downtime assessment
[Safe / Needs splitting — if splitting required, link to proposed sequence]

## Recommended next step
[Proceed to deploy / Fix before deploying / Split migration first]
```

---

## Deploy phase

*The migration is reviewed and ready to ship.*

### 1. Confirm prerequisites

Ask or verify:
- Has the migration been reviewed? (Is there a `MIGRATION_REVIEW.md`?)
- Which environment? (dev / staging / production)
- Is a database backup in place?
- Is the application being deployed alongside the migration, or separately?

### 2. Generate the deployment script

Use `dotnet ef migrations script` with the correct flags for the target environment. See REFERENCE.md for provider-specific script generation commands.

Show the script as a preview. Ask: **"Should I save this as `deploy-[MigrationName].sql`?"**

### 3. Produce the deployment checklist

Alongside the script, generate a process checklist:

```
# Deployment Checklist — [MigrationName]

## Before deploying
- [ ] Backup confirmed: [database name, timestamp]
- [ ] Migration reviewed: MIGRATION_REVIEW.md present
- [ ] Script generated and reviewed: deploy-[MigrationName].sql
- [ ] Staging deployment successful
- [ ] Rollback window confirmed: [duration]
- [ ] Team notified

## Deployment steps
[Numbered sequence — script first or app first, depending on migration type]

## After deploying
- [ ] Smoke test passed
- [ ] Row counts verified on affected tables
- [ ] Application logs clean (no EF Core errors)
- [ ] Rollback window closed

## Rollback procedure
[What to run / do if something goes wrong — specific to this migration]
```

Show preview. Ask: **"Should I save this as `deploy-[MigrationName]-checklist.md`?"**
