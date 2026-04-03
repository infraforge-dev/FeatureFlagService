# Specification: CI Pipeline — AI Reviewer
<!-- spec-ai-reviewer.md -->

**Document:** `Docs/Decisions/spec-ai-reviewer/spec.md`
**Status:** Ready for Review
**Branch:** `feature/ci-ai-reviewer`
**PR:** #35
**Phase:** 1
**Depends on:** PRs #33 and #34 merged (CI core pipeline live)
**Author:** Joe / Claude Architect Session
**Date:** 2026-03-31

---

## Table of Contents

- [User Story](#user-story)
- [Goals and Non-Goals](#goals-and-non-goals)
- [Design Decisions and Rationale](#design-decisions-and-rationale)
- [Pipeline Architecture](#pipeline-architecture)
- [Files Delivered in This PR](#files-delivered-in-this-pr)
- [Job Definition: ai-review](#job-definition-ai-review)
  - [Trigger and Conditions](#trigger-and-conditions)
  - [Permissions](#permissions)
  - [Step-by-Step Breakdown](#step-by-step-breakdown)
  - [Full YAML Block](#full-yaml-block)
- [Claude API Call Structure](#claude-api-call-structure)
- [Claude Response Schema](#claude-response-schema)
- [PR Comment Format](#pr-comment-format)
- [Review Lifecycle](#review-lifecycle)
- [Fail-Open Behavior](#fail-open-behavior)
- [System Prompt](#system-prompt)
- [Secrets and Configuration](#secrets-and-configuration)
- [Acceptance Criteria](#acceptance-criteria)
- [Out of Scope](#out-of-scope)
- [Known Constraints and Future Hardening](#known-constraints-and-future-hardening)

---

## User Story

> As a developer submitting a pull request to FeatureFlagService, I want an AI reviewer
> to automatically inspect my diff and flag Clean Architecture violations, FluentValidation v12
> misuse, and missing CancellationToken propagation — so that I catch design issues before
> human review, without blocking my PR on transient infrastructure failures.

---

## Goals and Non-Goals

**Goals:**

- Implement the `ai-review` job stubbed in `ci.yml` during PRs #33/#34
- Call the Claude API with the PR diff and a project-specific system prompt
- Post a structured top-level PR comment summarizing findings
- Submit a formal `REQUEST_CHANGES` or `APPROVE` GitHub review state
- Fail open if the API is unavailable — the PR must remain mergeable
- Dismiss the previous bot review before posting a new one (no stale reviews accumulating)
- Keep the `ANTHROPIC_API_KEY` out of all log output

**Non-Goals:**

- Inline diff comments (requires diff position calculation — see [Design Decisions](#design-decisions-and-rationale))
- Integration test execution (Phase 2)
- Code coverage gating (Phase 2)
- Branch protection rule configuration (manual GitHub UI step — not in YAML)
- Any deployment steps

---

## Design Decisions and Rationale

### Decision 1: Top-Level Comment, Not Inline Diff Comments

**The problem:** GitHub's review API (`POST /pulls/{number}/reviews`) requires a `position`
value for each inline comment — not a line number. Position is the number of lines from
the first `@@` hunk header in the unified diff. Claude's analysis produces file paths and
approximate line numbers, not diff positions.

**Options considered:**

| Option | Approach | Problem |
|---|---|---|
| A | Inline comments using `position` | Requires parsing unified diff hunk headers to convert line → position. Fragile, fails silently if the line isn't in the diff. |
| B | Inline comments using `line` + `side` | GitHub docs confirm `line` is supported for single-file comments — but the multi-comment review endpoint still uses `position`. Mixing approaches adds complexity. |
| C | Top-level PR comment with file + line references | Claude outputs file path + line number in Markdown table. Comment appears in the conversation thread. No diff parsing required. |

**Decision: Option C.** A single structured top-level comment with a table of findings is
reliable, readable, and immune to diff position calculation errors. The formal review state
(`REQUEST_CHANGES` / `APPROVE`) is set separately using the review submission endpoint —
this is what blocks or approves the merge. This matches the pattern used by popular automated
review tools including Danger.js and Reviewdog in fallback mode.

---

### Decision 2: `actions/github-script` Over Raw `curl` for GitHub API Calls

**The problem:** Posting a review and dismissing a previous one requires multiple GitHub API
calls. Raw `curl` requires manual JSON escaping of multi-line bodies, manual error handling,
and leaks `GITHUB_TOKEN` into shell command strings.

**Decision:** Use `actions/github-script@v7` for all GitHub API interactions. It provides
the authenticated `github` client and `context` object automatically, handles JSON safely,
and is the documented best practice in GitHub Actions.

Raw `curl` is retained for the Claude API call because `actions/github-script` does not
support external HTTP calls via `fetch` in the Actions environment.

---

### Decision 3: Diff Fetched via `actions/github-script`, Not `git diff`

**The problem:** `git diff` on the runner compares local branches. The PR diff needs to
reflect exactly what GitHub shows in the Files Changed tab — the unified diff between
the base and head commits of the PR.

**Decision:** Fetch the diff using the GitHub REST API (`GET /repos/{owner}/{repo}/pulls/{number}`)
with `Accept: application/vnd.github.v3.diff`. This returns the canonical PR diff and
does not require branch checkout juggling.

---

### Decision 4: Character Truncation, Not Token Counting

**The problem:** Claude's context window is measured in tokens, not characters. Exact
token counting requires running a tokenizer on the runner — an unnecessary dependency.

**Decision:** Truncate the diff at 48,000 characters before sending to the API. At ~4 chars
per token average, this approximates 12,000 tokens — well within the Sonnet context window
and leaving room for the system prompt and response. The truncation is done with `head -c 48000`
in bash. The system prompt must instruct Claude to note if the diff appears truncated.

---

## Pipeline Architecture

```
PR opened/updated/labeled targeting dev or main
  │
  ├── lint-format  ──────────┐
  │                          ├──► both pass
  └── build-test  ──────────┘
                              │
                    label 'ai-review' present?
                              │ YES
                              ▼
                         ai-review job
                              │
                    ┌─────────────────────┐
                    │ 1. Dismiss prev bot │
                    │    review           │
                    │ 2. Fetch PR diff    │
                    │    (GitHub API)     │
                    │ 3. Truncate diff    │
                    │    (48k chars)      │
                    │ 4. Read system      │
                    │    prompt from file │
                    │ 5. POST to Claude   │
                    │    API (Sonnet)     │
                    │ 6. Parse JSON       │
                    │    response         │
                    │ 7. Post PR comment  │
                    │    + submit review  │
                    └─────────────────────┘
                              │
               ┌──────────────┴──────────────┐
           approved: true               approved: false
               │                             │
         APPROVE review             REQUEST_CHANGES review
         (merge enabled)            (merge blocked for
                                     non-admins)
```

---

## Files Delivered in This PR

| File | Action | Notes |
|---|---|---|
| `.github/workflows/ci.yml` | Modify | Uncomment and implement the `ai-review` job stub |
| `.github/prompts/ai-review-system.md` | Create | System prompt read at runtime by the workflow |

No new C# files. No changes to `src/` or `tests/`.

---

## Job Definition: ai-review

### Trigger and Conditions

The job must run only when ALL of the following are true:

1. The triggering event is `pull_request`
2. The PR carries the label `ai-review`
3. Both `lint-format` and `build-test` have passed (`needs: [lint-format, build-test]`)

```yaml
needs: [lint-format, build-test]
if: >
  github.event_name == 'pull_request' &&
  contains(github.event.pull_request.labels.*.name, 'ai-review')
```

The `labeled` event type (already present in the trigger config from PR #34) ensures the
job fires when the label is added to an already-open PR, not just when the PR is first opened.

---

### Permissions

The `ai-review` job requires explicit permission to write pull request reviews. This must
be declared at the job level, not the workflow level, to avoid granting broad permissions
to the other jobs.

```yaml
permissions:
  contents: read
  pull-requests: write
```

---

### Step-by-Step Breakdown

| # | Step Name | Tool | Purpose |
|---|---|---|---|
| 1 | Checkout | `actions/checkout@v4` | Required to read `.github/prompts/ai-review-system.md` at runtime |
| 2 | Dismiss previous bot review | `actions/github-script@v7` | Prevents stale `REQUEST_CHANGES` reviews from accumulating across runs |
| 3 | Fetch and truncate PR diff | `actions/github-script@v7` + bash | Retrieves canonical PR diff from GitHub API; truncates to 48k chars |
| 4 | Read system prompt | bash `cat` | Reads prompt file into an env var for use in the API call |
| 5 | Call Claude API | bash `curl` | POSTs diff to Claude Sonnet; captures JSON response |
| 6 | Validate and parse response | bash + `jq` | Checks JSON schema; routes to fail-open path if invalid |
| 7 | Post PR comment | `actions/github-script@v7` | Posts structured Markdown comment to PR conversation |
| 8 | Submit GitHub review | `actions/github-script@v7` | Submits `REQUEST_CHANGES` or `APPROVE` based on `approved` field |

---

### Full YAML Block

The following replaces the commented stub in `ci.yml`. Claude Code must uncomment the
stub and replace it entirely with this block.

```yaml
  ai-review:
    name: AI Code Review
    runs-on: ubuntu-latest
    needs: [lint-format, build-test]
    if: >
      github.event_name == 'pull_request' &&
      contains(github.event.pull_request.labels.*.name, 'ai-review')
    permissions:
      contents: read
      pull-requests: write

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      # Step 2: Dismiss any previous REQUEST_CHANGES review from the bot.
      # This prevents stale blocking reviews from accumulating across re-runs.
      - name: Dismiss previous bot review
        uses: actions/github-script@v7
        with:
          script: |
            const reviews = await github.rest.pulls.listReviews({
              owner: context.repo.owner,
              repo: context.repo.repo,
              pull_number: context.payload.pull_request.number,
            });
            const botLogin = 'github-actions[bot]';
            for (const review of reviews.data) {
              if (
                review.user.login === botLogin &&
                review.state === 'CHANGES_REQUESTED'
              ) {
                await github.rest.pulls.dismissReview({
                  owner: context.repo.owner,
                  repo: context.repo.repo,
                  pull_number: context.payload.pull_request.number,
                  review_id: review.id,
                  message: 'Dismissed — re-reviewing updated diff.',
                });
              }
            }

      # Step 3: Fetch the canonical PR diff from GitHub and truncate to ~12k tokens.
      # Using the GitHub API (not git diff) ensures the diff matches the Files Changed tab.
      - name: Fetch and truncate PR diff
        uses: actions/github-script@v7
        id: get-diff
        with:
          script: |
            const response = await github.request(
              'GET /repos/{owner}/{repo}/pulls/{pull_number}',
              {
                owner: context.repo.owner,
                repo: context.repo.repo,
                pull_number: context.payload.pull_request.number,
                headers: {
                  accept: 'application/vnd.github.v3.diff',
                },
              }
            );
            const diff = String(response.data);
            const truncated = diff.length > 48000
              ? diff.substring(0, 48000) + '\n\n[diff truncated at 48,000 characters]'
              : diff;
            core.setOutput('diff', truncated);

      # Step 4: Read the system prompt from the versioned file.
      # Never hardcode the system prompt in YAML — it must be reviewable via Git history.
      - name: Read system prompt
        id: read-prompt
        run: |
          SYSTEM_PROMPT=$(cat .github/prompts/ai-review-system.md)
          echo "prompt<<EOF" >> $GITHUB_OUTPUT
          echo "$SYSTEM_PROMPT" >> $GITHUB_OUTPUT
          echo "EOF" >> $GITHUB_OUTPUT

      # Step 5: Call the Claude API.
      # ANTHROPIC_API_KEY is never echoed or interpolated into a logged string.
      # --max-time 60 ensures the job fails fast on a hung connection rather than consuming
      # the full 6-hour Actions timeout.
      - name: Call Claude API
        id: claude-review
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
          DIFF: ${{ steps.get-diff.outputs.diff }}
          SYSTEM_PROMPT: ${{ steps.read-prompt.outputs.prompt }}
        run: |
          RESPONSE=$(curl -s --max-time 60 -X POST https://api.anthropic.com/v1/messages \
            -H "x-api-key: $ANTHROPIC_API_KEY" \
            -H "anthropic-version: 2023-06-01" \
            -H "content-type: application/json" \
            --data-binary @- << 'CURL_EOF'
          {
            "model": "claude-sonnet-4-6",
            "max_tokens": 2048,
            "system": ${{ toJSON(steps.read-prompt.outputs.prompt) }},
            "messages": [
              {
                "role": "user",
                "content": "Review the following pull request diff:\n\n<diff>\n${{ steps.get-diff.outputs.diff }}\n</diff>"
              }
            ]
          }
          CURL_EOF
          )
          echo "response<<EOF" >> $GITHUB_OUTPUT
          echo "$RESPONSE" >> $GITHUB_OUTPUT
          echo "EOF" >> $GITHUB_OUTPUT

      # Step 6: Validate and parse the response.
      # If the response is missing required fields, route to the fail-open path.
      - name: Validate and parse response
        id: parse
        env:
          RESPONSE: ${{ steps.claude-review.outputs.response }}
        run: |
          # Extract the text content block from Claude's response envelope
          CONTENT=$(echo "$RESPONSE" | jq -r '.content[0].text // empty')

          if [ -z "$CONTENT" ]; then
            echo "valid=false" >> $GITHUB_OUTPUT
            exit 0
          fi

          # Validate required fields are present
          HAS_SUMMARY=$(echo "$CONTENT" | jq 'has("summary")' 2>/dev/null || echo "false")
          HAS_ISSUES=$(echo "$CONTENT" | jq 'has("issues")' 2>/dev/null || echo "false")
          HAS_APPROVED=$(echo "$CONTENT" | jq 'has("approved")' 2>/dev/null || echo "false")

          if [ "$HAS_SUMMARY" = "true" ] && [ "$HAS_ISSUES" = "true" ] && [ "$HAS_APPROVED" = "true" ]; then
            echo "valid=true" >> $GITHUB_OUTPUT
            echo "content<<EOF" >> $GITHUB_OUTPUT
            echo "$CONTENT" >> $GITHUB_OUTPUT
            echo "EOF" >> $GITHUB_OUTPUT
          else
            echo "valid=false" >> $GITHUB_OUTPUT
          fi

      # Step 7 (fail-open path): Post unavailability notice if parsing failed.
      - name: Post unavailability notice
        if: steps.parse.outputs.valid == 'false'
        uses: actions/github-script@v7
        with:
          script: |
            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.payload.pull_request.number,
              body: `## AI Code Review — Unavailable\n\nThe AI reviewer could not complete this review (API error or unexpected response format).\n\nThis check is advisory. The PR remains mergeable — proceed with human review.`,
            });

      # Step 7 (success path): Post structured review comment.
      - name: Post PR review comment
        if: steps.parse.outputs.valid == 'true'
        uses: actions/github-script@v7
        env:
          REVIEW_CONTENT: ${{ steps.parse.outputs.content }}
        with:
          script: |
            const content = JSON.parse(process.env.REVIEW_CONTENT);
            const { summary, issues, approved } = content;

            const severityIcon = (s) => {
              if (s === 'error') return '🔴';
              if (s === 'warning') return '🟡';
              return '🔵';
            };

            let body = `## AI Code Review\n\n**Summary:** ${summary}\n\n---\n\n`;

            if (issues.length === 0) {
              body += `✅ No issues found.\n`;
            } else {
              body += `### Issues Found\n\n`;
              body += `| Severity | File | Line | Comment |\n`;
              body += `|---|---|---|---|\n`;
              for (const issue of issues) {
                body += `| ${severityIcon(issue.severity)} ${issue.severity} | \`${issue.file}\` | ${issue.line} | ${issue.comment} |\n`;
              }
            }

            body += `\n---\n_Reviewed by Claude \`claude-sonnet-4-6\` · Dismiss this review in the GitHub UI to override_`;

            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.payload.pull_request.number,
              body,
            });

      # Step 8: Submit the formal GitHub review state.
      # REQUEST_CHANGES blocks merge for non-admins. APPROVE enables merge.
      # This is a separate call from the comment — review state and comment are distinct.
      - name: Submit GitHub review
        if: steps.parse.outputs.valid == 'true'
        uses: actions/github-script@v7
        env:
          REVIEW_CONTENT: ${{ steps.parse.outputs.content }}
        with:
          script: |
            const content = JSON.parse(process.env.REVIEW_CONTENT);
            const event = content.approved ? 'APPROVE' : 'REQUEST_CHANGES';
            const body = content.approved
              ? 'AI review passed. No issues found.'
              : 'AI review found issues. See the comment above for details. Dismiss this review to override.';

            await github.rest.pulls.createReview({
              owner: context.repo.owner,
              repo: context.repo.repo,
              pull_number: context.payload.pull_request.number,
              commit_id: context.payload.pull_request.head.sha,
              event,
              body,
            });
```

---

## Claude API Call Structure

```json
POST https://api.anthropic.com/v1/messages

Headers:
  x-api-key: <from ANTHROPIC_API_KEY secret>
  anthropic-version: 2023-06-01
  content-type: application/json

Body:
{
  "model": "claude-sonnet-4-6",
  "max_tokens": 2048,
  "system": "<contents of .github/prompts/ai-review-system.md>",
  "messages": [
    {
      "role": "user",
      "content": "Review the following pull request diff:\n\n<diff>\n{TRUNCATED_DIFF}\n</diff>"
    }
  ]
}
```

**Model:** `claude-sonnet-4-6` — current Sonnet release. Smart enough to reason about
architecture and FluentValidation v12 constraints. Fast enough for CI use. Do not use Opus.

**Max tokens:** 2048 — sufficient for a summary paragraph plus a reasonable number of issues.
Increase to 4096 if large PRs consistently produce truncated responses (monitor via Actions log).

---

## Claude Response Schema

Claude must be instructed (via system prompt) to return ONLY a valid JSON object — no
Markdown fences, no preamble, no trailing explanation.

```json
{
  "summary": "string — one paragraph describing the overall state of the PR",
  "issues": [
    {
      "file": "string — relative path from repo root, e.g. FeatureFlag.Api/Controllers/FeatureFlagsController.cs",
      "line": "number — approximate line number in the file (not the diff position)",
      "severity": "error | warning | suggestion",
      "comment": "string — plain English, actionable description of the issue"
    }
  ],
  "approved": "boolean — true only when issues array is empty and no concerns found"
}
```

**Severity definitions Claude must follow:**

| Severity | When to use |
|---|---|
| `error` | Violation of an enforced rule (Clean Architecture boundary, FluentValidation v12 constraint, missing CancellationToken). PR should not merge without fix. |
| `warning` | Likely problem, or deviation from established pattern. Should be addressed but may be intentional. |
| `suggestion` | Improvement, not a violation. Reviewer notes it but does not block merge. |

---

## PR Comment Format

The comment posted to the PR conversation thread will look like this:

```markdown
## AI Code Review

**Summary:** The controller correctly delegates to the service layer using DTOs,
and CancellationToken is propagated throughout. One issue found: the validator
uses `.Transform()` which was removed in FluentValidation v12. Two suggestions
for improved guard clause placement.

---

### Issues Found

| Severity | File | Line | Comment |
|---|---|---|---|
| 🔴 error | `FeatureFlag.Application/Validators/CreateFlagRequestValidator.cs` | 24 | `.Transform()` was removed in FluentValidation v12. Replace with a `.Must()` lambda that applies the same sanitization. |
| 🔵 suggestion | `FeatureFlag.Api/Controllers/FeatureFlagsController.cs` | 18 | Consider moving the null guard above the validation call for consistency with other actions. |

---
_Reviewed by Claude `claude-sonnet-4-6` · Dismiss this review in the GitHub UI to override_
```

When no issues are found:

```markdown
## AI Code Review

**Summary:** All changed files follow Clean Architecture boundaries, use DTOs
correctly, and propagate CancellationToken throughout. No issues found.

---

✅ No issues found.

---
_Reviewed by Claude `claude-sonnet-4-6` · Dismiss this review in the GitHub UI to override_
```

---

## Review Lifecycle

**When `approved: false`:**
1. A top-level PR comment with the issues table is posted
2. A formal `REQUEST_CHANGES` review is submitted — the merge button is disabled for non-admins
3. The PR author addresses the issues and pushes new commits
4. The `synchronize` event re-triggers the workflow
5. Step 2 dismisses the previous `REQUEST_CHANGES` review
6. Claude reviews the updated diff and posts a fresh comment and review state

**When `approved: true`:**
1. A top-level PR comment with "No issues found" is posted
2. A formal `APPROVE` review is submitted — the merge button is enabled

**Admin override:**
- The repo admin can dismiss the `REQUEST_CHANGES` review via `PR → Reviews → Dismiss review`
- After dismissal, the merge button re-enables immediately
- No re-run required. No further CI action is triggered.
- GitHub admins can always dismiss reviews on repos they own — no branch protection
  configuration required for this to work.

**One active review at a time:**
- Step 2 always dismisses previous bot `REQUEST_CHANGES` reviews before posting a new one
- At most one bot review is active on the PR at any point in time

---

## Fail-Open Behavior

The `ai-review` job must never block a PR due to transient infrastructure issues.

**Triggers for fail-open path:**

- `curl` returns non-200 HTTP status from Claude API
- `curl` times out (after 60 seconds, per `--max-time 60`)
- Response body is not valid JSON
- Response JSON does not contain all three required fields (`summary`, `issues`, `approved`)

**Fail-open action:**

1. Post this comment to the PR:

   ```
   ## AI Code Review — Unavailable

   The AI reviewer could not complete this review (API error or unexpected response format).

   This check is advisory. The PR remains mergeable — proceed with human review.
   ```

2. The job exits with code `0` — it does not fail, and the PR is not blocked.

**Future hardening (tracked, not in scope for Phase 1):**

When the project moves toward a production CI posture (Phase 8+), consider:
- Retry with exponential backoff before triggering fail-open
- Fail-closed for PRs targeting `main` only (security-critical path)
- Alert via GitHub issue comment with the raw error response for diagnosis

---

## System Prompt

Stored at `.github/prompts/ai-review-system.md`. Read at runtime — never hardcoded in YAML.
Versioned in Git so changes to review behavior are auditable.

```markdown
You are a senior .NET engineer performing a code review on a pull request for FeatureFlagService.

FeatureFlagService is a .NET 10 Web API built with strict Clean Architecture:

- Domain layer (FeatureFlag.Domain): entities, value objects, enums, interfaces. Zero outward dependencies.
- Application layer (FeatureFlag.Application): services, DTOs, validators, strategies. Depends only on Domain.
- Infrastructure layer (FeatureFlag.Infrastructure): EF Core, Npgsql, PostgreSQL, repository implementations. Depends on Application and Domain.
- API layer (FeatureFlag.Api): controllers, middleware, DI wiring. Depends only on Application.

The dependency rule is absolute: inner layers never reference outer layers.

Rules to enforce:

1. Domain entities (e.g. `Flag`) must never appear in controller method signatures,
   return types, or cross any service boundary. Use DTOs only.
2. `IFeatureFlagService` methods must accept and return DTOs only — never the `Flag` entity.
3. FluentValidation version is 12. Do not suggest `.Transform()` — it was removed in v12.
   The correct pattern is a `.Must()` lambda that performs the same transformation.
4. Validators are registered with explicit `AddScoped<IValidator<T>, TValidator>()` in DI.
   Do not suggest `AddValidatorsFromAssemblyContaining` — it is not used in this project.
5. Controllers call `ValidateAsync()` manually and return `ValidationProblem()` on failure.
   `FluentValidation.AspNetCore` and `AddFluentValidationAutoValidation()` are not used.
6. All async methods must propagate `CancellationToken` through every call site, including
   repository calls and external service calls.
7. Evaluation logic must remain deterministic and isolated from persistence.
   Do not suggest mixing evaluation logic with repository calls.
8. Do not suggest adding `try/catch` blocks in controllers. A global exception middleware
   handles unhandled exceptions and produces standardized `ProblemDetails` responses.
9. Naming conventions: interfaces prefixed with `I`, async methods suffixed with `Async`,
   no abbreviations in public member names.
10. Zero warnings policy: do not suggest suppressing warnings with `#pragma warning disable`
    or `[SuppressMessage]` without an explanatory comment justifying the suppression.

Review the diff for:

- Violations of the rules above
- SOLID principle violations (especially Open/Closed — watch for switch statements
  or long if/else chains where a strategy or registry pattern should be used)
- Logic errors or missing precondition guards
- Security concerns: unvalidated input reaching persistence, injection risk, sensitive
  data logged or exposed in responses
- Missing or incorrect `CancellationToken` propagation
- Any pattern inconsistent with the established codebase conventions described above

Do NOT flag:

- Formatting or whitespace — CSharpier handles this in a separate CI step
- Missing XML documentation on internal or private members
- Test coverage gaps — tracked separately in Phase 2
- Stylistic preferences with no correctness or maintainability impact

If the diff appears to be truncated (ends abruptly or contains a truncation notice),
note this in your summary and limit findings to what is visible.

Respond ONLY with a valid JSON object. No Markdown code fences. No preamble. No explanation
outside the JSON structure. The response must be parseable by `JSON.parse()` with no
preprocessing.

Required schema:
{
  "summary": "string — one paragraph overview of the review",
  "issues": [
    {
      "file": "string — relative path from repo root",
      "line": number — approximate line number in the source file,
      "severity": "error | warning | suggestion",
      "comment": "string — plain English, actionable"
    }
  ],
  "approved": boolean — true only if issues array is empty and no concerns found
}
```

---

## Secrets and Configuration

| Secret name | Value source | Setup required |
|---|---|---|
| `ANTHROPIC_API_KEY` | console.anthropic.com | ✅ Must be added manually before first run |
| `GITHUB_TOKEN` | Auto-injected by GitHub Actions | ❌ None — use `${{ secrets.GITHUB_TOKEN }}` |

**To add `ANTHROPIC_API_KEY`:**
`GitHub repo → Settings → Secrets and variables → Actions → New repository secret`

**Secret hygiene rules:**

- `ANTHROPIC_API_KEY` must be passed to the `curl` command via the `env:` block — not
  interpolated inline in the shell command string
- No step may `echo`, `print`, or otherwise log the value of `ANTHROPIC_API_KEY`
- `GITHUB_TOKEN` is never stored or hardcoded — always referenced as `${{ secrets.GITHUB_TOKEN }}`

---

## Acceptance Criteria

### AC-1: Trigger behavior
- [ ] A PR to `dev` or `main` WITHOUT the `ai-review` label shows the `ai-review` job as
  skipped (not absent — it appears in the workflow run but does not execute)
- [ ] Adding the `ai-review` label to an open PR triggers the `ai-review` job via the
  `labeled` event
- [ ] The `ai-review` job does not start until both `lint-format` and `build-test` have
  green checkmarks

### AC-2: Review comment posted
- [ ] After the job runs, a comment appears in the PR conversation thread beginning with
  `## AI Code Review`
- [ ] The comment includes a **Summary** paragraph
- [ ] When issues are present, the comment includes a Markdown table with Severity, File,
  Line, and Comment columns
- [ ] When no issues are present, the comment contains `✅ No issues found.`

### AC-3: GitHub review state set
- [ ] When Claude returns `approved: false`, the PR shows `Changes requested` review state
  and the merge button is disabled for non-admins
- [ ] When Claude returns `approved: true`, the PR shows `Approved` review state

### AC-4: Admin override
- [ ] The repo admin can dismiss the bot review via `PR → Reviews → Dismiss review`
- [ ] After dismissal, the merge button re-enables without re-running any job

### AC-5: No stale review accumulation
- [ ] Pushing a second commit to a PR with the `ai-review` label results in exactly one
  active bot review — the previous `REQUEST_CHANGES` review is dismissed before the new
  one is posted

### AC-6: Fail-open on API error
- [ ] If `ANTHROPIC_API_KEY` is temporarily set to an invalid value, the `ai-review` job
  exits with code `0` (green checkmark in Actions)
- [ ] A comment is posted: `## AI Code Review — Unavailable`
- [ ] The PR remains mergeable after the fail-open path

### AC-7: Secret hygiene
- [ ] `ANTHROPIC_API_KEY` does not appear in any step log in the Actions run
- [ ] `GITHUB_TOKEN` is not hardcoded anywhere in the YAML

### AC-8: System prompt is file-based
- [ ] `.github/prompts/ai-review-system.md` exists in the repo after this PR merges
- [ ] The YAML reads it via `cat` at runtime — the prompt text is not hardcoded in `ci.yml`

---

## Out of Scope

Must not be implemented in this PR:

- Inline diff comments (requires diff position calculation — see Design Decisions)
- Integration test execution (Phase 2 — requires Postgres service container)
- Code coverage reporting (Phase 2)
- SonarCloud or any third-party static analysis
- Branch protection rule configuration (manual GitHub UI step)
- Any deployment step

---

## Known Constraints and Future Hardening

| Constraint | Detail | Tracked For |
|---|---|---|
| No inline diff comments | GitHub's `position` parameter requires diff-position math, not line numbers. All findings appear in a top-level comment table. | Phase 4+ if prioritized |
| Fail-open on API error | Transient Claude API failures do not block the PR. No retry logic in Phase 1. | Phase 8 — retry with backoff, fail-closed on `main` |
| Single review per run | One `REQUEST_CHANGES` or `APPROVE` review active at a time. Previous reviews are dismissed, not deleted — they appear in the review history. | Acceptable for Phase 1 |
| Diff truncation at 48k chars | Very large PRs lose tail content. Claude is instructed to note truncation in the summary. | Increase `max_tokens` or split large PRs manually |
| `claude-sonnet-4-6` model pinned | Model string is hardcoded in the YAML. Update manually when a new Sonnet version is preferred. | Phase 8 — parameterize via env var |

---

*FeatureFlagService | feature/ci-ai-reviewer | Phase 1 — AI Reviewer | v1.0*
