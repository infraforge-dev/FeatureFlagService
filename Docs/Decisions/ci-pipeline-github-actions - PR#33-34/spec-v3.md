# Specification: CI Pipeline — GitHub Actions (v3)

**Document:** `docs/decisions/spec-ci-pipeline.md`
**Status:** Ready for Implementation — PRs 1 and 2 only
**Branch:** `docs/ci-cd-foundation-and-dx`
**Scope:** PR 1 (code style foundation) + PR 2 (core CI pipeline)
**Explicitly excludes:** AI reviewer job — covered in `docs/decisions/spec-ai-reviewer.md` (forthcoming)
**Replaces:** v2 — revised after Claude Code + Codex second review pass
**Author:** Joe / Claude Architect Session
**Date:** 2026-03-29

---

## Table of Contents

- [Revision Log](#revision-log)
- [User Story](#user-story)
- [Goals and Non-Goals](#goals-and-non-goals)
- [PR Split Overview](#pr-split-overview)
- [PR 1 — Code Style Foundation](#pr-1--code-style-foundation)
  - [Files to Create](#files-to-create)
  - [Test Trait Decoration](#test-trait-decoration)
  - [Acceptance Criteria — PR 1](#acceptance-criteria--pr-1)
- [PR 2 — Core CI Pipeline](#pr-2--core-ci-pipeline)
  - [Trigger Configuration](#trigger-configuration)
  - [Job 1: lint-format](#job-1-lint-format)
  - [Job 2: build-test](#job-2-build-test)
  - [Job 3: ai-review stub](#job-3-ai-review-stub)
  - [Acceptance Criteria — PR 2](#acceptance-criteria--pr-2)
- [Secrets and Configuration](#secrets-and-configuration)
- [File Structure](#file-structure)
- [Out of Scope](#out-of-scope)
- [Future Phases](#future-phases)
- [Known Constraints](#known-constraints)

---

## Revision Log

### v1 → v2

| Issue | v1 | v2 Fix |
|---|---|---|
| `-warnaserror` non-standard CLI flag | Used `-warnaserror` | Changed to `-p:TreatWarningsAsErrors=true` |
| Model version stale | `claude-sonnet-4-5` | Updated to `claude-sonnet-4-6` |
| `dotnet format` vs CSharpier conflation | Used `dotnet format` | CSharpier is source of truth |
| `GITHUB_TOKEN` permissions undeclared | Not specified | Explicit `pull-requests: write` on `ai-review` job |
| Previous reviews accumulate | Claimed auto-dismiss | Explicit dismiss-previous-review step |
| Repo path mismatch in system prompt | `Banderas.Api` | Corrected to `Banderas.Api` |
| NuGet caching absent | Not specified | `cache: 'nuget'` on all jobs |
| Token truncation underspecified | "12,000 tokens" vague | `head -c 48000` specified |
| Inline comment positioning risk | Specified file + line | Top-level PR comment with `file:line` refs |
| Fail behavior on API error | Not specified | Fail-open with warning comment |
| Test trait decoration | Mandated but unchecked | Existing tests must be decorated as part of this work |

### v2 → v3

| Issue | v2 | v3 Fix |
|---|---|---|
| Prerequisite deadlock | "Stop if prerequisites missing" — but test decoration was also in-scope | Prerequisites split: "must exist before YAML" vs "created as part of this work" |
| Concurrency group lacks workflow prefix | `ci-${{ github.ref }}` | `${{ github.workflow }}-${{ github.event.pull_request.number \|\| github.ref }}` |
| AC-6 wording inaccurate | "does NOT show an ai-review job" | Corrected to "ai-review job appears as skipped" |
| Secret handling rule vs sample inconsistency | Rule said no inline secret; sample showed it in shell | Clarified: env var in shell is correct pattern; echoing to stdout is what's forbidden |
| AI reviewer job in core pipeline | Full implementation attempted | Stripped to commented-out stub; implementation deferred to `spec-ai-reviewer.md` |
| Scope too broad for one PR | All three jobs in one spec | Split into PR 1 (style foundation) + PR 2 (core pipeline) + PR 3 (AI reviewer) |

---

## User Story

> As a developer working on Banderas, I want every push and pull request to automatically verify that the code is correctly formatted, builds cleanly, and passes unit tests — so that I catch regressions and formatting drift before they reach protected branches.

*(The AI reviewer user story is covered in `spec-ai-reviewer.md`.)*

---

## Goals and Non-Goals

**Goals:**
- Establish CSharpier as the formatting source of truth with supporting config files
- Run format verification, build, and unit tests on every push to branch prefixes and every PR targeting `dev` or `main`
- Block PRs that fail format, build, or test checks
- Run `lint-format` and `build-test` in parallel; total wall time target under 2 minutes for these two jobs
- Stub the `ai-review` job in the YAML so PR 3 can enable it without structural changes

**Non-Goals (this spec):**
- AI reviewer implementation (PR 3 — `spec-ai-reviewer.md`)
- Integration tests against a live database (Phase 2)
- Code coverage gate (Phase 2)
- Security scanning (Phase 3)
- Deployment (Phase 8)

---

## PR Split Overview

| PR | Branch | Delivers | Merges into |
|---|---|---|---|
| **PR 1** | `feature/code-style-foundation` | Config files + test trait decoration | `dev` |
| **PR 2** | `feature/ci-core-pipeline` | `ci.yml` with `lint-format` + `build-test` | `dev` (after PR 1) |
| **PR 3** | `feature/ci-ai-reviewer` | AI reviewer job + system prompt | `dev` (after PR 2) |

PR 1 must be merged before PR 2 is opened. The format check step in PR 2 will fail on every existing file if the CSharpier config files from PR 1 are not present.

---

## PR 1 — Code Style Foundation

**Branch:** `feature/code-style-foundation`
**Purpose:** Establish all formatter prerequisites so the CI format gate has something valid to enforce.

---

### Files to Create

#### `.editorconfig` (repo root)

```ini
root = true

[*]
indent_style = space
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{cs,csproj,sln}]
indent_size = 4

[*.{json,yml,yaml,xml,md}]
indent_size = 2
```

**Why `end_of_line = lf`:** Eliminates carriage return warnings from CSharpier. Windows uses CRLF by default; forcing LF in `.editorconfig` means CSharpier and `dotnet format` both agree on line endings regardless of developer OS.

---

#### `.gitattributes` (repo root)

```gitattributes
* text=auto eol=lf
*.cs text eol=lf
*.csproj text eol=lf
*.sln text eol=lf
*.json text eol=lf
*.yml text eol=lf
*.yaml text eol=lf
*.md text eol=lf
```

**Why this is needed:** `.gitattributes` tells Git to normalize line endings on commit regardless of the developer's OS. Without this, a Windows developer committing CRLF files would pass locally (CSharpier configured for LF) but break CI. The `.gitattributes` file is the enforcement point at the Git layer.

---

#### `.csharpierrc.json` (repo root)

```json
{
  "printWidth": 100
}
```

Minimal config. CSharpier is intentionally opinionated — the fewer overrides, the more consistent the output across machines.

---

#### `.config/dotnet-tools.json` (repo root)

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "csharpier": {
      "version": "0.30.6",
      "commands": ["dotnet-csharpier"]
    }
  }
}
```

**Why this file exists:** CSharpier is a dotnet tool, not part of the SDK. Without a tool manifest, `dotnet tool restore` has nothing to install from, and `dotnet csharpier` will fail with "command not found" in CI. The manifest pins the exact version so all developers and CI runners use identical formatting output.

**Version note:** `0.30.6` is the current stable release as of this spec. Verify at https://www.nuget.org/packages/CSharpier before implementing and update if a newer stable version exists.

---

#### `.vscode/settings.json` (update or create)

```json
{
  "files.eol": "\n",
  "editor.formatOnSave": true,
  "editor.defaultFormatter": "csharpier.csharpier-vscode",
  "[csharp]": {
    "editor.defaultFormatter": "csharpier.csharpier-vscode"
  }
}
```

This configures VS Code to auto-format on save using CSharpier. Developers never need to manually run the formatter — saving a file is enough. This is what makes the linter "quiet" — violations are fixed silently on save rather than surfaced as warnings.

---

### Test Trait Decoration

All existing test classes in `Banderas.Tests` must be decorated with `[Trait("Category", "Unit")]` as part of PR 1.

**This is in-scope for PR 1, not a prerequisite blocking implementation.** Claude Code should scan `Banderas.Tests/**/*.cs` for test classes and add the trait to any class decorated with xUnit attributes (`[Fact]`, `[Theory]`) that does not already have a `[Trait("Category", ...)]` attribute.

**Convention going forward:**

```csharp
// Unit test — runs in CI immediately
[Trait("Category", "Unit")]
public class PercentageStrategyTests
{
    [Fact]
    public void Returns_true_when_user_in_bucket() { ... }
}

// Integration test — filtered out until Phase 2
[Trait("Category", "Integration")]
public class FlagEndpointTests
{
    [Fact]
    public async Task GetAll_returns_seeded_flags() { ... }
}
```

**Why `--filter "Category!=Integration"` instead of `--filter "Category=Unit"`:**
Using `!=Integration` means any undecorated test still runs rather than being silently skipped. This is intentional — undecorated tests are treated as unit tests by default. A test only gets excluded if it is explicitly tagged `Integration`.

---

### Acceptance Criteria — PR 1

- [ ] `.editorconfig` present at repo root with `end_of_line = lf`
- [ ] `.gitattributes` present at repo root with LF normalization rules for `.cs`, `.csproj`, `.sln`, `.json`, `.yml`
- [ ] `.csharpierrc.json` present at repo root with `printWidth: 100`
- [ ] `.config/dotnet-tools.json` present with CSharpier pinned to a specific version
- [ ] Running `dotnet tool restore` from repo root succeeds with exit code 0
- [ ] Running `dotnet csharpier --check .` from repo root passes on the existing codebase (no files would be reformatted)
- [ ] If `dotnet csharpier --check .` fails: Claude Code must run `dotnet csharpier .` to fix violations, then commit the formatted files, then verify `--check` passes
- [ ] `.vscode/settings.json` present with `formatOnSave: true` and CSharpier as default formatter
- [ ] All existing test classes in `Banderas.Tests` carry `[Trait("Category", "Unit")]`
- [ ] Running `dotnet test Banderas.sln --filter "Category!=Integration"` succeeds with all tests passing

---

## PR 2 — Core CI Pipeline

**Branch:** `feature/ci-core-pipeline`
**Depends on:** PR 1 merged to `dev`
**Purpose:** Implement `lint-format` and `build-test` jobs. Stub `ai-review` job for PR 3.

---

### Trigger Configuration

```yaml
name: CI

on:
  push:
    branches:
      - 'feature/**'
      - 'fix/**'
      - 'refactor/**'
      - 'docs/**'
      - 'test/**'

  pull_request:
    branches:
      - dev
      - main
    types:
      - opened
      - synchronize
      - reopened
      - labeled

concurrency:
  group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

**Why `github.workflow` in the concurrency group:** If a second workflow file is added later, using only `github.ref` would cause the two workflows to share a concurrency group and cancel each other unexpectedly. Prefixing with the workflow name scopes cancellation correctly to this workflow only.

**Why `pull_request.number || github.ref`:** For PR events, groups by PR number — each PR gets its own slot and new pushes cancel the previous run for that PR. For push events (no PR number), groups by ref — pushes to the same branch cancel each other. This gives optimal cancellation behavior for both event types.

**Trigger scope:**
- `push` triggers run `lint-format` and `build-test` only. The `ai-review` job never runs on push events.
- `pull_request` triggers run all three jobs, but `ai-review` is conditional on the `ai-review` label AND will be a no-op stub until PR 3.

---

### Job 1: lint-format

**Purpose:** Enforce CSharpier formatting and confirm the project builds with zero warnings.

**Runner:** `ubuntu-latest`

```yaml
lint-format:
  name: Lint and Format
  runs-on: ubuntu-latest
  steps:
    - name: Checkout
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.x'
        cache: 'nuget'

    - name: Restore dotnet tools
      run: dotnet tool restore

    - name: Restore packages
      run: dotnet restore Banderas.sln

    - name: Check formatting
      run: dotnet csharpier --check .

    - name: Build
      run: >
        dotnet build Banderas.sln
        --no-restore
        --no-incremental
        -p:TreatWarningsAsErrors=true
```

**Step notes:**

`dotnet tool restore` — reads `.config/dotnet-tools.json` and installs the pinned CSharpier version into the local tool path. This must run before `dotnet csharpier`. Without it the command is not found.

`dotnet csharpier --check .` — exits non-zero if any file would be reformatted. Does not modify files. This is the format gate.

`-p:TreatWarningsAsErrors=true` — correct cross-platform MSBuild property for treating warnings as errors. Do not use `-warnaserror` — it is an MSBuild passthrough that behaves inconsistently across platforms and is not documented in the `dotnet build` CLI reference.

`--no-incremental` — forces a full rebuild every run. Prevents stale artifact false positives where a cached intermediate file masks a real build error.

`cache: 'nuget'` — caches the global NuGet package cache between runs using the lockfile as the cache key. Typically saves 30–60 seconds on subsequent runs. Requires `dotnet restore` to generate a lockfile on first run.

---

### Job 2: build-test

**Purpose:** Run the unit test suite independently of the lint job. Parallel execution means a test failure and a format failure are both surfaced simultaneously rather than sequentially.

**Runner:** `ubuntu-latest`

```yaml
build-test:
  name: Build and Test
  runs-on: ubuntu-latest
  steps:
    - name: Checkout
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.x'
        cache: 'nuget'

    - name: Restore packages
      run: dotnet restore Banderas.sln

    - name: Run unit tests
      run: >
        dotnet test Banderas.sln
        --no-restore
        --filter "Category!=Integration"
        --logger "console;verbosity=normal"
```

**Step notes:**

`--filter "Category!=Integration"` — runs all tests except those explicitly tagged Integration. Undecorated tests run by default. See PR 1 trait decoration section for the convention.

`--logger "console;verbosity=normal"` — surfaces test names and failure messages directly in the Actions log without requiring a separate artifact download.

**Integration test stub:** The integration test step is intentionally absent from this job. It will be added in Phase 2 when a Postgres service container is configured. See [Future Phases](#future-phases).

---

### Job 3: ai-review stub

The `ai-review` job must be present in `ci.yml` as a commented-out stub. This allows PR 3 to uncomment and implement it without restructuring the workflow file.

```yaml
# ai-review:
#   name: AI Code Review
#   runs-on: ubuntu-latest
#   needs: [lint-format, build-test]
#   # Implementation: see docs/decisions/spec-ai-reviewer.md
#   # Conditions: pull_request event + 'ai-review' label present
#   # Permissions: pull-requests: write
```

Claude Code must include this comment block verbatim in `ci.yml`. Do not implement any steps inside this job in PR 2.

---

### Acceptance Criteria — PR 2

**Trigger behavior:**
- [ ] Pushing to a `feature/**` branch creates a workflow run containing `lint-format` and `build-test` jobs
- [ ] Pushing to a `feature/**` branch does NOT trigger an `ai-review` job (stub is commented out)
- [ ] Opening a PR targeting `dev` or `main` creates a workflow run containing `lint-format` and `build-test` jobs

**Parallel execution:**
- [ ] `lint-format` and `build-test` appear as concurrent jobs in the GitHub Actions run timeline (verify via the graph view — both should start at the same time)
- [ ] Neither job lists the other in a `needs:` field

**NuGet caching:**
- [ ] On the second workflow run, both jobs show a "Cache hit" message in the Setup .NET step log

**Format gate:**
- [ ] Temporarily introducing a formatting violation (e.g. wrong indentation) and pushing causes `lint-format` to fail at the "Check formatting" step
- [ ] Reverting the violation and pushing causes `lint-format` to pass

**Build gate:**
- [ ] Temporarily introducing a compiler warning (e.g. unused variable) and pushing causes `lint-format` to fail at the "Build" step with `TreatWarningsAsErrors` message visible in log
- [ ] Build step output does not contain `-warnaserror` in the invocation log

**Unit test gate:**
- [ ] Temporarily breaking a unit test and pushing causes `build-test` to fail at the "Run unit tests" step
- [ ] Test output log shows test names and failure message (verbosity=normal)
- [ ] Test output confirms zero `Integration` category tests were executed

**Concurrency:**
- [ ] Pushing two commits rapidly to the same branch results in the first run being cancelled ("Cancelled" status) and the second run completing

**Secret hygiene (applicable when `ANTHROPIC_API_KEY` is later added):**
- [ ] No step in `lint-format` or `build-test` references or prints `ANTHROPIC_API_KEY`

---

## Secrets and Configuration

**PR 2 requires no secrets.** `lint-format` and `build-test` make no external API calls.

The `ANTHROPIC_API_KEY` secret will be required in PR 3. For reference, it must be added before PR 3 is implemented:

| Secret name | Where to add | How to get it |
|---|---|---|
| `ANTHROPIC_API_KEY` | `Repo → Settings → Secrets and variables → Actions → New repository secret` | console.anthropic.com |
| `GITHUB_TOKEN` | Auto-injected by GitHub Actions — no setup needed | Use as `${{ secrets.GITHUB_TOKEN }}` |

**Secret handling rule:** Using a secret via an environment variable in a shell step is the correct pattern. What is forbidden is echoing, printing, or interpolating the secret value into a string that appears in stdout. For example:

```yaml
# Correct — secret in env block, referenced as $VAR in shell
env:
  API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
run: curl -H "x-api-key: $API_KEY" ...

# Forbidden — value visible in log
run: echo "Key is ${{ secrets.ANTHROPIC_API_KEY }}"
```

GitHub Actions automatically masks secrets in logs when they are referenced via `${{ secrets.* }}`, but this masking is not guaranteed when the value is passed through intermediate variables without the env block pattern.

---

## File Structure

Files Claude Code must produce for PRs 1 and 2:

```
.editorconfig                          ← PR 1 — create
.gitattributes                         ← PR 1 — create
.csharpierrc.json                      ← PR 1 — create
.config/
  dotnet-tools.json                    ← PR 1 — create
.vscode/
  settings.json                        ← PR 1 — create or update

Banderas.Tests/
  **/*.cs                              ← PR 1 — add [Trait("Category", "Unit")] to all test classes

.github/
  workflows/
    ci.yml                             ← PR 2 — create
```

Files deferred to PR 3 (`spec-ai-reviewer.md`):

```
.github/
  prompts/
    ai-review-system.md               ← PR 3
```

---

## Out of Scope

Must not be implemented in PRs 1 or 2:

- Any steps inside the `ai-review` job (stub only)
- Integration test execution or Postgres service container
- Code coverage reporting or enforcement
- SonarCloud or any third-party analysis service
- Dependabot or CodeQL security scanning
- Docker image builds or pushes
- Azure deployment steps
- Branch protection rule configuration (manual GitHub UI step — cannot be automated from workflow)

---

## Future Phases

### Phase 2 — Integration tests (add to `build-test` job)

```yaml
services:
  postgres:
    image: postgres:16
    env:
      POSTGRES_DB: featureflags_test
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: testpassword
    ports:
      - 5432:5432
    options: >-
      --health-cmd pg_isready
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5
```

Update test step to run all categories:
```bash
dotnet test Banderas.sln --no-restore --logger "console;verbosity=normal"
```

Add environment variable override for connection string:
```yaml
env:
  ConnectionStrings__DefaultConnection: >-
    Host=localhost;Port=5432;Database=featureflags_test;
    Username=postgres;Password=testpassword
```

### Phase 2 — Code coverage gate (add after unit tests in `build-test`)

```bash
dotnet test Banderas.sln \
  --no-restore \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

reportgenerator \
  -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:MarkdownSummaryGithub

python3 -c "
import xml.etree.ElementTree as ET
tree = ET.parse('./coverage/report/Summary.xml')
rate = float(tree.getroot().get('line-rate'))
assert rate >= 0.80, f'Line coverage {rate:.0%} is below the 80% threshold'
"
```

### Phase 3 — Security scanning

Enable Dependabot for NuGet packages via `.github/dependabot.yml`:
```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule:
      interval: weekly
```

Enable CodeQL via `github/codeql-action` (free for public repos).

---

## Known Constraints

| Constraint | Impact | Mitigation |
|---|---|---|
| PR 1 must be merged before PR 2 is opened | `dotnet csharpier --check .` fails on every file without the config files | Enforce merge order — do not open PR 2 until PR 1 is on `dev` |
| CSharpier may reformat existing files | PR 1 may have a large diff of whitespace-only changes | Expected and correct — commit the formatted output as part of PR 1 |
| `GITHUB_TOKEN` write permissions | Default repo setting may be read-only; `ai-review` job will 403 | For PR 3 only: navigate to `Settings → Actions → General → Workflow permissions → Read and write permissions` — not needed for PRs 1 or 2 |
| Branch protection rules are manual | Merge gate not enforced until manually configured in GitHub UI | After first successful PR 2 run: go to `Settings → Branches → Add rule` for `dev` and `main`, require `lint-format` and `build-test` as required status checks |
| NuGet cache miss on first run | First run of PR 2 will be slower than subsequent runs | Expected — cache is populated on first run and hit from second run onward |
