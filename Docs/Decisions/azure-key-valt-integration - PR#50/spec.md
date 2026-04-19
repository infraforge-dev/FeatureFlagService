# Specification: Azure Key Vault Integration
<!-- spec-azure-keyvault-integration.md -->

**Document:** `Docs/Decisions/azure-keyvault-integration - PR#50/spec.md`
**Status:** Ready for Implementation
**Branch:** `feature/azure-keyvault-integration`
**PR:** #50
**Phase:** 1.5 — Azure Foundation
**Author:** Joe / Claude Architect Session
**Date:** 2026-04-18

---

## Table of Contents

- [User Story](#user-story)
- [Background & Goals](#background--goals)
- [Non-Goals](#non-goals)
- [Design Decisions](#design-decisions)
  - [DD-1: DefaultAzureCredential over explicit credential types](#dd-1-defaultazurecredential-over-explicit-credential-types)
  - [DD-2: Key Vault as configuration provider, not a service](#dd-2-key-vault-as-configuration-provider-not-a-service)
  - [DD-3: Fail fast in production, fall back in development](#dd-3-fail-fast-in-production-fall-back-in-development)
  - [DD-4: RBAC over Access Policies](#dd-4-rbac-over-access-policies)
- [Secret Naming Convention](#secret-naming-convention)
- [Configuration Changes](#configuration-changes)
- [Startup Wiring](#startup-wiring)
- [NuGet Packages Required](#nuget-packages-required)
- [File Layout](#file-layout)
- [Acceptance Criteria](#acceptance-criteria)
  - [AC-1: Key Vault URI in appsettings.json](#ac-1-key-vault-uri-in-appsettigsjson)
  - [AC-2: Connection string removed from appsettings](#ac-2-connection-string-removed-from-appsettings)
  - [AC-3: Key Vault wired as configuration provider](#ac-3-key-vault-wired-as-configuration-provider)
  - [AC-4: DefaultAzureCredential used for authentication](#ac-4-defaultazurecredential-used-for-authentication)
  - [AC-5: Local development fallback](#ac-5-local-development-fallback)
  - [AC-6: Fail fast in production](#ac-6-fail-fast-in-production)
  - [AC-7: App starts and connects to database](#ac-7-app-starts-and-connects-to-database)
  - [AC-8: Secret not logged or exposed](#ac-8-secret-not-logged-or-exposed)
- [Out of Scope](#out-of-scope)
- [Learning Opportunities](#learning-opportunities)
- [Known Constraints](#known-constraints)

---

## User Story

> As a developer, I want the application to retrieve secrets from Azure Key Vault
> at startup so that sensitive configuration values — specifically the PostgreSQL
> connection string — are never stored in plain text in the codebase or version
> control.

---

## Background & Goals

Currently `ConnectionStrings:DefaultConnection` is stored in
`appsettings.Development.json` in plain text. This is acceptable for local
development against a Docker Postgres instance but is not acceptable for any
environment that points to a real database.

The connection string `ConnectionStrings--DefaultConnection` has already been
added to `kv-banderas-dev` in Azure. The goal of this PR is to wire the
application to read that secret from Key Vault at startup — transparently, with
no change to how EF Core or any other consumer accesses the value.

**Key constraint:** The same `Program.cs` code must work in both local
development (no Azure context required) and in production (Managed Identity
required). `DefaultAzureCredential` is the mechanism that satisfies both.

---

## Non-Goals

- Managed Identity setup on the Container App — deferred to Phase 8 (deployment)
- Moving Application Insights connection string to Key Vault — Phase 1.5 PR #51
- Moving Azure OpenAI endpoint/key to Key Vault — Phase 1.5 PR #52
- Any UI or API surface changes
- Key rotation automation

---

## Design Decisions

### DD-1: DefaultAzureCredential over explicit credential types

`DefaultAzureCredential` tries a chain of identity sources in order:
environment variables → workload identity → Managed Identity → Visual Studio →
Azure CLI → Azure PowerShell. It uses the first one that works.

**Why this matters:**
- Locally: picks up the developer's `az login` session automatically
- In CI: can use environment variable credentials if needed
- In production: uses the Container App's Managed Identity

No credential type needs to change between environments. No secrets are stored
in code. This is the Microsoft-recommended approach for Azure SDK authentication.

**Alternative considered:** `ClientSecretCredential` — requires storing a client
secret somewhere, reintroducing the circular secret problem we're solving.
Rejected.

---

### DD-2: Key Vault as configuration provider, not a service

Azure Key Vault can be consumed two ways:
1. As a **configuration provider** — secrets are loaded into `IConfiguration`
   at startup, transparently replacing values that would otherwise come from
   `appsettings.json`
2. As a **service** — inject `SecretClient` and call `GetSecretAsync()` explicitly
   wherever a secret is needed

**We use Option 1 — configuration provider.**

This means EF Core, connection string consumers, and all other code that reads
`IConfiguration["ConnectionStrings:DefaultConnection"]` continues to work
unchanged. Key Vault becomes invisible to all consumers — they don't know or care
where the value came from.

**Why it matters for interviews:** This is the Dependency Inversion Principle
applied to configuration — consumers depend on the abstraction (`IConfiguration`),
not the concrete source (Key Vault).

---

### DD-3: Fail fast in production, fall back in development

**Local development:**
Key Vault is added to the configuration pipeline only when a `KeyVaultUri`
setting is present in configuration. If `KeyVaultUri` is absent (i.e., a
developer hasn't configured it), the app falls back gracefully to
`appsettings.Development.json`. No crash. No friction.

**Production:**
`KeyVaultUri` will be set via environment variable or `appsettings.json` (non-secret).
If Key Vault is unreachable at startup, `AddAzureKeyVault()` throws and the host
fails to build. This is intentional — **fail fast, fail loud**. An app with no
database connection string is non-functional. A silent start followed by runtime
crashes on every request is worse than a clean startup failure with a clear error
message.

---

### DD-4: RBAC over Access Policies

Azure Key Vault supports two authorization models:
- **Vault Access Policies** — legacy, vault-scoped, all-or-nothing per identity
- **Azure RBAC** — modern, fine-grained, role assignments at vault or secret level

We use **RBAC**. The identity (developer or Managed Identity) is assigned the
`Key Vault Secrets User` role, which grants read-only access to secrets. This
is the minimum privilege required and the Microsoft-recommended approach for
new vaults.

---

## Secret Naming Convention

Azure Key Vault does not support `:` in secret names. The .NET Azure Key Vault
configuration provider automatically maps `--` in secret names to `:` in
`IConfiguration`.

| Key Vault Secret Name | IConfiguration Key |
|---|---|
| `ConnectionStrings--DefaultConnection` | `ConnectionStrings:DefaultConnection` |

This mapping is automatic — no custom code required.

---

## Configuration Changes

### `appsettings.json` — Add KeyVaultUri (non-secret)

```json
{
  "Azure": {
    "KeyVaultUri": ""
  }
}
```

The URI is not a secret — it's a non-sensitive endpoint. It is safe to store in
`appsettings.json`. It will be populated via environment variable or deployment
configuration in production.

### `appsettings.Development.json` — Add KeyVaultUri for local dev

```json
{
  "Azure": {
    "KeyVaultUri": "https://kv-banderas-dev.vault.azure.net/"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Database=banderas;Username=postgres;Password=postgres"
  }
}
```

**Important:** The connection string remains in `appsettings.Development.json`
as a fallback for developers who do not have Azure CLI access. When `KeyVaultUri`
is present AND the developer is authenticated via `az login`, Key Vault overrides
the local value. When Key Vault is unreachable, the local value is used.

This is safe because `appsettings.Development.json` only contains local Docker
credentials — not production secrets.

---

## Startup Wiring

**File:** `Banderas.Api/Program.cs`

Add the following block after `var builder = WebApplication.CreateBuilder(args);`
and before any service registrations:

```csharp
// Azure Key Vault — load secrets into IConfiguration at startup
var keyVaultUri = builder.Configuration["Azure:KeyVaultUri"];

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential()
    );
}
```

**Why before service registrations?** Services like EF Core read
`IConfiguration` during registration. Key Vault must be loaded into
`IConfiguration` before any service that depends on a secret is registered.
If Key Vault is added after `builder.Build()`, the secrets are never available
to the DI container.

---

## NuGet Packages Required

Add to `Banderas.Api.csproj`:

```xml
<PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.*" />
<PackageReference Include="Azure.Identity" Version="1.*" />
```

**`Azure.Extensions.AspNetCore.Configuration.Secrets`** — provides the
`AddAzureKeyVault()` configuration extension method.

**`Azure.Identity`** — provides `DefaultAzureCredential` and the full Azure
identity chain.

---

## File Layout

No new files are introduced. Changes are limited to:

```
Banderas.Api/
├── Banderas.Api.csproj          ← Add two NuGet packages
├── Program.cs                   ← Add Key Vault configuration block
└── appsettings.json             ← Add Azure:KeyVaultUri (empty string default)

appsettings.Development.json     ← Add Azure:KeyVaultUri (dev vault URI)
```

`ConnectionStrings:DefaultConnection` is **not removed** from
`appsettings.Development.json` — it remains as a fallback for local dev
without Azure access.

---

## Acceptance Criteria

### AC-1: Key Vault URI in appsettings.json

- [ ] `appsettings.json` contains `"Azure": { "KeyVaultUri": "" }`
- [ ] `appsettings.Development.json` contains `"Azure": { "KeyVaultUri": "https://kv-banderas-dev.vault.azure.net/" }`
- [ ] Neither file contains production secrets

---

### AC-2: Connection string removed from production config

- [ ] `appsettings.json` does NOT contain `ConnectionStrings:DefaultConnection`
- [ ] `appsettings.Development.json` retains `ConnectionStrings:DefaultConnection`
  as a local fallback only

---

### AC-3: Key Vault wired as configuration provider

- [ ] `Program.cs` calls `builder.Configuration.AddAzureKeyVault()` when
  `Azure:KeyVaultUri` is non-empty
- [ ] The call is placed before any service registrations that depend on
  `IConfiguration`
- [ ] When `Azure:KeyVaultUri` is empty or absent, the block is skipped without
  error

---

### AC-4: DefaultAzureCredential used for authentication

- [ ] `new DefaultAzureCredential()` is passed to `AddAzureKeyVault()`
- [ ] No API keys, client secrets, or passwords are hardcoded anywhere
- [ ] No credential type other than `DefaultAzureCredential` is introduced

---

### AC-5: Local development fallback

- [ ] With `Azure:KeyVaultUri` set and `az login` authenticated to the correct
  tenant, the app reads the connection string from Key Vault
- [ ] With `Azure:KeyVaultUri` absent or empty, the app reads the connection
  string from `appsettings.Development.json` without error

---

### AC-6: Fail fast in production

- [ ] If `Azure:KeyVaultUri` is set and Key Vault is unreachable, the app fails
  at startup with a clear exception — it does NOT start and serve broken requests
- [ ] The exception message is sufficient to diagnose the failure (wrong URI,
  missing RBAC role, network issue)

---

### AC-7: App starts and connects to database

- [ ] `docker compose up` starts the app successfully
- [ ] The app connects to the local Postgres instance
- [ ] All 113 existing tests continue to pass
- [ ] No regression in any existing endpoint

---

### AC-8: Secret not logged or exposed

- [ ] The connection string value is not written to any log output at any log level
- [ ] `Azure:KeyVaultUri` (non-secret) may appear in logs — this is acceptable
- [ ] The connection string does not appear in any API response or error message

---

## Out of Scope

Must not be implemented in this PR:

- Managed Identity assignment on Container App (Phase 8)
- Application Insights wiring (PR #51)
- Azure OpenAI wiring (PR #52)
- Key rotation or secret versioning
- Any changes to domain, application, or infrastructure layers
- Any new API endpoints

---

## Learning Opportunities

**💡 The Configuration Pipeline in .NET**
`IConfiguration` in .NET is built from multiple layered providers — `appsettings.json`,
environment variables, user secrets, Key Vault, and more. Later providers override
earlier ones for the same key. This is why Key Vault must be added last — its values
win over `appsettings.json`. Think of it like CSS specificity, but for app config.

**💡 DefaultAzureCredential and the Credential Chain**
`DefaultAzureCredential` tries identity sources in a fixed order and uses the first
that works. This means the same code runs in dev (Azure CLI), CI (environment
variables), and production (Managed Identity) without modification. It's the Azure
SDK's implementation of the Strategy pattern.

**💡 Secret Naming: `--` vs `:`**
Colons are illegal in Key Vault secret names. The .NET Azure Key Vault provider
maps `--` → `:` automatically. This is a convention, not magic — worth knowing
because it's a common source of confusion when secrets "aren't found" in config.

**💡 Fail Fast Principle**
An app that crashes at startup with a clear error is better than one that starts
successfully but fails on every request. This is a core reliability principle —
surface failures as early as possible, as loudly as possible.

---

## Known Constraints

| Constraint | Detail | Tracked For |
|---|---|---|
| Managed Identity not yet assigned | Local dev uses `az login`; production Managed Identity assignment is a Phase 8 deployment task | Phase 8 |
| `appsettings.Development.json` committed | Contains local Docker credentials only — intentional, not a security issue | Acceptable |
| Single Key Vault per environment | One vault URI per environment. Multi-vault or multi-environment vault routing is out of scope | Phase 8+ |

---

*Banderas | feature/azure-keyvault-integration | Phase 1.5 — Azure Foundation | v1.0*
