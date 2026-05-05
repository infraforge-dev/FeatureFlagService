# ef-migration-plan — Reference

## Hard stop patterns

Full detail on each hard stop the review phase checks for.

---

### DROP COLUMN / DROP TABLE

**What it looks like:**
```csharp
migrationBuilder.DropColumn(
    name: "LegacyEmail",
    table: "Users");
```

**Why it's dangerous:** The column and its data are gone the moment `Up()` runs. If the application is rolled back but the migration is not, any code referencing `LegacyEmail` will throw. If `Down()` recreates the column, it recreates it empty — the original data is unrecoverable.

**Safer approach — soft delete pattern:**
```csharp
// Step 1: Stop writing to the column (deploy app change)
// Step 2: Verify no reads in production logs
// Step 3: Add migration to drop — only after confidence period
```

**Confirm keyword:** `CONFIRM DROP`

---

### Empty or missing Down()

**What it looks like:**
```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // EF Core generated this empty — or the developer deleted it
}
```

**Why it's dangerous:** `dotnet ef database update [PreviousMigration]` silently does nothing. The migration appears rolled back in the migrations history table but the schema change remains. The database and the migration history are now out of sync.

**Fix:** Always implement `Down()` or explicitly document why rollback is not possible and what the manual recovery procedure is.

**Confirm keyword:** `CONFIRM IRREVERSIBLE`

---

### Multi-statement migration without a transaction

**What it looks like:**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(...);   // statement 1
    migrationBuilder.CreateIndex(...);          // statement 2
    migrationBuilder.Sql("UPDATE ...");         // statement 3
}
```

**Why it's dangerous:** EF Core wraps migrations in a transaction by default — but only if the provider supports transactional DDL. SQL Server supports it. Postgres supports it. SQLite has limitations. If any statement fails mid-migration and no transaction is present, the schema is left in a partially applied state that neither matches the previous nor the new version.

**Check:** Confirm `suppressTransaction: false` is not set anywhere in the migration. If raw SQL (`migrationBuilder.Sql(...)`) is present, verify the SQL itself is transactional.

**Confirm keyword:** `CONFIRM NO-ROLLBACK`

---

### Non-nullable column, no default, existing rows

**What it looks like:**
```csharp
migrationBuilder.AddColumn<string>(
    name: "TenantId",
    table: "Users",
    nullable: false);   // no defaultValue, no defaultValueSql
```

**Why it's dangerous:** This will throw at runtime against any table with existing rows. EF Core generates this when you add a required property without a default — it is valid for empty tables and will silently destroy your deployment against a populated one.

**Fix options:**

Option 1 — add a temporary default, backfill, then remove default:
```csharp
// Migration 1: add nullable
migrationBuilder.AddColumn<string>(name: "TenantId", table: "Users", nullable: true);

// Migration 2 (after backfill job runs): add constraint
migrationBuilder.AlterColumn<string>(name: "TenantId", table: "Users", nullable: false);
```

Option 2 — add with a sentinel default value:
```csharp
migrationBuilder.AddColumn<string>(
    name: "TenantId",
    table: "Users",
    nullable: false,
    defaultValue: "default-tenant");
// Then backfill real values, then optionally remove the default
```

**Confirm keyword:** `CONFIRM BREAKING`

---

## Zero-downtime migration patterns

Common schema changes that require multi-step deployment when the application runs continuously.

---

### Adding a non-nullable column

**Naive (breaks zero-downtime):**
Single migration adds non-nullable column. Old app version doesn't write the column → constraint violation.

**Safe sequence:**
```
Step 1 — Migration: add column as nullable
Step 2 — Deploy: new app version that writes the column (handles both null and non-null reads)
Step 3 — Backfill job: populate null rows
Step 4 — Migration: add NOT NULL constraint
Step 5 — Deploy: old compatibility code can be removed
```

---

### Renaming a column

**Naive (breaks zero-downtime):**
Single migration renames column. Old app reads old name → column not found.

**Safe sequence:**
```
Step 1 — Migration: add new column (nullable)
Step 2 — Migration or trigger: sync writes from old column to new
Step 3 — Deploy: app writes to both columns, reads from new
Step 4 — Backfill: copy remaining data old → new
Step 5 — Deploy: app reads and writes only from new column
Step 6 — Migration: drop old column (with CONFIRM DROP)
```

---

### Adding a large table index

**Naive (blocks zero-downtime):**
Standard `CREATE INDEX` takes a table lock on SQL Server. Long-running on large tables.

**SQL Server safe approach:**
```csharp
migrationBuilder.Sql(
    "CREATE INDEX CONCURRENTLY IX_Users_TenantId ON Users (TenantId)",
    suppressTransaction: true  // CONCURRENTLY cannot run inside a transaction
);
```

**Postgres safe approach:**
```sql
CREATE INDEX CONCURRENTLY IX_Users_TenantId ON "Users" ("TenantId");
```
Note: `CONCURRENTLY` is Postgres-only. Must run outside a transaction (`suppressTransaction: true`).

**SQLite:** No concurrent index creation. SQLite is typically not used in high-concurrency production scenarios where this matters.

---

### Dropping a column

**Safe sequence (soft delete pattern):**
```
Step 1 — Deploy: remove all reads and writes to the column in application code
Step 2 — Monitor: verify no column references in production logs (confidence period)
Step 3 — Migration: drop the column (requires CONFIRM DROP)
```

Never skip the confidence period. Code references in scheduled jobs, background services, or rarely-triggered paths are the ones that bite you.

---

## Provider-specific behavior

### SQL Server

- Supports transactional DDL — migrations are wrapped in a transaction by default ✅
- `CREATE INDEX` takes a shared lock — use `WITH (ONLINE = ON)` for large tables
- `ALTER COLUMN` to change type or nullability takes a schema lock
- No `CONCURRENTLY` keyword

```csharp
// Online index creation on SQL Server
migrationBuilder.Sql(
    "CREATE INDEX IX_Users_TenantId ON Users (TenantId) WITH (ONLINE = ON)",
    suppressTransaction: true
);
```

### PostgreSQL (Npgsql)

- Supports transactional DDL ✅
- `CREATE INDEX CONCURRENTLY` — non-blocking, must run outside a transaction
- `ALTER TABLE ... ALTER COLUMN` — some operations rewrite the table (adding NOT NULL without a default)
- Enum type changes require special handling

```csharp
// Concurrent index on Postgres
migrationBuilder.Sql(
    @"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_Users_TenantId"" ON ""Users"" (""TenantId"")",
    suppressTransaction: true
);
```

### SQLite

- Limited DDL support — `ALTER TABLE` only supports `ADD COLUMN` and `RENAME`
- No `DROP COLUMN` support before SQLite 3.35 (2021) — EF Core works around this by recreating the table
- Table recreations are safe for dev/test but should never surprise you in production
- Typically not used in high-concurrency production scenarios

---

## Deployment script generation

### Generate a script for a specific migration range

```bash
# From the last applied migration to the target
dotnet ef migrations script [FromMigration] [ToMigration] \
  --output deploy-[MigrationName].sql \
  --idempotent \
  --project src/YourProject \
  --startup-project src/YourProject
```

`--idempotent` generates a script that checks whether each migration has already been applied before running it. Always use this for production deployments.

### Generate a full schema script (from scratch)

```bash
dotnet ef migrations script \
  --output full-schema.sql \
  --idempotent \
  --project src/YourProject
```

### Apply the migration directly (non-production only)

```bash
# Dev and staging only — never run dotnet ef database update against production
dotnet ef database update [MigrationName] \
  --project src/YourProject \
  --connection "your-connection-string"
```

**Production rule:** Always use a generated SQL script applied by your deployment pipeline, not `dotnet ef database update`. The script is reviewable, auditable, and can be dry-run. `database update` cannot.

---

## Rollback reference

### Rollback via Down() — when it's safe

```bash
# Roll back to the migration before the one you want to undo
dotnet ef database update [PreviousMigrationName] \
  --project src/YourProject
```

Only safe when:
- `Down()` is fully implemented
- No data was dropped or transformed irreversibly
- The application version being rolled back to does not reference the new schema

### Rollback via script

```bash
# Generate a rollback script (runs Down() for the range)
dotnet ef migrations script [CurrentMigration] [PreviousMigration] \
  --output rollback-[MigrationName].sql \
  --idempotent
```

Generate and review the rollback script *before* deploying, not after something goes wrong.

### When rollback is not possible

Document this explicitly in `MIGRATION_PLAN.md` before the migration is applied:

```
## Rollback plan
This migration drops column Users.LegacyEmail. Rollback is not possible without
restoring from backup. Rollback window: 30 minutes post-deployment. Backup
location: [backup identifier].
```

---

## Migration naming conventions

EF Core generates timestamp-based names by default. Always rename to something meaningful before committing.

```bash
# Generated (bad)
dotnet ef migrations add 20260415143200_AddTenantId

# Rename immediately
# In the Migrations/ folder, rename the file and update the class name
# Good names describe the change, not the date
AddTenantIdToUsers
DropLegacyEmailColumn_Phase3
AddIndexOnUsersEmail
```

The timestamp lives in the migration snapshot — the class name is what developers read in `git log`, code review, and incident reports.
