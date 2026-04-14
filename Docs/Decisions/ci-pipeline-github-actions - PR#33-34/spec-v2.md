# Specification: CI Pipeline — GitHub Actions (v2)

**Document:** `docs/decisions/spec-ci-pipeline.md`  
**Status:** Ready for Implementation  
**Branch:** `docs/ci-cd-foundation-and-dx`  
**Phase:** 1 (Linting, Build, Unit Tests, AI PR Reviewer)  
**Replaces:** v1 — revised after dual Claude Code + Codex review  
**Author:** Joe / Claude Architect Session  
**Date:** 2026-03-29  

---

## Table of Contents

- [Revision Log](#revision-log)
- [User Story](#user-story)
- [Goals and Non-Goals](#goals-and-non-goals)
- [Prerequisites](#prerequisites)
- [Pipeline Architecture](#pipeline-architecture)
- [Trigger Configuration](#trigger-configuration)
- [Job Definitions](#job-definitions)
  - [Job 1: lint-format](#job-1-lint-format)
  - [Job 2: build-test](#job-2-build-test)
  - [Job 3: ai-review](#job-3-ai-review)
- [AI Reviewer — System Prompt](#ai-reviewer--system-prompt)
- [AI Reviewer — Fail-Open Behavior](#ai-reviewer--fail-open-behavior)
- [AI Reviewer — Review Lifecycle](#ai-reviewer--review-lifecycle)
- [Secrets and Configuration](#secrets-and-configuration)
- [File Structure](#file-structure)
- [Acceptance Criteria](#acceptance-criteria)
- [Out of Scope](#out-of-scope)
- [Future Phases](#future-phases)
- [Known Constraints](#known-constraints)

---

## Revision Log

| Issue | v1 | v2 Fix |
|---|---|---|
| `-warnaserror` non-standard CLI flag | Used `-warnaserror` | Changed to `-p:TreatWarningsAsErrors=true` |
| Model version stale | `claude-sonnet-4-5` | Updated to `claude-sonnet-4-6` |
| `dotnet format` vs CSharpier conflation | Used `dotnet format` | CSharpier is source of truth — `dotnet csharpier --check .` |
| `GITHUB_TOKEN` permissions undeclared | Not specified | Explicit `pull-requests: write` on `ai-review` job |
| Previous reviews accumulate | Claimed auto-dismiss | Explicit dismiss-previous-review step added |
| Repo path mismatch in system prompt | `Bandera.Api` | Corrected to `Bandera.Api` |
| NuGet caching absent | Not specified | `cache: 'nuget'` added to `setup-dotnet` in all jobs |
| Token truncation underspecified | "12,000 tokens" with no impl | `head -c 48000` character approximation |
| Inline comment positioning | Specified file + line | Top-level PR comment with `file:line` references |
| Fail behavior on API error | Not specified | Fail-open with warning comment; future hardening noted |
| Test trait decoration | Mandated but existing tests unchecked | Existing tests must be decorated as part of this work |

---

## User Story

> As a developer working on Bandera, I want every push and pull request to automatically verify that the code is correctly formatted, builds cleanly, and passes unit tests — and I want PRs labeled `ai-review` to receive an AI-assisted code review with structured feedback — so that I catch regressions and design issues before they reach protected branches.

---

## Goals and Non-Goals

**Goals:**
- Run CSharpier format verification, build, and unit tests on every push to branch prefixes and every PR targeting `dev` or `main`
- Block PRs that fail format, build, or test checks
- Trigger an AI code reviewer on PRs carrying the `ai-review` label, only after all other checks pass
- AI reviewer posts a structured top-level review comment listing issues by `file:line`
- AI reviewer requests changes when issues are found; repo admin can dismiss and override
- Fail-open: transient API errors post a warning comment and do not block the PR
- Pipeline wall time under 3 minutes via parallel jobs and NuGet caching

**Non-Goals (explicitly deferred):**
- Integration tests against a live database (Phase 2)
- Code coverage gate (Phase 2)
- Security scanning via CodeQL or Dependabot (Phase 3)
- Deployment to Azure (Phase 8)
- Switching from fail-open to fail-closed on the AI reviewer (future — noted in Known Constraints)

---

## Prerequisites

The following must be in place **before implementing this spec**. These are not part of this implementation task.

| Prerequisite | Status | Notes |
|---|---|---|
| `.config/dotnet-tools.json` with CSharpier registered | ❌ Must be created | `dotnet new tool-manifest && dotnet tool install csharpier` |
| `.csharpierrc.json` at repo root | ❌ Must be created | Minimal config — `{ "printWidth": 100 }` |
| `.editorconfig` at repo root | ❌ Must be created | Line endings, indent style |
| `.gitattributes` at repo root | ❌ Must be created | `* text=auto eol=lf` |
| `ANTHROPIC_API_KEY` secret in GitHub | ❌ Must be added manually | `Repo → Settings → Secrets → Actions` |
| Existing tests decorated with `[Trait("Category", "Unit")]` | ❌ Must be done as part of this work | See Job 2 notes |

Claude Code must verify all prerequisites are met before generating the workflow YAML. If any are missing, surface them and stop — do not generate a pipeline that will immediately fail.

---

## Pipeline Architecture

```
Push to feature/**, fix/**, refactor/**, docs/**, test/**:
  ┌── [lint-format] ──────────────────────────────────────────┐
  │   restore (cached) → csharpier --check → build            │
  └────────────────────────────────────────────────────────────┘
  ┌── [build-test] ───────────────────────────────────────────┐
  │   restore (cached) → unit tests (xUnit, Category!=Integ.) │
  └────────────────────────────────────────────────────────────┘

PR targeting dev or main:
  Same two parallel jobs above, then if label 'ai-review' present:
  ┌── [ai-review] ────────────────────────────────────────────┐
  │   dismiss previous Claude review (if any)                  │
  │   fetch diff → truncate to 48,000 chars                    │
  │   POST to Claude API → parse JSON response                 │
  │   post top-level PR comment (file:line issue list)         │
  │   post REQUEST_CHANGES or APPROVE review                   │
  │   on any error → post warning comment, exit 0 (fail-open) │
  └────────────────────────────────────────────────────────────┘
```

`lint-format` and `build-test` always run in parallel.  
`ai-review` has `needs: [lint-format, build-test]` — it only runs when both pass.  
No point reviewing code that doesn't build or isn't formatted.

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
- `push` triggers run `lint-format` and `build-test` only. No AI reviewer on pushes.
- `pull_request` triggers run all three jobs, but `ai-review` is conditional on the `ai-review` label.
- The `labeled` type ensures the reviewer fires when a label is added to an already-open PR, not just when the PR is first opened.
- Concurrency group recommended to cancel in-progress runs on the same PR when a new commit arrives:

```yaml
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
```

---

## Job Definitions

### Job 1: lint-format

**Purpose:** Enforce CSharpier formatting and confirm the project builds with zero warnings.

**Runner:** `ubuntu-latest`

**Permissions:** Default (read-only) — no GitHub API writes needed.

**Steps:**

| # | Step | Command / Action | Fail behavior |
|---|---|---|---|
| 1 | Checkout | `actions/checkout@v4` | Hard fail |
| 2 | Setup .NET | `actions/setup-dotnet@v4` — `dotnet-version: '10.x'`, `cache: 'nuget'` | Hard fail |
| 3 | Restore tools | `dotnet tool restore` | Hard fail — installs CSharpier from `dotnet-tools.json` |
| 4 | Restore packages | `dotnet restore Bandera.sln` | Hard fail |
| 5 | Format check | `dotnet csharpier --check .` | Hard fail — PR cannot merge |
| 6 | Build | `dotnet build Bandera.sln --no-restore --no-incremental -p:TreatWarningsAsErrors=true` | Hard fail — PR cannot merge |

**Notes:**
- `dotnet tool restore` reads `.config/dotnet-tools.json` and installs the pinned CSharpier version. This is required — CSharpier is not part of the SDK.
- `dotnet csharpier --check .` exits non-zero if any file would be reformatted. It does not modify files.
- `-p:TreatWarningsAsErrors=true` is the correct, cross-platform MSBuild property. Do not use `-warnaserror`.
- `--no-incremental` forces a clean build every run, preventing stale artifact false positives in CI.

---

### Job 2: build-test

**Purpose:** Run unit tests independently of the lint job. Confirms tests pass on a clean restore.

**Runner:** `ubuntu-latest`

**Permissions:** Default (read-only).

**Steps:**

| # | Step | Command / Action | Fail behavior |
|---|---|---|---|
| 1 | Checkout | `actions/checkout@v4` | Hard fail |
| 2 | Setup .NET | `actions/setup-dotnet@v4` — `dotnet-version: '10.x'`, `cache: 'nuget'` | Hard fail |
| 3 | Restore packages | `dotnet restore Bandera.sln` | Hard fail |
| 4 | Run unit tests | `dotnet test Bandera.sln --no-restore --filter "Category!=Integration"` | Hard fail — PR cannot merge |
| 5 | _(Integration tests)_ | _Stubbed — not active in Phase 1_ | N/A |

**Test category convention — enforced as part of this implementation:**

All test classes in `Bandera.Tests` must carry a `[Trait]` attribute before this pipeline is activated. Claude Code must update existing test files as part of this work.

```csharp
// Unit test — runs in CI Phase 1
[Trait("Category", "Unit")]
public class PercentageStrategyTests
{
    // ...
}

// Integration test — filtered out until Phase 2
[Trait("Category", "Integration")]
public class FlagEndpointTests
{
    // ...
}
```

The `--filter "Category!=Integration"` expression runs all tests that are NOT tagged Integration. Undecorated tests run by default — this is intentional so undecorated tests are never silently skipped. All existing tests must be explicitly decorated `Unit` before this pipeline goes live.

---

### Job 3: ai-review

**Purpose:** Use Claude to review PR diffs against project-specific .NET and Clean Architecture rules. Posts a structured top-level comment and requests changes when issues are found.

**Runner:** `ubuntu-latest`

**Permissions (explicit — required):**
```yaml
permissions:
  contents: read
  pull-requests: write
```

**Condition:**
```yaml
needs: [lint-format, build-test]
if: |
  github.event_name == 'pull_request' &&
  contains(github.event.pull_request.labels.*.name, 'ai-review')
```

**Steps:**

| # | Step | Action / Script | Notes |
|---|---|---|---|
| 1 | Checkout | `actions/checkout@v4` | Needed to read prompt file |
| 2 | Dismiss previous Claude review | `actions/github-script@v7` — see script below | Prevents review accumulation |
| 3 | Fetch PR diff | `actions/github-script@v7` — GitHub API `GET /repos/{owner}/{repo}/pulls/{number}/files` | Gets file list + patch content |
| 4 | Truncate diff | `echo "$DIFF" \| head -c 48000` | ~12k tokens. Prevents context overflow |
| 5 | Read system prompt | `cat .github/prompts/ai-review-system.md` | Prompt stored as versioned file |
| 6 | Call Claude API | `curl` POST to `https://api.anthropic.com/v1/messages` | See request structure below |
| 7 | Validate JSON response | `jq` — check for `.issues` array and `.summary` key | If invalid, trigger fail-open path |
| 8 | Post PR comment + review | `actions/github-script@v7` | Top-level comment with issue list; then APPROVE or REQUEST_CHANGES |

---

**Step 2 — Dismiss previous Claude review script:**

```javascript
// actions/github-script@v7
const reviews = await github.rest.pulls.listReviews({
  owner: context.repo.owner,
  repo: context.repo.repo,
  pull_number: context.payload.pull_request.number,
});

const botLogin = 'github-actions[bot]';
for (const review of reviews.data) {
  if (review.user.login === botLogin && review.state === 'CHANGES_REQUESTED') {
    await github.rest.pulls.dismissReview({
      owner: context.repo.owner,
      repo: context.repo.repo,
      pull_number: context.payload.pull_request.number,
      review_id: review.id,
      message: 'Dismissed by ai-review job — re-reviewing updated diff.',
    });
  }
}
```

---

**Step 6 — Claude API request structure:**

```bash
curl -s -X POST https://api.anthropic.com/v1/messages \
  -H "x-api-key: $ANTHROPIC_API_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -H "content-type: application/json" \
  -d '{
    "model": "claude-sonnet-4-6",
    "max_tokens": 2048,
    "system": "'"$SYSTEM_PROMPT"'",
    "messages": [
      {
        "role": "user",
        "content": "Review the following pull request diff:\n\n<diff>\n'"$TRUNCATED_DIFF"'\n</diff>"
      }
    ]
  }'
```

---

**Step 8 — PR comment format:**

Claude's response is parsed and posted as a single top-level PR comment in this format:

```
## AI Code Review

**Summary:** {summary from Claude}

---

### Issues Found

| Severity | File | Line | Comment |
|---|---|---|---|
| 🔴 error | Bandera.Api/Controllers/BanderasController.cs | 42 | Domain entity `Flag` returned directly from service call — map to `FlagResponse` DTO |
| 🟡 warning | Bandera.Application/Services/BanderaService.cs | 88 | Missing CancellationToken propagation to repository call |
| 🔵 suggestion | Bandera.Domain/Entities/Flag.cs | 15 | Consider guard clause for null name on construction |

---
_Reviewed by Claude claude-sonnet-4-6 · [Dismiss this review](link) to override_
```

After posting the comment, a formal GitHub review is submitted:
- `approved: false` in Claude's JSON → `REQUEST_CHANGES` review state (blocks merge)
- `approved: true` and empty `issues` array → `APPROVE` review state

---

## AI Reviewer — System Prompt

Stored at `.github/prompts/ai-review-system.md`. Read at runtime by the workflow — never hardcoded in YAML.

```
You are a senior .NET engineer performing a code review on a pull request for Bandera.

Bandera is a .NET 10 Web API following strict Clean Architecture:
- Domain layer: entities, value objects, enums, interfaces. Zero outward dependencies.
- Application layer: services, DTOs, validators, strategies. Depends only on Domain.
- Infrastructure layer: EF Core + Npgsql, PostgreSQL, repository implementations. Depends on Application.
- API layer: controllers, middleware, DI wiring. Depends on Application only.

Project namespace roots: Bandera.Domain, Bandera.Application, Bandera.Infrastructure, Bandera.Api

Rules to enforce:
1. Domain entities must never appear in controller signatures or cross the service boundary. DTOs only.
2. IBanderaService methods must accept and return DTOs only — never the Flag entity.
3. FluentValidation is v12. Do not suggest .Transform() — removed in v12. Use .Must() lambda instead.
4. Validators are registered with explicit AddScoped<IValidator<T>, TValidator>() — not AddValidatorsFromAssemblyContaining.
5. Controllers call ValidateAsync() manually — FluentValidation.AspNetCore is not used.
6. All async methods must propagate CancellationToken through every call site.
7. Evaluation logic must remain deterministic and isolated from persistence.
8. Do not suggest adding try/catch in controllers — global exception middleware handles error responses.
9. Naming: interfaces prefixed with I, async methods suffixed with Async, no abbreviations.
10. Zero warnings policy — no pragma warning suppress or SuppressMessage without an explanatory comment.

Review for:
- Violations of the above rules
- SOLID principle violations (especially Open/Closed — watch for primitive parameter lists on update methods)
- Logic errors or missing precondition guards
- Security concerns (unvalidated input reaching persistence, injection risk)
- Missing or incorrect CancellationToken propagation
- Any pattern inconsistent with the established codebase conventions

Do NOT flag:
- Formatting or whitespace — CSharpier handles this in a separate CI step
- Missing XML documentation on internal or private members
- Test coverage gaps — tracked separately in Phase 2
- Stylistic preferences with no correctness impact

Respond ONLY with a valid JSON object. No markdown code fences, no preamble, no text outside the JSON structure.

Required schema:
{
  "summary": "string — one paragraph overview of the review",
  "issues": [
    {
      "file": "string — relative file path from repo root",
      "line": number — line number in the file (not diff position),
      "severity": "error | warning | suggestion",
      "comment": "string — plain English explanation of the issue"
    }
  ],
  "approved": boolean — true only if issues array is empty and no concerns found
}
```

---

## AI Reviewer — Fail-Open Behavior

The `ai-review` job must not block a PR due to transient infrastructure failures.

**Fail-open triggers:**
- Claude API returns non-200 HTTP status
- Response body is not valid JSON
- Response JSON does not conform to the required schema (missing `issues`, missing `summary`)
- `curl` command times out (set `--max-time 60`)

**Fail-open action:**
When any of the above occurs, the job must:
1. Post a top-level PR comment:
   ```
   ## AI Code Review — Unavailable
   The AI reviewer could not complete this review (API error or invalid response).
   This check is advisory. You may merge after human review.
   ```
2. Exit with code `0` — the job passes, the PR remains mergeable.

**Future hardening note (tracked):**
> This fail-open behavior is appropriate for Phase 1 on a portfolio project. When Bandera moves toward production infrastructure (Phase 8+) and the AI reviewer is gating changes to security-critical or customer-facing code paths, this should be revisited. Options include: retry with exponential backoff before failing open, alerting via a GitHub issue comment, or switching to fail-closed for PRs targeting `main` only.

---

## AI Reviewer — Review Lifecycle

**How blocking works:**
- When `approved: false`, the job posts a `REQUEST_CHANGES` GitHub review — the merge button is disabled for contributors.
- Only one active `REQUEST_CHANGES` review from the bot will exist at a time (previous ones are dismissed in Step 2).

**How override works:**
- The repo admin dismisses the review via `PR → Reviews → Dismiss review`.
- After dismissal, the merge button re-enables. No further CI action is required.
- This requires no special configuration — GitHub admins can always dismiss reviews on repos they own.

**Re-review on push:**
- When new commits are pushed to the PR and the `ai-review` label is still present, the workflow re-triggers (`synchronize` event type).
- Step 2 dismisses the previous review before posting a fresh one.
- The PR always reflects the most recent diff's review result.

---

## Secrets and Configuration

| Secret name | Value | Where to get it | Setup required |
|---|---|---|---|
| `ANTHROPIC_API_KEY` | Claude API key | console.anthropic.com | Manual — add before first run |
| `GITHUB_TOKEN` | Auto-injected by Actions | GitHub provides automatically | None — use `${{ secrets.GITHUB_TOKEN }}` |

Rules:
- `ANTHROPIC_API_KEY` must never be echoed, logged, or interpolated into a string that is printed to stdout.
- The workflow must use `${{ secrets.ANTHROPIC_API_KEY }}` in the env block, not inline in shell commands.

---

## File Structure

```
.config/
  dotnet-tools.json              ← CSharpier tool manifest (prerequisite)

.github/
  workflows/
    ci.yml                       ← Implement this
  prompts/
    ai-review-system.md          ← Implement this

Bandera.Tests/
  Domain/
    ValueObjects/
      FeatureEvaluationContextTests.cs   ← Add [Trait("Category", "Unit")]
  (any other existing test files)        ← Add [Trait("Category", "Unit")]
```

---

## Acceptance Criteria

### AC-1: CSharpier format gate
- [ ] Pushing code with incorrect CSharpier formatting causes `lint-format` to fail at the format check step
- [ ] A correctly formatted push causes `lint-format` to pass the format check step
- [ ] `dotnet tool restore` succeeds, confirming `.config/dotnet-tools.json` is present and valid

### AC-2: Build gate
- [ ] Code with a compiler error causes `lint-format` to fail at the build step
- [ ] A compiler warning causes `lint-format` to fail (`TreatWarningsAsErrors=true` active)
- [ ] Build command does not use `-warnaserror` flag

### AC-3: Unit test gate
- [ ] A failing unit test causes `build-test` to fail
- [ ] Tests decorated `[Trait("Category", "Integration")]` do not run (confirmed via test output showing 0 Integration tests executed)
- [ ] All existing tests in `Bandera.Tests` carry `[Trait("Category", "Unit")]`

### AC-4: Parallel execution
- [ ] `lint-format` and `build-test` appear as concurrent jobs in the GitHub Actions run timeline
- [ ] Neither job lists the other in its `needs:` field

### AC-5: NuGet caching
- [ ] Both `lint-format` and `build-test` jobs show a cache hit on second run (confirm via Actions log "Cache hit" message)

### AC-6: AI reviewer trigger conditions
- [ ] A PR to `dev` or `main` without the `ai-review` label does NOT show an `ai-review` job in the Actions run
- [ ] Adding the `ai-review` label to an open PR triggers the `ai-review` job (verify via `labeled` event)
- [ ] `ai-review` job does not appear until both `lint-format` and `build-test` have green checkmarks

### AC-7: Dismiss previous review
- [ ] Running `ai-review` twice on the same PR results in exactly one active Claude review (not two stacked `CHANGES_REQUESTED` reviews)

### AC-8: AI reviewer output — issues found
- [ ] A PR comment appears with the heading `## AI Code Review`
- [ ] The comment includes a summary paragraph
- [ ] The comment includes a markdown table listing issues with severity, file, line, and comment columns
- [ ] The PR shows `Changes requested` review state — merge button is disabled

### AC-9: AI reviewer output — approved
- [ ] When Claude returns `approved: true` and an empty `issues` array, the PR shows `Approved` review state

### AC-10: Fail-open behavior
- [ ] If `ANTHROPIC_API_KEY` is temporarily invalid, the `ai-review` job exits with code 0
- [ ] A warning comment is posted: `## AI Code Review — Unavailable`
- [ ] The PR remains mergeable after the fail-open path

### AC-11: Admin override
- [ ] Repo admin can dismiss the Claude review via the GitHub PR UI
- [ ] After dismissal, the merge button re-enables without re-running any job

### AC-12: Secret hygiene
- [ ] `ANTHROPIC_API_KEY` does not appear in any job log output
- [ ] `GITHUB_TOKEN` is referenced only via `${{ secrets.GITHUB_TOKEN }}` — never hardcoded

---

## Out of Scope

Must not be implemented in this branch:

- Integration test execution (requires Postgres service container — Phase 2)
- Code coverage reporting or enforcement (Phase 2)
- SonarCloud or any third-party analysis service
- Dependabot or CodeQL security scanning (Phase 3)
- Docker image builds or pushes
- Azure deployment steps (Phase 8)
- Branch protection rule configuration (manual GitHub UI operation — not automatable from workflow)

---

## Future Phases

### Phase 2 — Integration tests

Add service container to `build-test` job:

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

Update test filter to run all categories:
```bash
dotnet test Bandera.sln --no-restore
```

Add environment variable override for connection string pointing at `localhost:5432` (the service container port-mapped address).

### Phase 2 — Code coverage gate

```bash
dotnet test Bandera.sln \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

reportgenerator \
  -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:MarkdownSummaryGithub

# Enforce minimum line coverage
python3 -c "
import xml.etree.ElementTree as ET
tree = ET.parse('./coverage/report/Summary.xml')
rate = float(tree.getroot().get('line-rate'))
assert rate >= 0.80, f'Coverage {rate:.0%} is below 80% threshold'
"
```

### Phase 3 — Security scanning

Enable Dependabot for NuGet via `.github/dependabot.yml`:
```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule:
      interval: weekly
```

Enable CodeQL via `github/codeql-action` (free for public repos).

### Phase 8 — Fail-open to fail-closed

Revisit AI reviewer error behavior. Consider:
- Retry with exponential backoff (up to 3 attempts) before failing open
- Fail-closed only on PRs targeting `main`
- Alert via GitHub issue comment on repeated failures

---

## Known Constraints

| Constraint | Impact | Mitigation |
|---|---|---|
| CSharpier config files must exist before running | `lint-format` fails immediately on every file | Treat as hard prerequisite — implement formatter config before activating this pipeline |
| `GITHUB_TOKEN` write permissions setting | `ai-review` may 403 on first run | Navigate to `Settings → Actions → General → Workflow permissions → Read and write` |
| Diff truncation at 48,000 chars | Very large PRs get partial review | Enforce small, focused PRs as team practice |
| Fail-open on API error | Transient failures silently pass | Acceptable in Phase 1; revisit in Phase 8 (documented above) |
| Branch protection rules are manual | Merge gate not enforced until manually configured | Admin must configure `dev` and `main` branch protection after first successful pipeline run — require `lint-format` and `build-test` as required checks |
| `github-actions[bot]` dismiss logic | If bot login differs in some GitHub configurations, dismiss step silently no-ops | Verify bot login matches in Actions log on first run |
