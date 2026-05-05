---
name: git-workflow
description: >
  Generate a feature branch setup command, conventional commits, a push command, and
  a standardized pull request creation command for the completed feature. Use this skill whenever the user says
  "/git-workflow", "generate commits", "write the PR", "let's commit", or after
  post-work documentation is complete. The user takes over after this point.
---

# Git Workflow

Produce the exact git commands and PR content the user needs to copy-paste. No
guesswork, no placeholders. Everything here is ready to run or paste. The workflow
must never push feature work directly to `dev`, `main`, or `master`.

## Before you begin — confirm readiness

Ask: "Is post-work complete? Are all foundation docs updated?" Do not proceed until
confirmed. This is the final step — foundation docs must already reflect the new state.

## Step 1 — Read the context

Read these to produce accurate commit messages and PR content:

- `docs/decisions/<feature-branch-name>/spec.md` — scope, phase, feature name
- `docs/decisions/<feature-branch-name>/implementation-notes.md` — what was built,
  deviations, key decisions, file-by-file changes

## Step 2 — Create or switch to the feature branch first

Before staging or committing, generate a branch setup command.

**Rules:**
- If currently on `dev`, `main`, or `master`, create a feature branch before any
  commit commands.
- Prefer the branch name from the spec frontmatter/body if present, e.g.
  `fix/ai-response-validation`.
- If the spec does not name a branch, derive one from the decision folder or feature
  name using lowercase kebab-case and one of these prefixes:
  - `feature/` for net-new behavior
  - `fix/` for bug fixes or hardening
  - `refactor/` for structural changes
  - `docs/` for documentation-only work
- If already on a non-base feature branch, keep that branch and include a comment
  confirming it.
- Never emit `git push origin dev`, `git push origin main`, or
  `git push origin master` for feature work.

**Example output format:**

```bash
# Branch setup
git switch -c fix/ai-response-validation
```

## Step 3 — Group changes into logical commits

Group file changes into 3–5 commits maximum. Use this grouping priority:

| Group | Files | Commit type |
|---|---|---|
| Domain / Application layer | entities, value objects, interfaces, services | `feat` or `refactor` |
| Infrastructure / Persistence | repositories, DbContext, migrations, config | `feat` or `refactor` |
| API layer | controllers, middleware, DTOs, DI registration | `feat` or `fix` |
| Tests | unit tests, integration tests | `test` |
| Documentation | all docs/decisions/*, docs/architecture.md, etc. | `docs` |

Never mix code and documentation in the same commit. The docs commit is always last.

## Step 4 — Write the commits

Use Conventional Commits format strictly:

```
<type>(<scope>): <short description in sentence case, ≤72 chars>

<optional body: what and why, not how. Wrap at 72 chars.>
```

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
Scope: the layer or domain area (e.g., `domain`, `api`, `infra`, `tests`, `docs`)

**Rules:**
- Description is sentence case, no period at the end
- Body is optional but include it for non-obvious changes
- One blank line between subject and body
- Reference the spec in the docs commit body

**Example output format:**

```bash
# Commit 1 — Domain layer
git add src/FeatureFlag.Domain/Exceptions/ src/FeatureFlag.Domain/Entities/
git commit -m "feat(domain): add domain exception hierarchy and base exception

Named exception types carry HTTP status codes so the middleware
never needs to change when new error conditions are added."

# Commit 2 — API layer
git add src/FeatureFlag.Api/Middleware/ src/FeatureFlag.Api/DependencyInjection.cs
git commit -m "feat(api): add GlobalExceptionMiddleware and wire into pipeline"

# Commit 3 — Tests
git add tests/FeatureFlag.Tests/Middleware/
git commit -m "test(api): add unit tests for GlobalExceptionMiddleware"

# Commit 4 — Docs
git add docs/
git commit -m "docs: update foundation docs and add implementation notes for exception handling

Spec: docs/decisions/feat/global-exception-handling/spec.md"
```

## Step 5 — Write the push command

```bash
git push -u origin <feature-branch-name>
```

The branch name here must match the branch setup command from Step 2. Do not use
the current base branch name unless the work is intentionally documentation or
repository maintenance on a non-protected branch and the user explicitly requested it.

## Step 6 — Write the PR creation command

**Title format:** `<type>(<scope>): <Feature Name> — Phase <X>`

Write the PR body to a temporary markdown file, then create the PR with `gh`.
Default base branch is `dev` unless the repository default branch or user request
clearly says otherwise.

**Command template:**

```bash
cat > /tmp/<feature-slug>-pr.md <<'EOF'
<PR body markdown>
EOF

gh pr create \
  --base dev \
  --head <feature-branch-name> \
  --title "<type>(<scope>): <Feature Name> — Phase <X>" \
  --body-file /tmp/<feature-slug>-pr.md \
  --draft
```

**Body template:**

```markdown
## Summary

<2–3 sentences from the "What Was Built" section of implementation-notes.md>

## Changes in this PR

<bullet list of the logical commit groups — one line each>

## Spec

`docs/decisions/<feature-branch-name>/spec.md`

## Implementation Notes

`docs/decisions/<feature-branch-name>/implementation-notes.md`

## Definition of Done

<paste the DoD checklist from the spec with checkmarks applied>

## Testing

<paste the "How to Test" section from implementation-notes.md>
```

## Step 7 — Hand off

Output the branch setup, commit, push, and PR creation commands in copy-pasteable
blocks, then end with:

> That's everything. Copy the commands and run them in order. The branch will be
> pushed and the draft PR will be opened for you. The rest is yours.

Do not suggest any further agent actions. The session ends here.
