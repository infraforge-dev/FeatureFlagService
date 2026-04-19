# feature/azure-keyvault-integration — Implementation Notes
**Session date:** 2026-04-19
**Branch:** `feature/azure-keyvault-integration`
**Spec reference:** `Docs/Decisions/azure-key-valt-integration - PR#50/spec.md`
**Build status:** Passed — 0 warnings, 0 errors
**Tests:** 113/113 passing
**PR:** #50

## Table of Contents
- [Summary](#summary)
- [Implemented Scope](#implemented-scope)
- [Implementation Notes](#implementation-notes)
- [Spec Deviations](#spec-deviations)
- [Verification](#verification)

## Summary
This PR wired Azure Key Vault as a configuration provider in `Banderas.Api`,
loading secrets into `IConfiguration` at startup before any service registrations
run. The connection string is now sourced from Key Vault when a vault URI is
present, with a transparent fallback to `appsettings.Development.json` for local
development without Azure access. The integration test factory was also updated to
isolate tests from the Key Vault credential chain.

## Implemented Scope
- Added to `Banderas.Api/Banderas.Api.csproj`:
  - `Azure.Extensions.AspNetCore.Configuration.Secrets` — provides `AddAzureKeyVault()`
  - `Azure.Identity` — provides `DefaultAzureCredential`
- Updated `Banderas.Api/Program.cs` to read `Azure:KeyVaultUri` from configuration
  and conditionally call `builder.Configuration.AddAzureKeyVault()` with
  `DefaultAzureCredential` before any service registrations.
- Updated `Banderas.Api/appsettings.json` to include `"Azure": { "KeyVaultUri": "" }`
  as a non-secret, empty-default placeholder.
- Updated `Banderas.Api/appsettings.Development.json` to include
  `Azure:KeyVaultUri` pointing to `kv-banderas-dev` for local development.
  The local Docker connection string remains as a fallback for developers without
  Azure CLI access.
- Updated `Banderas.Tests.Integration/Fixtures/BanderasApiFactory.cs` to:
  - Call `builder.UseEnvironment("Testing")` to prevent `appsettings.Development.json`
    from loading during tests, which would otherwise inject the real vault URI and
    trigger `DefaultAzureCredential` in a context with no Azure identity.
  - Explicitly call `DatabaseSeeder.SeedAsync(reset: false)` in `InitializeAsync`
    after migration, since the seeder is now only invoked by `Program.cs` inside
    the `IsDevelopment()` guard.

## Implementation Notes
The implementation stays strictly aligned with the spec's intended architecture:

- Key Vault is added as a **configuration provider**, not a service. All consumers
  (EF Core, etc.) read `IConfiguration["ConnectionStrings:DefaultConnection"]`
  unchanged — they have no knowledge of Key Vault. This is Dependency Inversion
  applied to configuration.
- The `AddAzureKeyVault()` call is placed before `builder.Services` registrations,
  so secrets are available when EF Core reads the connection string during DI setup.
  Placing it after `builder.Build()` would make secrets permanently unavailable to
  the container.
- The null/empty guard (`string.IsNullOrWhiteSpace(keyVaultUri)`) provides the
  fail-silent local fallback (DD-3) without any environment-specific branching.
  When the URI is absent, the block is skipped entirely.
- The `--` to `:` secret naming convention is handled automatically by the provider.
  No custom mapping code was needed for `ConnectionStrings--DefaultConnection` →
  `ConnectionStrings:DefaultConnection`.

## Spec Deviations
One area of unspecified implementation work was required:

1. **`BanderasApiFactory.cs` changes were not in the spec's file layout.** The spec
   listed only `Banderas.Api.csproj`, `Program.cs`, and the two `appsettings`
   files. However, adding Key Vault to the startup pipeline introduced two
   integration test failures that required factory changes:
   - `DefaultAzureCredential` was being invoked during tests because the
     "Development" environment caused `appsettings.Development.json` to load,
     injecting the real vault URI. Fixed with `builder.UseEnvironment("Testing")`.
   - The seed data test (`SeedDataStartupTests`) began failing because seeding runs
     inside `if (app.Environment.IsDevelopment())` in `Program.cs`. With the
     environment changed to "Testing", the seeder no longer ran automatically.
     Fixed by calling `DatabaseSeeder.SeedAsync()` explicitly in `InitializeAsync`.

   These changes were necessary to maintain the 113-test passing baseline required
   by AC-7 and do not alter any production behavior.

## Verification
The implementation was verified with:

```bash
dotnet csharpier format .
dotnet build Banderas.sln
dotnet test Banderas.sln --filter "Category=Unit"
dotnet test Banderas.sln --filter "Category=Integration"
dotnet csharpier check .
```

Final result:
- Build: passed with 0 warnings / 0 errors
- Unit tests: passing
- Integration tests: passing
- Total: 113/113 passing
