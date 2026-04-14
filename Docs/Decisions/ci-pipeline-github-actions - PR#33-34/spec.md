# Specification: CI Pipeline — GitHub Actions
<!-- spec-ci-pipeline.md -->

**Document:** `docs/decisions/spec-ci-pipeline.md`
**Status:** Ready for Implementation
**Branch:** `docs/ci-cd-foundation-and-dx`
**Phase:** 1 (Linting, Build, Unit Tests, AI PR Reviewer) — Integration tests stubbed for Phase 2
**Author:** Joe / Claude Architect Session
**Date:** 2026-03-29

---

## Table of Contents

- [User Story](#user-story)
- [Goals and Non-Goals](#goals-and-non-goals)
- [Pipeline Architecture](#pipeline-architecture)
- [Trigger Configuration](#trigger-configuration)
- [Job Definitions](#job-definitions)
  - [Job 1: lint-format](#job-1-lint-format)
  - [Job 2: build-test](#job-2-build-test)
  - [Job 3: ai-review](#job-3-ai-review)
- [AI Reviewer — System Prompt](#ai-reviewer--system-prompt)
- [AI Reviewer — Block and Override Behavior](#ai-reviewer--block-and-override-behavior)
- [Secrets and Configuration](#secrets-and-configuration)
- [File Structure](#file-structure)
- [Acceptance Criteria](#acceptance-criteria)
- [Out of Scope](#out-of-scope)
- [Future Phases](#future-phases)
- [Known Constraints](#known-constraints)

---

## User Story

> As a developer working on Banderas, I want every push and pull request to automatically verify that the code builds, is correctly formatted, and passes unit tests — and I want every labeled PR to receive an AI-assisted code review with inline comments — so that I catch regressions and design issues before they reach protected branches.

---

## Goals and Non-Goals

**Goals:**
- Run format verification, build, and unit tests automatically on every push to `feature/*` branches and on every PR targeting `dev` or `main`
- Block PRs that fail format, build, or test checks
- Trigger an AI code reviewer on PRs that carry the `ai-review` label
- AI reviewer posts inline comments and requests changes, but can be overridden by a repo admin
- Pipeline is fast: lint and test jobs run in parallel; total wall time target is under 3 minutes for Phase 1

**Non-Goals (explicitly deferred):**
- Integration tests against a live database (Phase 2)
- Code coverage gate (Phase 2)
- Security scanning via CodeQL or Dependabot alerts (Phase 3)
- Deployment to Azure (Phase 8)
- Automatically triggering Devin or any third-party AI agent

---

## Pipeline Architecture

```
Push to feature/* branch:
  ┌── [lint-format] ──────────────────────────────────────────┐
  │   restore → format check → build (-warnaserror)            │
  └────────────────────────────────────────────────────────────┘

  ┌── [build-test] ───────────────────────────────────────────┐
  │   restore → unit tests (xUnit)                             │
  └────────────────────────────────────────────────────────────┘

PR targeting dev or main:
  Same two parallel jobs above, plus:

  ┌── [ai-review] (only if label 'ai-review' is present) ─────┐
  │   fetch changed files + diffs                              │
  │   POST to Claude API (Sonnet) with system prompt           │
  │   POST review + inline comments to GitHub PR               │
  │   Request Changes (blocks merge; admin can override)       │
  └────────────────────────────────────────────────────────────┘
```

`lint-format` and `build-test` always run in parallel.
`ai-review` waits for both to succeed before running (no point reviewing broken code).

---

## Trigger Configuration

```yaml
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
```

**Notes:**
- `push` triggers run `lint-format` and `build-test` only (no AI reviewer)
- `pull_request` triggers run all three jobs, but `ai-review` is conditional on the `ai-review` label
- Re-running when a label is added (`labeled` type) ensures the reviewer fires if you add the label after opening the PR

---

## Job Definitions

### Job 1: lint-format

**Purpose:** Enforce code style and prevent broken builds from reaching PRs.

**Runner:** `ubuntu-latest`

**Steps:**

| Step | Command | Fail behavior |
|---|---|---|
| Checkout | `actions/checkout@v4` | Hard fail |
| Setup .NET | `actions/setup-dotnet@v4` with `dotnet-version: '10.x'` | Hard fail |
| Restore | `dotnet restore Banderas.sln` | Hard fail |
| Format check | `dotnet format Banderas.sln --verify-no-changes` | Hard fail — PR cannot merge |
| Build | `dotnet build Banderas.sln --no-restore --no-incremental -warnaserror` | Hard fail — PR cannot merge |

**Notes:**
- `--verify-no-changes` means CSharpier/dotnet format will exit non-zero if any file would be changed, causing the step to fail
- `-warnaserror` treats all compiler warnings as errors — enforces zero-warning policy
- `--no-incremental` forces a clean build every run, avoiding stale artifact false positives

---

### Job 2: build-test

**Purpose:** Run unit tests and confirm the application compiles independently of Job 1.

**Runner:** `ubuntu-latest`

**Steps:**

| Step | Command | Fail behavior |
|---|---|---|
| Checkout | `actions/checkout@v4` | Hard fail |
| Setup .NET | `actions/setup-dotnet@v4` with `dotnet-version: '10.x'` | Hard fail |
| Restore | `dotnet restore Banderas.sln` | Hard fail |
| Unit tests | `dotnet test Banderas.sln --no-restore --filter "Category!=Integration"` | Hard fail — PR cannot merge |
| Integration tests | _(stubbed — see note)_ | Not active in Phase 1 |

**Integration test stub note:**
The integration test step is defined in this spec but must not be added to the YAML until:
1. Integration tests exist in the test project
2. A Postgres service container is configured in the job (see [Future Phases](#future-phases))

The `--filter "Category!=Integration"` flag on the unit test step ensures that any integration test added early by mistake does not run and contaminate the Phase 1 pipeline.

**Test category convention:**
All xUnit tests must be decorated to indicate type:
```csharp
// Unit test — runs in Phase 1
[Trait("Category", "Unit")]
public class PercentageStrategyTests { ... }

// Integration test — skipped until Phase 2
[Trait("Category", "Integration")]
public class FlagEndpointTests { ... }
```

---

### Job 3: ai-review

**Purpose:** Use Claude to review PR diffs for .NET best practices, Clean Architecture violations, and project-specific patterns. Posts inline comments and requests changes on the PR.

**Runner:** `ubuntu-latest`

**Condition:** Runs only when:
- The event is `pull_request`
- The label `ai-review` is present on the PR
- Both `lint-format` and `build-test` have passed

```yaml
needs: [lint-format, build-test]
if: |
  github.event_name == 'pull_request' &&
  contains(github.event.pull_request.labels.*.name, 'ai-review')
```

**Steps:**

| Step | Action | Notes |
|---|---|---|
| Checkout | `actions/checkout@v4` | Needed to read file content |
| Fetch PR diff | GitHub API via `curl` or `actions/github-script` | Gets list of changed files and unified diffs |
| Trim diff | Inline script | Truncate to 12,000 tokens max to stay within Claude context limits |
| Call Claude API | `curl` POST to `https://api.anthropic.com/v1/messages` | Uses `claude-sonnet-4-5` model, system prompt below |
| Parse response | `jq` or inline Python | Extracts file-level comments from Claude's JSON response |
| Post review | GitHub API via `actions/github-script` | Posts a `REQUEST_CHANGES` review with inline comments |

**Claude API call structure:**
```json
{
  "model": "claude-sonnet-4-5",
  "max_tokens": 2048,
  "system": "<system prompt — see below>",
  "messages": [
    {
      "role": "user",
      "content": "Review the following pull request diff:\n\n<diff>\n{DIFF_CONTENT}\n</diff>\n\nChanged files: {FILE_LIST}"
    }
  ]
}
```

**Expected Claude response format:**
Claude must be prompted to return structured JSON (no markdown fences):
```json
{
  "summary": "One paragraph summary of the review",
  "issues": [
    {
      "file": "Banderas.Api/Controllers/BanderasController.cs",
      "line": 42,
      "severity": "error|warning|suggestion",
      "comment": "Plain text comment to post inline"
    }
  ],
  "approved": false
}
```

If `approved` is `true` and `issues` is empty, the job posts an approving review instead of requesting changes.

---

## AI Reviewer — System Prompt

This prompt must be committed as `.github/prompts/ai-review-system.md` and read into the workflow at runtime. It must not be hardcoded in the YAML.

```
You are a senior .NET engineer performing a code review on a pull request for Banderas.

Banderas is a .NET 10 Web API following strict Clean Architecture:
- Domain layer: entities, value objects, enums, interfaces. No outward dependencies.
- Application layer: services, DTOs, validators, strategies. Depends only on Domain.
- Infrastructure layer: EF Core, Postgres, repository implementations. Depends on Application.
- API layer: controllers, middleware, DI wiring. Depends on Application only.

Key rules to enforce:
1. Domain entities must never appear in controller signatures or cross the service boundary. Use DTOs.
2. IBanderasService methods must accept and return DTOs only — never the Flag entity.
3. FluentValidation is v12. Do not suggest .Transform() — it was removed. Use .Must() lambda pattern.
4. FluentValidation validators are registered explicitly with AddScoped<>() — not AddValidatorsFromAssemblyContaining.
5. Controllers call ValidateAsync() manually — FluentValidation.AspNetCore is not used.
6. All async methods must propagate CancellationToken.
7. Evaluation logic must remain deterministic and isolated from persistence.
8. Do not suggest adding try/catch in controllers — global exception middleware handles this.
9. Naming: interfaces prefixed with I, async methods suffixed with Async, no abbreviations.
10. Zero warnings policy — no suppression attributes without a comment explaining why.

Review for:
- Violations of the above rules
- SOLID principle violations
- Logic errors or precondition gaps
- Security concerns (injection, unvalidated input reaching persistence)
- Missing or incorrect CancellationToken propagation
- Any pattern inconsistent with the existing codebase

Do NOT flag:
- Style issues already handled by CSharpier (spacing, braces, line length)
- Missing XML documentation on internal/private members
- Test coverage gaps (a separate phase handles this)

Respond ONLY with a valid JSON object. No markdown, no preamble, no explanation outside the JSON.
Use this exact schema:
{
  "summary": "string",
  "issues": [
    { "file": "string", "line": number, "severity": "error|warning|suggestion", "comment": "string" }
  ],
  "approved": boolean
}
```

---

## AI Reviewer — Block and Override Behavior

**How blocking works:**
- The `ai-review` job posts a GitHub **"Request Changes"** review when `approved: false`
- GitHub treats this as a blocking review — the merge button is disabled for contributors
- Configure `dev` and `main` as protected branches with "Require pull request reviews before merging" enabled

**How override works:**
- The repo admin (you) can **dismiss the review** via the GitHub PR UI: `Reviews → Dismiss review`
- After dismissing, the merge button re-enables with no further CI requirement
- This requires no special configuration — GitHub admins can always dismiss reviews on their own repos

**One-block-per-run behavior:**
- Each run of the `ai-review` job resolves the previous review automatically by posting a new one
- If you push a fix and the PR is re-labeled or the workflow is re-run, Claude reviews the updated diff
- No accumulation of stale blocking reviews — only the most recent review counts

---

## Secrets and Configuration

The following secrets must be added to the GitHub repository before the workflow runs:

| Secret name | Value | Where to get it |
|---|---|---|
| `ANTHROPIC_API_KEY` | Claude API key | console.anthropic.com |
| `GITHUB_TOKEN` | Auto-provided by GitHub Actions | No setup needed — `${{ secrets.GITHUB_TOKEN }}` |

To add secrets: `GitHub repo → Settings → Secrets and variables → Actions → New repository secret`

No environment variables are stored in the YAML directly. All sensitive values come from secrets.

---

## File Structure

```
.github/
  workflows/
    ci.yml                     ← Main pipeline definition (implement this)
  prompts/
    ai-review-system.md        ← AI reviewer system prompt (implement this)
  CODEOWNERS                   ← Optional: auto-assign reviewers
```

**Note:** `.github/prompts/ai-review-system.md` must be created and committed before the `ai-review` job will function correctly. The YAML reads this file at runtime using `cat`.

---

## Acceptance Criteria

### AC-1: Format gate
- [ ] Pushing code with incorrect formatting causes the `lint-format` job to fail
- [ ] A correctly formatted push causes `lint-format` to pass
- [ ] Format check runs on pushes to `feature/**`, `fix/**`, `refactor/**`, `docs/**`, `test/**`

### AC-2: Build gate
- [ ] Code with a compiler error causes `lint-format` to fail at the build step
- [ ] A compiler warning causes `lint-format` to fail (warnaserror active)

### AC-3: Unit test gate
- [ ] A failing unit test causes `build-test` to fail
- [ ] Tests decorated with `[Trait("Category", "Integration")]` do not run in Phase 1

### AC-4: Parallel execution
- [ ] `lint-format` and `build-test` run concurrently — verify via GitHub Actions timeline view
- [ ] Neither job depends on the other

### AC-5: AI reviewer trigger conditions
- [ ] Opening a PR to `dev` or `main` WITHOUT the `ai-review` label does NOT trigger `ai-review` job
- [ ] Adding the `ai-review` label to an open PR triggers the `ai-review` job
- [ ] `ai-review` job does not start until both `lint-format` and `build-test` have passed

### AC-6: AI reviewer output
- [ ] Claude posts a review comment on the PR with a summary paragraph
- [ ] Claude posts at least one inline comment on the diff (when issues exist)
- [ ] When `approved: false`, the PR shows a "Changes requested" review state — merge is blocked
- [ ] When `approved: true`, the PR shows an "Approved" review state

### AC-7: Admin override
- [ ] Repo admin can dismiss the Claude review via the GitHub PR UI
- [ ] After dismissal, the merge button re-enables

### AC-8: Secrets
- [ ] Pipeline does not log or echo `ANTHROPIC_API_KEY` at any point
- [ ] `GITHUB_TOKEN` is used only for posting reviews, not stored elsewhere

---

## Out of Scope

The following are explicitly excluded from this spec and must not be implemented by Claude Code during this session:

- Integration test execution against Postgres
- Code coverage reporting or gates
- SonarCloud or any third-party code quality service
- Dependabot or CodeQL security scanning
- Docker image builds
- Any Azure deployment steps
- Branch protection rule configuration (manual GitHub UI step — not automated)

---

## Future Phases

### Phase 2 — Integration tests

Add a Postgres service container to `build-test`:

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
dotnet test Banderas.sln --no-restore
```

Add connection string override via environment variable pointing to the service container.

### Phase 2 — Coverage gate

Add after unit test step:
```bash
dotnet test Banderas.sln \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

reportgenerator \
  -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:MarkdownSummaryGithub

# Fail if line coverage < 80%
```

### Phase 3 — Security scanning

Enable Dependabot for NuGet packages via `.github/dependabot.yml`.
Enable CodeQL via `github/codeql-action` (free for public repos).

---

## Known Constraints

- **CSharpier config must be in place before this pipeline runs.** The format check step will fail on all existing files if `.editorconfig` and `.csharpier.json` are not committed first. Resolve CSharpier configuration (tracked separately) before implementing this spec.
- **AI reviewer token limit.** Diffs larger than ~12,000 tokens will be truncated before being sent to Claude. Very large PRs will receive a partial review. Mitigation: keep PRs small and focused (good practice anyway).
- **`GITHUB_TOKEN` permission scope.** The default `GITHUB_TOKEN` in GitHub Actions may not have write access to pull request reviews depending on repository settings. If the `ai-review` job fails with a 403, navigate to `Settings → Actions → General → Workflow permissions` and set to "Read and write permissions."
- **Branch protection rules must be set manually.** Protecting `dev` and `main` to require passing status checks is a GitHub UI operation, not automatable via this workflow. This must be configured by the repo admin after the first successful pipeline run.