# Known Versions
# Last verified: 2026-05-07
# Run this prompt again whenever a dependency feels stale.

This file pins claims the Specwright workflow makes about external platforms,
packages, and frameworks. Each claim is verified against official documentation
on the date noted. A `⚠️ unverified` tag means the claim could not be confirmed
from a primary source — treat it as suspect.

## .NET Runtime / SDK

- Target framework `net10.0` is the current LTS and ships .NET 10.0 GA, released 2025-11-11 with support through 2028-11-14 (verified 2026-05-07)
- .NET 10 is the LTS in the even-numbered cadence; .NET 9 (STS) reaches EOL 2026-11-10 (verified 2026-05-07)
- Nullable reference types and implicit usings remain the project-template defaults in .NET 10 (verified 2026-05-07)

## EF Core

- EF Core 10.0 is GA, released alongside .NET 10 in November 2025 (LTS, supported through 2028-11-10) (verified 2026-05-07)
- `ExecuteUpdateAsync` accepts non-expression lambdas in EF Core 10 — relevant to `ef-migration-plan` backfill guidance (verified 2026-05-07)
- Vector similarity search left experimental status in EF Core 10 (verified 2026-05-07)
- Named query filters added in EF Core 10 (verified 2026-05-07)
- `dotnet ef migrations script` and `dotnet ef database update` commands and flag set referenced by `ef-migration-plan` are unchanged in EF Core 10 ⚠️ unverified

## ASP.NET Core 10

- Built-in OpenAPI document generation supports OpenAPI 3.1 and JSON Schema 2020-12 (verified 2026-05-07)
- `WithOpenApi()` extension method (used in `dotnet-api-design` REFERENCE.md endpoint examples) is **deprecated in ASP.NET Core 10** — see Microsoft's "Breaking change: Deprecation of WithOpenApi extension method" (verified 2026-05-07). The skill's greenfield scaffold should be updated.
- Minimal APIs gained built-in validation in .NET 10; failed validation produces a ProblemDetails response automatically and integrates with `IProblemDetailsService` (verified 2026-05-07)
- `AddProblemDetails()`, `UseExceptionHandler()`, `UseStatusCodePages()`, `Results.Problem(...)` / `TypedResults.Problem(...)` APIs referenced by the skill are present and current (verified 2026-05-07)

## OpenAPI Tooling

- `Microsoft.AspNetCore.OpenApi` is the **default** package in the .NET 9+ web API template; Swashbuckle was removed from the default template in .NET 9 (verified 2026-05-07)
- Swashbuckle.AspNetCore is **not deprecated** — it remains a community package consumers can opt into. The `dotnet-api-design` REFERENCE.md treats both as acceptable, which matches official guidance (verified 2026-05-07)
- Scalar (package: `Scalar.AspNetCore`) is the recommended modern UI for OpenAPI in .NET 10; Swagger UI still works but is no longer the default (verified 2026-05-07)
- The skill's pass criteria — `Swashbuckle.AspNetCore` **or** `Microsoft.AspNetCore.OpenApi` (+ `Scalar`) in `.csproj` — is consistent with current guidance (verified 2026-05-07)

## API Versioning

- Latest stable `Asp.Versioning.Http`: **10.0.0** (verified 2026-05-07)
- Latest stable `Asp.Versioning.Mvc`: **10.0.0** (verified 2026-05-07)
- `Asp.Versioning.Mvc.ApiExplorer` 10.0.0 — required to wire versioning into OpenAPI document generation (verified 2026-05-07)
- The `AddApiVersioning(...)`, `NewApiVersionSet()`, `WithApiVersionSet()`, `MapToApiVersion()` API surface used in the scaffold is current (verified 2026-05-07)

## Validation

- FluentValidation 12.0 is current. **`FluentValidation.AspNetCore` is deprecated and unmaintained**; the auto-validation pipeline it provided was removed (verified 2026-05-07)
- `dotnet-api-design` REFERENCE.md uses `AddValidatorsFromAssemblyContaining<Program>()` (from `FluentValidation.DependencyInjectionExtensions` 12.x) — still supported (verified 2026-05-07)
- The skill should pair FluentValidation with manual validation in endpoint filters (or .NET 10's built-in minimal-API validation), **not** with `AddFluentValidation()` from the deprecated package (verified 2026-05-07)

## Testing Stack

- xUnit v3 latest stable: **3.2.2**, released 2026-01-14. Targets `net8.0` or later — fully supports .NET 10 (verified 2026-05-07)
- xUnit v2 latest stable: **2.9.3** — still supported but new projects should use v3 (verified 2026-05-07)
- The TDD reference uses generic xUnit APIs (`[Fact]`, `[Theory]`, `IClassFixture`, `IAsyncLifetime`) that work on both v2 and v3 (verified 2026-05-07)
- **FluentAssertions v8 is no longer free for commercial use** — Xceed proprietary license, $129.95/dev/year. v7 (latest under Apache 2.0) remains free indefinitely (verified 2026-05-07). The TDD reference does not pin a version; if commercial use is intended, pin to `<8.0.0` or replace with Shouldly / awaitable assertions.
- Moq latest stable: **4.20.72**, published 2024-09-07. SponsorLink was removed in 4.20.2; current versions are clean (verified 2026-05-07). NSubstitute is widely recommended as an alternative for new projects, but Moq remains technically viable.
- `Testcontainers.PostgreSql` latest: **4.11.0**. Targets `net8.0` and `netstandard2.0`; works under .NET 10 (verified 2026-05-07)
- `WebApplicationFactory<TEntryPoint>` (from `Microsoft.AspNetCore.Mvc.Testing`) — API surface used in the integration-test pattern is unchanged in .NET 10 ⚠️ unverified
- Coverlet + `dotnet test --collect:"XPlat Code Coverage"` invocation is unchanged ⚠️ unverified

## Code Style

- CSharpier latest stable: **1.2.6**, released 2025-02-06 (verified 2026-05-07)
- CSharpier supports C# 14 / .NET 10 features including extension declarations and file-level directives (verified 2026-05-07)
- The CLAUDE.md commands `dotnet csharpier .` and `dotnet csharpier --check .` match current CLI syntax (verified 2026-05-07)

## Error Shape

- ASP.NET Core's ProblemDetails implementation is compatible with both **RFC 7807** and **RFC 9457** (RFC 9457 obsoleted 7807 in July 2023). The core schema (`type`, `title`, `status`, `detail`, `instance`) is identical (verified 2026-05-07)
- `dotnet-api-design` REFERENCE.md cites RFC 7807 — technically accurate but stale. Consider updating to RFC 9457, which formalizes the `errors` extension member that minimal-API validation already produces.

## Git / GitHub Conventions

- Conventional Commits stable specification: **v1.0.0** (verified 2026-05-07). The types referenced by `git-workflow` (`feat`, `fix`, `refactor`, `test`, `docs`, `chore`) are aligned with the spec.
- GitHub CLI `gh pr create` supports both `--body-file` (`-F`) and `--draft` (`-d`) flags exactly as `git-workflow` SKILL.md uses them (verified 2026-05-07)

## Google Drive (review-handoff skill)

- Google Drive API v3 is the current REST surface (verified 2026-05-07)
- **Markdown round-trip is native.** Google Docs added markdown import and export in July 2024. `files.export` supports `mimeType=text/markdown` for Google Docs (verified against the official [export formats table](https://developers.google.com/workspace/drive/api/guides/ref-export-formats), 2026-05-07). Upload-with-conversion is supported by sending markdown content with target mime `application/vnd.google-apps.document`.
- The `claude.ai Google Drive` MCP server in this project exposes file-level operations only (`create_file`, `read_file_content`, `search_files`, `get_file_metadata`, etc.). It does **not** expose `comments.list` / `replies.list` (verified 2026-05-07 against the loaded MCP tool schemas).
- `create_file` auto-converts `text/plain` → Doc and `text/csv` → Sheet by default. Whether `text/markdown` triggers conversion through this MCP tool is ⚠️ unverified — needs a smoke test (see `.skills/review-handoff/REFERENCE.md`).
- `read_file_content` returns a "natural language representation" with the explicit caveat that the format may change. Parsing logic in `review-handoff` must be tolerant (verified 2026-05-07).
- Drive's `comments` / `replies` REST endpoints exist and would give the skill anchor-text and resolution semantics, but require an MCP wrapper or shell tool that isn't currently installed. The skill works around this with the inline-comment convention in `CONVENTIONS.md`.

## Claude Code / Agent SDK (host environment)

- Skills are loaded from `.skills/` in the project and invoked via the `Skill` tool ⚠️ unverified against current Claude Code release notes — the skills here use a frontmatter format that matches earlier published examples
- MCP servers (`claude_ai_Figma`, `claude_ai_Google_Drive`, `claude_ai_Microsoft_Learn`) are surfaced as deferred tools via `ToolSearch`; the skill assumes these are installed in the user's environment ⚠️ unverified — the calling user must confirm MCP availability before `review-handoff` will work

---

## Action items surfaced by this audit

These are not version pins — they are concrete drift items the skills should address.

1. **`dotnet-api-design` REFERENCE.md** — replace `.WithOpenApi()` in the minimal-API scaffold; it is deprecated in ASP.NET Core 10. The replacement uses the metadata extensions directly (`.WithName()`, `.WithSummary()`, `.WithDescription()`, `.Produces<T>()`).
2. **`dotnet-api-design` REFERENCE.md** — RFC reference can be updated from 7807 to 9457.
3. **`dotnet-api-design` REFERENCE.md** — the FluentValidation scaffold should not rely on `FluentValidation.AspNetCore` (deprecated). Use manual validation in an endpoint filter (`AddEndpointFilter<ValidationFilter<T>>()` is already shown — confirm the filter implementation does not import the dead package).
4. **`tdd/REFERENCE.md`** — FluentAssertions is unpinned. Decide between `<8.0.0` (free), v7 maintenance branch, or migrating to Shouldly / awaitable xUnit assertions before any commercial use.
5. **`tdd/REFERENCE.md`** — Moq is unpinned. Either pin to ≥4.20.2 (post-SponsorLink) or migrate to NSubstitute. The Moq examples translate cleanly.
